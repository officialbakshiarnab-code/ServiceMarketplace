using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Application contract for authentication and audit workflows.
public interface IAuthService
{
    Task<AuthRegisterResult> RegisterAsync(string email, string password, string role);
    Task<AuthLoginResult> LoginAsync(string email, string password, string? userAgent, string? ipAddress);
    Task LogoutAsync(string userId, string sessionId, string? role, string? userAgent, string? ipAddress);
    Task HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress);
}
