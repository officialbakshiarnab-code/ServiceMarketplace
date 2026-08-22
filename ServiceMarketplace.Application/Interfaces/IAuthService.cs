using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Application contract for authentication and audit workflows.
/// 
/// Age Requirements:
/// - User role: No age restriction
/// - ServiceProvider role: Must be 18+ years old
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user with the specified role, validating age requirements.
    /// 
    /// Age Rules:
    /// - User role: No age restriction
    /// - ServiceProvider role: Must be 18+ years old (age >= 18)
    /// </summary>
    Task<AuthRegisterResult> RegisterAsync(
        string email, 
        string password, 
        string role,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        string phoneNumber,
        RegistrationCommercialOnboardingDto? commercialOnboarding = null,
        object? governmentIdImage = null);

    Task<AuthLoginResult> LoginAsync(string email, string password, string? userAgent, string? ipAddress);
    Task<AuthLoginResult> LoginWithIdentifierAsync(string identifier, string password, string? userAgent, string? ipAddress);
    Task LogoutAsync(string userId, string sessionId, string? role, string? userAgent, string? ipAddress);
    Task HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress);
    
    /// <summary>
    /// Refreshes an access token using a valid refresh token.
    /// Creates new access and refresh tokens (with rotation).
    /// Idempotent for same refresh token within 30 seconds.
    /// </summary>
    Task<RefreshTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        string? userAgent,
        string? ipAddress);
}
