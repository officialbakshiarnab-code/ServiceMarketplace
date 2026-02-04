using Microsoft.JSInterop;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Web.Services;

// Purpose: Web token storage using browser localStorage.
public sealed class LocalStorageTokenStorage : ITokenStorage
{
    private const string AccessTokenKey = "auth_token";
    private const string RefreshTokenKey = "refresh_token";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStorage(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask SaveTokenAsync(string token)
        => _js.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, token);

    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);

    public ValueTask SaveRefreshTokenAsync(string refreshToken)
        => _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);

    public async Task<string?> GetRefreshTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);

    public ValueTask ClearAsync()
    {
        // Clear both tokens
        _js.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        return _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
    }

    // Explicit interface implementations for Task-based methods
    Task ITokenStorage.SaveTokenAsync(string token) => SaveTokenAsync(token).AsTask();
    Task ITokenStorage.SaveRefreshTokenAsync(string refreshToken) => SaveRefreshTokenAsync(refreshToken).AsTask();
    Task ITokenStorage.ClearAsync() => ClearAsync().AsTask();
}
