using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ServiceMarketplace.UI.Shared.Auth;

// Purpose: Restores auth state from stored JWT and keeps UI auth state consistent.
public sealed class TokenAuthenticationStateProvider(ITokenStorage tokenStorage) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly ITokenStorage _tokenStorage = tokenStorage;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(Anonymous);

        try
        {
            var jwt = _tokenHandler.ReadJwtToken(token);

            if (jwt.ValidTo <= DateTime.UtcNow)
            {
                await _tokenStorage.ClearAsync();
                return new AuthenticationState(Anonymous);
            }

            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
            var principal = new ClaimsPrincipal(identity);

            return new AuthenticationState(principal);
        }
        catch
        {
            await _tokenStorage.ClearAsync();
            return new AuthenticationState(Anonymous);
        }
    }

    public void NotifyAuthStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}