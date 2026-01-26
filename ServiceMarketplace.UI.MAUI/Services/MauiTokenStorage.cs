using Microsoft.Maui.Storage;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.MAUI.Services;

// Purpose: MAUI token storage using SecureStorage.
public sealed class MauiTokenStorage : ITokenStorage
{
    public Task SaveTokenAsync(string token) => SecureStorage.SetAsync("auth_token", token);

    public Task<string?> GetTokenAsync() => SecureStorage.GetAsync("auth_token");

    public Task ClearAsync()
    {
        SecureStorage.Remove("auth_token");
        return Task.CompletedTask;
    }
}
