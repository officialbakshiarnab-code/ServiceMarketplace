using Microsoft.JSInterop;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Web.Services;

// Purpose: Web token storage using browser localStorage.
public sealed class LocalStorageTokenStorage : ITokenStorage
{
    private const string TokenKey = "auth_token";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStorage(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask SaveTokenAsync(string token)
        => _js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public ValueTask ClearAsync()
        => _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);

    Task ITokenStorage.SaveTokenAsync(string token) => SaveTokenAsync(token).AsTask();
    Task ITokenStorage.ClearAsync() => ClearAsync().AsTask();
}
