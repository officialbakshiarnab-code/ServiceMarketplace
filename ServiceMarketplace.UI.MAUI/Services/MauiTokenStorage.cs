using Microsoft.Maui.Storage;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.MAUI.Services;

// Purpose: MAUI token storage using SecureStorage.
public sealed class MauiTokenStorage : ITokenStorage
{
    private const string AccessTokenKey = "auth_token";
    private const string RefreshTokenKey = "refresh_token";

    public Task SaveTokenAsync(string token) => SecureStorage.SetAsync(AccessTokenKey, token);

    public Task<string?> GetTokenAsync() => SecureStorage.GetAsync(AccessTokenKey);

    public Task SaveRefreshTokenAsync(string refreshToken) => SecureStorage.SetAsync(RefreshTokenKey, refreshToken);

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.GetAsync(RefreshTokenKey);

    public Task ClearAsync()
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }
}
