using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ServiceMarketplace.UI.Shared.Auth;

// Purpose: Role-aware navigation to the correct dashboard after login.
public sealed class AuthRedirector(AuthenticationStateProvider authenticationStateProvider, NavigationManager navigationManager)
{
    private readonly AuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider;
    private readonly NavigationManager _navigationManager = navigationManager;

    public async Task RedirectToDashboardAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            _navigationManager.NavigateTo("/login");
            return;
        }

        if (user.IsInRole("User"))
        {
            _navigationManager.NavigateTo("/user/dashboard");
            return;
        }

        if (user.IsInRole("ServiceProvider"))
        {
            _navigationManager.NavigateTo("/provider/dashboard");
            return;
        }

        _navigationManager.NavigateTo("/");
    }
}