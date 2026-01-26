using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace ServiceMarketplace.UI.Shared.Auth;

public sealed class AuthState
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthState(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true;
    }

    public async Task<string?> GetRoleAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Role)?.Value;
    }
}
