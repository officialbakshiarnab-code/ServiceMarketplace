using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace ServiceMarketplace.API.Tests.Helpers;

/// <summary>
/// Helper methods for parsing and validating JWT tokens in tests.
/// </summary>
public static class JwtTestHelper
{
    /// <summary>
    /// Extracts and parses a JWT token from the response.
    /// </summary>
    public static string? ExtractToken(HttpContent content)
    {
        var json = content.ReadAsStringAsync().Result;
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("token", out var token))
            return token.GetString();

        if (doc.RootElement.TryGetProperty("accessToken", out var accessToken))
            return accessToken.GetString();

        return null;
    }

    /// <summary>
    /// Extracts refresh token from response.
    /// </summary>
    public static string? ExtractRefreshToken(HttpContent content)
    {
        var json = content.ReadAsStringAsync().Result;
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("refreshToken", out var token))
            return token.GetString();

        return null;
    }

    /// <summary>
    /// Parses JWT and returns all claims as dictionary.
    /// </summary>
    public static Dictionary<string, string> GetClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        return jwtToken.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value);
    }

    /// <summary>
    /// Gets all claim values for a specific claim type.
    /// </summary>
    public static IReadOnlyList<string> GetClaimValues(string token, string claimType)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        return jwtToken.Claims
            .Where(c => c.Type == claimType)
            .Select(c => c.Value)
            .ToList();
    }

    /// <summary>
    /// Gets a specific claim from JWT.
    /// </summary>
    public static string? GetClaim(string token, string claimType)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    /// <summary>
    /// Validates token expiration.
    /// </summary>
    public static bool IsTokenExpired(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.ValidTo < DateTime.UtcNow;
    }

    /// <summary>
    /// Gets token expiration time.
    /// </summary>
    public static DateTime? GetTokenExpiration(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.ValidTo;
    }
}
