using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Custom authentication state provider that reads JWT tokens from storage.
/// Validates token expiration and extracts claims for Blazor authorization.
/// </summary>
public sealed class TokenAuthenticationStateProvider(ITokenStorage tokenStorage) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly ITokenStorage _tokenStorage = tokenStorage;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    /// <summary>
    /// Retrieves authentication state by reading and validating stored JWT token.
    /// Returns anonymous state if token is missing, expired, or invalid.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(Anonymous);

        try
        {
            // Parse JWT without validation (signature validation happens server-side)
            var jwt = _tokenHandler.ReadJwtToken(token);

            // Check if token has expired
            if (jwt.ValidTo <= DateTime.UtcNow)
            {
                await _tokenStorage.ClearAsync();
                return new AuthenticationState(Anonymous);
            }

            // Create authenticated identity with claims from JWT
            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
            var principal = new ClaimsPrincipal(identity);

            return new AuthenticationState(principal);
        }
        catch
        {
            // If token is malformed or parsing fails, clear it and return anonymous
            await _tokenStorage.ClearAsync();
            return new AuthenticationState(Anonymous);
        }
    }

    /// <summary>
    /// Notifies Blazor to re-evaluate authentication state.
    /// Call this after saving a new JWT to storage (e.g., after login).
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Signs out the current user by clearing token storage and notifying Blazor.
    /// Forces immediate UI update to unauthenticated state.
    /// </summary>
    public async Task SignOutAsync()
    {
        await _tokenStorage.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }
}