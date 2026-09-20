using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public sealed class ClientRefreshReliabilityTests
{
    private sealed class Storage : ITokenStorage
    {
        public string? Token = "expired";
        public string? Refresh = "refresh-original";
        public Task<string?> GetTokenAsync() => Task.FromResult(Token);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);
        public Task SaveTokenAsync(string value) { Token = value; return Task.CompletedTask; }
        public Task SaveRefreshTokenAsync(string value) { Refresh = value; return Task.CompletedTask; }
        public Task ClearAsync() { Token = Refresh = null; return Task.CompletedTask; }
    }

    private sealed class Transport(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private static HttpResponseMessage Expired() => new(HttpStatusCode.Unauthorized)
    {
        Content = JsonContent.Create(new { error = "access_token_expired" })
    };
    private static HttpResponseMessage Refreshed(string token = "fresh") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new RefreshTokenResponse
        {
            AccessToken = token, RefreshToken = "refresh-rotated",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(10), RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        })
    };
    private static SessionTokenRefreshService Session(Storage storage, HttpMessageHandler transport) => new(storage,
        new RefreshTokenTransport(new HttpClient(transport) { BaseAddress = new Uri("https://api.test/") }),
        NullLogger<SessionTokenRefreshService>.Instance);
    private static HttpClient Client(Storage storage, SessionTokenRefreshService session, HttpMessageHandler transport) => new(
        new AuthorizingHttpClientHandler(storage, session) { InnerHandler = transport }) { BaseAddress = session.ApiBaseUri };

    [Fact]
    public async Task FiveSimultaneousExpiredRequests_RotateOnce_AndReplayBodiesOnce()
    {
        var storage = new Storage();
        var rotations = 0;
        var initial = 0;
        var retries = 0;
        var allInitial = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = Session(storage, new Transport(_ =>
        {
            Interlocked.Increment(ref rotations);
            return Task.FromResult(Refreshed());
        }));
        using var client = Client(storage, session, new Transport(async request =>
        {
            Assert.Equal("payload", await request.Content!.ReadAsStringAsync());
            Assert.Equal("preserved", request.Headers.GetValues("X-Test").Single());
            if (request.Headers.Authorization!.Parameter == "expired")
            {
                if (Interlocked.Increment(ref initial) == 5) allInitial.SetResult();
                await allInitial.Task.WaitAsync(TimeSpan.FromSeconds(5));
                return Expired();
            }
            Assert.Equal("fresh", request.Headers.Authorization.Parameter);
            Interlocked.Increment(ref retries);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders") { Content = new StringContent("payload") };
            request.Headers.Add("X-Test", "preserved");
            using var result = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }));
        Assert.Equal(1, rotations);
        Assert.Equal(5, initial);
        Assert.Equal(5, retries);
        Assert.Equal("refresh-rotated", storage.Refresh);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "forbidden")]
    [InlineData(HttpStatusCode.InternalServerError, "server_error")]
    public async Task GenuineFailure_DoesNotRefresh(HttpStatusCode status, string error)
    {
        var storage = new Storage();
        var rotations = 0;
        var session = Session(storage, new Transport(_ => { rotations++; return Task.FromResult(Refreshed()); }));
        using var client = Client(storage, session, new Transport(_ => Task.FromResult(new HttpResponseMessage(status)
        { Content = JsonContent.Create(new { error }) })));
        using var result = await client.GetAsync("api/orders");
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(0, rotations);
    }

    [Fact]
    public async Task RetryStillUnauthorized_StopsAndSignsOut()
    {
        var storage = new Storage();
        var rotations = 0;
        var sends = 0;
        var session = Session(storage, new Transport(_ => { rotations++; return Task.FromResult(Refreshed()); }));
        using var client = Client(storage, session, new Transport(_ => { sends++; return Task.FromResult(Expired()); }));
        using var result = await client.GetAsync("api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(1, rotations);
        Assert.Equal(2, sends);
        Assert.Null(storage.Token);
        Assert.Null(storage.Refresh);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRefresh_ClearsPairAndDoesNotReplay(bool networkFailure)
    {
        var storage = new Storage();
        var session = Session(storage, new Transport(_ => networkFailure
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var sends = 0;
        using var client = Client(storage, session, new Transport(_ => { sends++; return Task.FromResult(Expired()); }));
        using var result = await client.GetAsync("api/orders");
        Assert.Equal(1, sends);
        Assert.Null(storage.Token);
        Assert.Null(storage.Refresh);
    }

    [Fact]
    public async Task LogoutDuringRefresh_CannotRestoreSession()
    {
        var storage = new Storage();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = Session(storage, new Transport(async _ =>
        {
            started.SetResult();
            await finish.Task;
            return Refreshed();
        }));
        var refreshing = session.RefreshAsync("expired");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var logout = session.SignOutAsync();
        finish.SetResult();
        Assert.False(await refreshing);
        await logout;
        Assert.Null(storage.Token);
        Assert.Null(storage.Refresh);
    }

    [Fact]
    public async Task CancelledCaller_DoesNotAbandonAnAlreadyStartedRotation()
    {
        var storage = new Storage();
        using var cancellation = new CancellationTokenSource();
        var session = Session(storage, new Transport(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(Refreshed());
        }));
        Assert.True(await session.RefreshAsync("expired", cancellation.Token));
        Assert.Equal("refresh-rotated", storage.Refresh);
    }

    [Fact]
    public async Task StateProvider_RefreshesExpiredStoredToken_InsteadOfClearingIt()
    {
        var storage = new Storage { Token = RateLimitingTests.Token("customer", expired: true) };
        var session = Session(storage, new Transport(_ => Task.FromResult(Refreshed(RateLimitingTests.Token("customer")))));
        using var provider = new TokenAuthenticationStateProvider(storage, session, NullLogger<TokenAuthenticationStateProvider>.Instance);
        var state = await provider.GetAuthenticationStateAsync();
        Assert.True(state.User.Identity!.IsAuthenticated);
        Assert.Equal("refresh-rotated", storage.Refresh);
    }

    [Fact]
    public async Task SignedOutAndOtherOriginRequests_DoNotLeakStaleDefaultToken()
    {
        var storage = new Storage { Token = null, Refresh = null };
        var session = Session(storage, new Transport(_ => throw new InvalidOperationException("Must not refresh")));
        using var client = Client(storage, session, new Transport(request =>
        {
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", "stale");
        using var first = await client.GetAsync("api/orders");
        storage.Token = "active";
        using var second = await client.GetAsync("https://other.test/api/orders");
    }

    [Fact]
    public async Task Late401FromPreviousLogin_DoesNotReplayUnderNewAccount()
    {
        var storage = new Storage();
        var session = Session(storage, new Transport(_ => throw new InvalidOperationException("Must not rotate the new session")));
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = 0;
        using var client = Client(storage, session, new Transport(async _ =>
        {
            sends++;
            sent.SetResult();
            await finish.Task;
            return Expired();
        }));
        var pending = client.GetAsync("api/orders");
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.SignOutAsync();
        await session.SetTokensAsync("other-account", "other-refresh");
        finish.SetResult();
        using var result = await pending;
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(1, sends);
        Assert.Equal("other-account", storage.Token);
    }

    [Fact]
    public async Task FeatureApiClient_RefreshesThroughRealApiAndReturnsData()
    {
        using var factory = new ServiceMarketplaceWebApplicationFactory();
        using var transport = factory.CreateClient();
        var email = $"refresh-feature-{Guid.NewGuid():N}@test.com";
        using var registration = await transport.PostAsJsonAsync("/api/auth/register",
            ServiceMarketplace.API.Tests.Builders.AuthRequestBuilder.CreateUser(email).BuildRegisterRequest());
        registration.EnsureSuccessStatusCode();
        using var login = await transport.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Test@123456" });
        login.EnsureSuccessStatusCode();
        var pair = await login.Content.ReadFromJsonAsync<ServiceMarketplace.API.Models.Auth.AuthResponse>();
        var userId = ServiceMarketplace.API.Tests.Helpers.JwtTestHelper.GetClaim(pair!.Token, System.Security.Claims.ClaimTypes.NameIdentifier)!;
        var storage = new Storage { Token = RateLimitingTests.Token(userId, expired: true), Refresh = pair.RefreshToken };
        var session = new SessionTokenRefreshService(storage, new RefreshTokenTransport(transport), NullLogger<SessionTokenRefreshService>.Instance);
        using var client = factory.CreateDefaultClient(new AuthorizingHttpClientHandler(storage, session));
        var requests = new ServiceMarketplace.UI.Shared.Requests.RequestsApiClient(client, storage);
        Assert.Empty(await requests.GetMyRequestsAsync());
        Assert.NotEqual(pair.RefreshToken, storage.Refresh);
        Assert.False(ServiceMarketplace.API.Tests.Helpers.JwtTestHelper.IsTokenExpired(storage.Token!));
    }

    [Fact]
    public async Task Login401_DoesNotAttachSessionTokenOrRefresh()
    {
        var storage = new Storage();
        var session = Session(storage, new Transport(_ => throw new InvalidOperationException("Must not refresh")));
        using var client = Client(storage, session, new Transport(request =>
        {
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(Expired());
        }));
        using var result = await client.PostAsJsonAsync("api/auth/login", new { });
        Assert.Equal("expired", storage.Token);
    }

    [Fact]
    public async Task ApiExpiryChallenge_IsDistinctFromInvalidSignature()
    {
        using var factory = new ServiceMarketplaceWebApplicationFactory();
        using var client = factory.CreateClient();
        foreach (var valid in new[] { true, false })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/mine");
            request.Headers.Authorization = new("Bearer", RateLimitingTests.Token("customer", valid, expired: true));
            using var result = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            var body = await result.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(valid ? "access_token_expired" : "unauthorized", body.GetProperty("error").GetString());
        }
    }
}
