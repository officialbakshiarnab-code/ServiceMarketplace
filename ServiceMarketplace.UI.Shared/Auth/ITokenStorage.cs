namespace ServiceMarketplace.UI.Shared.Auth;

public interface ITokenStorage
{
    /// <summary>
    /// Saves the access token (JWT, short-lived, 10 minutes).
    /// </summary>
    Task SaveTokenAsync(string token);

    /// <summary>
    /// Retrieves the stored access token.
    /// </summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Saves the refresh token (long-lived, 7 days, rotated on each use).
    /// </summary>
    Task SaveRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Retrieves the stored refresh token.
    /// </summary>
    Task<string?> GetRefreshTokenAsync();

    /// <summary>
    /// Clears both access token and refresh token.
    /// Called on logout or when tokens become invalid.
    /// </summary>
    Task ClearAsync();
}
