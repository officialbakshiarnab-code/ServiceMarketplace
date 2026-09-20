using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>Parses UI claims; the API remains responsible for cryptographic validation.</summary>
public sealed class TokenAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly ITokenStorage _storage;
    private readonly SessionTokenRefreshService _session;
    private readonly ILogger<TokenAuthenticationStateProvider> _logger;
    private readonly object _timerLock = new();
    private Timer? _expiryTimer;
    private bool _disposed;
    public bool IsInitialized { get; private set; }

    public TokenAuthenticationStateProvider(ITokenStorage storage, SessionTokenRefreshService session,
        ILogger<TokenAuthenticationStateProvider> logger)
    {
        _storage = storage;
        _session = session;
        _logger = logger;
        _session.Changed += NotifyAuthenticationStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _storage.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { ClearTimer(); return Anonymous; }
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo <= DateTime.UtcNow)
            {
                if (!await _session.RefreshAsync(token)) { ClearTimer(); return Anonymous; }
                token = await _storage.GetTokenAsync();
                if (string.IsNullOrWhiteSpace(token)) return Anonymous;
                jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            }
            if (jwt.ValidTo <= DateTime.UtcNow || !jwt.Claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                await _session.SignOutAsync();
                return Anonymous;
            }
            lock (_timerLock)
            {
                if (!_disposed)
                {
                    _expiryTimer?.Dispose();
                    var due = jwt.ValidTo - DateTime.UtcNow;
                    _expiryTimer = new Timer(_ => NotifyAuthenticationStateChanged(), null,
                        due > TimeSpan.Zero ? due : TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan);
                }
            }
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "jwt")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Authentication state unavailable. FailureType: {FailureType}", ex.GetType().Name);
            ClearTimer();
            return Anonymous;
        }
        finally { IsInitialized = true; }
    }

    public void NotifyAuthenticationStateChanged()
    {
        if (!_disposed) NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
    public Task SignOutAsync() => _session.SignOutAsync();
    private void ClearTimer()
    {
        lock (_timerLock) { _expiryTimer?.Dispose(); _expiryTimer = null; }
    }
    public void Dispose()
    {
        lock (_timerLock) { _disposed = true; _expiryTimer?.Dispose(); _expiryTimer = null; }
        _session.Changed -= NotifyAuthenticationStateChanged;
    }
}
