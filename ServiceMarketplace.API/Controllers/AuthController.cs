using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.API.Models.Auth;
using ServiceMarketplace.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

/// <summary>
/// Authentication controller with comprehensive audit trail.
/// Handles user registration, login with JWT issuance, and logout with session tracking.
/// All authentication events are logged to AuditLog for security monitoring.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Registers a new user with the specified role.
    /// Creates user account in ASP.NET Identity and assigns role.
    /// </summary>
    /// <param name="request">Registration details (email, password, role)</param>
    /// <returns>Success message or validation errors</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, request.Role);
        if (!result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(result.Error))
                return BadRequest(result.Error);

            if (result.Errors is { Count: > 0 })
                return BadRequest(result.Errors);

            return BadRequest("Registration failed");
        }

        return Ok("User registered successfully");
    }

    /// <summary>
    /// Authenticates user and issues JWT token with session tracking.
    /// Creates audit log entry with login timestamp and client metadata.
    /// </summary>
    /// <param name="request">Login credentials (email, password)</param>
    /// <returns>JWT token and expiration time</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, Request.Headers.UserAgent.ToString(), GetClientIpAddress());
        if (!result.Succeeded || result.Payload == null)
            return Unauthorized(result.Error ?? "Invalid credentials");

        return Ok(new AuthResponse
        {
            Token = result.Payload.Token,
            ExpiresAt = result.Payload.ExpiresAt
        });
    }

    /// <summary>
    /// Logs out the current user by creating a new audit log record.
    /// 
    /// CRITICAL BEHAVIOR:
    /// - Requires valid JWT authentication ([Authorize] attribute)
    /// - Extracts UserId, SessionId, and Role from JWT claims
    /// - Creates a NEW AuditLogs row with:
    ///   * EventType = "Logout"
    ///   * TimestampUtc = DateTime.UtcNow
    ///   * UserId, Role, SessionId from JWT
    ///   * IpAddress and UserAgent from request
    /// - NEVER updates existing rows (append-only audit trail)
    /// - Client is responsible for removing JWT from local storage after this call
    /// 
    /// AUDIT TRAIL:
    /// Each logout generates exactly ONE new row in AuditLogs table.
    /// This ensures complete audit trail for compliance and security monitoring.
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var sessionId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
            return Unauthorized(new { error = "Invalid token claims" });

        // Creates a NEW audit log row with EventType="Logout" (append-only, never updates)
        await authService.LogoutAsync(userId, sessionId, role, Request.Headers.UserAgent.ToString(), GetClientIpAddress());

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Notifies the backend that a JWT token has expired on the client.
    /// Creates a SessionExpired audit log entry.
    /// Called by TokenAuthenticationStateProvider when the 10-minute session timer fires.
    /// 
    /// Endpoint behavior:
    /// - Accepts expired token (no [Authorize] attribute required)
    /// - Extracts userId, sessionId, and role from token claims
    /// - Creates exactly one AuditLog entry with EventType="SessionExpired"
    /// - Uses SessionId to prevent duplicate entries for the same session
    /// 
    /// Why not [Authorize]?
    /// The token is already expired on the client, so JWT validation would fail.
    /// We manually parse the token to extract claims for audit purposes.
    /// </summary>
    /// <param name="request">Contains the expired JWT token</param>
    /// <returns>Success message or error</returns>
    [HttpPost("token-expired")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TokenExpired(TokenExpiredRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required");

        await authService.HandleTokenExpiredAsync(request.Token, Request.Headers.UserAgent.ToString(), GetClientIpAddress());
        return Ok(new { message = "Token expiry recorded" });
    }

    /// <summary>
    /// Retrieves the client IP address, checking for proxy headers.
    /// </summary>
    /// <returns>Client IP address or null if unavailable</returns>
    private string? GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Request payload for token-expired endpoint.
/// Contains the expired token for audit log extraction.
/// </summary>
public class TokenExpiredRequest
{
    public string Token { get; set; } = string.Empty;
}



