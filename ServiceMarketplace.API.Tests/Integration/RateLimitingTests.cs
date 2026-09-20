using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Configuration;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.Application.Constants;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

// Each test owns its host/limiter state; the ordinary fixture still disables limits.
public sealed class RateLimitingTests
{
    private sealed class RateLimitedFactory : ServiceMarketplaceWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Testing:EnableRateLimiting"] = "true" }));
        }
    }

    [Fact]
    public async Task RegisterEndpoint_EnforcesRateLimit_Auth_5PerMinute()
    {
        using var factory = new RateLimitedFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new { });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/register", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_EnforcesRateLimit_Auth_5PerMinute()
    {
        using var factory = new RateLimitedFactory();
        using var client = factory.CreateClient();
        // No registration consumes this host's shared auth budget.
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = "missing@test.invalid", Password = "test" });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task RefreshEndpoint_EnforcesRateLimit_Refresh_10PerMinute()
    {
        using var factory = new RateLimitedFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "invalid" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task RateLimitResponse_IncludesRetryAfterHeader()
    {
        using var factory = new RateLimitedFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            (await client.PostAsJsonAsync("/api/auth/login", new { })).Dispose();
        using var limited = await client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var body = await limited.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("rate_limit_exceeded", body.GetProperty("error").GetString());
        Assert.True(body.GetProperty("retryAfter").GetInt32() > 0);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task AuthenticatedUsersBehindSameIp_HaveIndependentPartitions()
    {
        using var factory = new RateLimitedFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        first.DefaultRequestHeaders.Authorization = new("Bearer", Token("customer-a"));
        second.DefaultRequestHeaders.Authorization = new("Bearer", Token("customer-b"));
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsJsonAsync("/api/requests", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.PostAsJsonAsync("/api/requests", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsJsonAsync("/api/requests", new { })).StatusCode);
    }

    [Fact]
    public async Task InvalidJwtAndSpoofedHeaders_CannotEscapeAnonymousPartition()
    {
        using var factory = new RateLimitedFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 6; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests?userId=" + i)
            {
                Content = JsonContent.Create(new { userId = i })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("forged-" + i, validSignature: false));
            request.Headers.Add("X-User-Id", "spoof-" + i);
            request.Headers.Add("X-Forwarded-For", "10.0.0." + i);
            using var response = await client.SendAsync(request);
            Assert.Equal(i < 5 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public void AnonymousPartition_UsesServerRemoteIp_NotUntrustedClaims()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "forged")]));
        Assert.Equal("ip:192.0.2.1", RateLimitIdentity.GetPartitionKey(context));
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
        Assert.Equal("ip:192.0.2.2", RateLimitIdentity.GetPartitionKey(context));
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "trusted")], "Bearer"));
        Assert.Equal("user:trusted", RateLimitIdentity.GetPartitionKey(context));
    }

    internal static string Token(string userId, bool validSignature = true, bool expired = false)
    {
        var key = validSignature ? "TEST_ONLY_JWT_SIGNING_KEY_32_BYTES_MINIMUM_12345" : "INVALID_SIGNING_KEY_32_BYTES_MINIMUM_123456789";
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "ServiceMarketplace", "ServiceMarketplaceUsers",
            [new Claim(ClaimTypes.NameIdentifier, userId),
             new Claim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceCustomer)],
            notBefore: DateTime.UtcNow.AddHours(-1), expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
    }
}
