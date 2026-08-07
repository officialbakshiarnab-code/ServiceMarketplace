using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.API.Models.Auth;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Application.Validators;
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
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Registers a new user with the specified role.
    /// Creates a custom auth user and assigns marketplace roles.
    /// 
    /// Validates role against RoleConstants.AllRoles.
    /// Validates age requirements based on role.
    /// 
    /// Age Requirements:
    /// - User role: No age restriction
    /// - ServiceProvider role: Must be 18+ years old
    /// - Both role: Must be 18+ years old
    /// 
    /// RETRY-SAFE (Idempotent):
    /// - Same registration request (email + password + role) submitted multiple times
    /// - First request: Creates user, returns 200 OK
    /// - Subsequent requests with same email+role: Returns 200 OK (idempotent)
    /// - Subsequent requests with same email but different role: Returns 400 Bad Request
    /// - Network retry-safe: Client can safely retry without duplicate user creation
    /// 
    /// Rate Limited: 5 requests per minute per IP address.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <returns>Success message or validation errors</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [Consumes("application/json")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (request == null)
            {
                logger.LogWarning("[AuthController] Register: Request is null");
                return BadRequest(new { error = "Invalid request" });
            }

            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                logger.LogWarning("[AuthController] Register: Email is empty");
                return BadRequest(new { error = "Email is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                logger.LogWarning("[AuthController] Register: Password is empty");
                return BadRequest(new { error = "Password is required" });
            }

            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                logger.LogWarning("[AuthController] Register: First name is empty");
                return BadRequest(new { error = "First name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.LastName))
            {
                logger.LogWarning("[AuthController] Register: Last name is empty");
                return BadRequest(new { error = "Last name is required" });
            }

            if (!RoleConstants.IsPublicRegistrationRole(request.Role))
            {
                logger.LogWarning("[AuthController] Register: Role is not allowed for public registration: {Role}. Public roles: {ValidRoles}",
                    request.Role, string.Join(", ", RoleConstants.PublicRegistrationRoles));
                return BadRequest(new { error = $"Invalid role. Valid roles are: {string.Join(", ", RoleConstants.PublicRegistrationRoles)}" });
            }

            var ageValidation = AgeValidator.ValidateAge(request.DateOfBirth, request.Role);
            if (!ageValidation.IsValid)
            {
                logger.LogWarning("[AuthController] Register: Age validation failed for {Email}, Role: {Role}, Error: {Error}",
                    request.Email, request.Role, ageValidation.Error);
                return BadRequest(new { error = ageValidation.Error ?? "Age validation failed" });
            }

            logger.LogInformation("[AuthController] Register: Attempting to register user {Email} with role {Role}, Age validation passed",
                request.Email, request.Role);

            var result = await authService.RegisterAsync(
                request.Email, 
                request.Password, 
                request.Role,
                request.FirstName,
                request.LastName,
                request.DateOfBirth,
                request.GovernmentIdImage);
            
            if (!result.Succeeded)
            {
                // Format error message
                var errorMessage = !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error
                    : result.Errors is { Count: > 0 }
                        ? string.Join("; ", result.Errors.Select(e => e.Description ?? e.Code))
                        : "Registration failed";

                logger.LogWarning("[AuthController] Register: Registration failed for {Email}: {Error}",
                    request.Email, errorMessage);

                return BadRequest(new { error = errorMessage });
            }

            logger.LogInformation("[AuthController] Register: User {Email} registered successfully with role {Role}",
                request.Email, request.Role);

            return Ok(new { message = "User registered successfully" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[AuthController] Register: Invalid operation for user {Email}", request?.Email);
            return BadRequest(new { error = "Registration cannot be completed at this time" });
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "[AuthController] Register: Invalid argument for user {Email}", request?.Email);
            return BadRequest(new { error = "Invalid registration data provided" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthController] Register: Unexpected error during registration for {Email}", request?.Email);
            return BadRequest(new { error = "An error occurred during registration. Please try again later." });
        }
    }

    /// <summary>
    /// Authenticates user and issues JWT token with session tracking.
    /// Creates audit log entry with login timestamp and client metadata.
    /// Validates that JWT contains correct role claims.
    /// 
    /// RETRY-SAFE (Idempotent):
    /// - Every call generates a NEW JWT with a unique SessionId (jti claim)
    /// - Same email+password submitted multiple times creates multiple audit entries
    /// - Each login creates a new SessionId, each with 10-minute expiration
    /// - SAFE TO RETRY: No state mutation, only token generation
    /// - Network retry-safe: Client can safely retry on network failures
    /// - Each retry gets a fresh JWT with new expiration time
    /// 
    /// Rate Limited: 5 requests per minute per IP address.
    /// </summary>
    /// <param name="request">Login credentials (email, password)</param>
    /// <returns>JWT token and expiration time</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            if (request == null)
            {
                logger.LogWarning("[AuthController] Login: Request is null");
                return BadRequest(new { error = "Invalid request" });
            }

            if (string.IsNullOrWhiteSpace(request.Identifier))
            {
                logger.LogWarning("[AuthController] Login: Identifier is empty");
                return BadRequest(new { error = "Identifier is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                logger.LogWarning("[AuthController] Login: Password is empty");
                return BadRequest(new { error = "Password is required" });
            }

            logger.LogInformation("[AuthController] Login: Attempting to login identifier {Identifier}", request.Identifier);

            var result = await authService.LoginWithIdentifierAsync(request.Identifier, request.Password,
                Request.Headers.UserAgent.ToString(), GetClientIpAddress());

            if (!result.Succeeded)
            {
                logger.LogWarning("[AuthController] Login: Failed for identifier {Identifier}: {Error}",
                    request.Identifier, result.Error ?? "Unknown error");
                // Return 401 Unauthorized for authentication failures (idempotent - same response on retry)
                return Unauthorized(new { error = result.Error ?? "Invalid credentials" });
            }

            if (result.Payload == null)
            {
                logger.LogError("[AuthController] Login: Succeeded but payload is null for identifier {Identifier}", request.Identifier);
                return BadRequest(new { error = "Failed to generate authentication token" });
            }

                logger.LogInformation("[AuthController] Login: Identifier {Identifier} logged in successfully",
                request.Identifier);

            // Return 200 OK with new JWT (different on each retry - new SessionId)
            // This is safe because each JWT is independent and idempotent
            return Ok(new AuthResponse
            {
                Token = result.Payload.Token,
                ExpiresAt = result.Payload.ExpiresAt,
                RefreshToken = result.Payload.RefreshToken,
                RefreshTokenExpiresAt = result.Payload.RefreshTokenExpiresAt
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[AuthController] Login: Service error for identifier {Identifier}", request?.Identifier);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Login service is temporarily unavailable. Please try again later." });
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "[AuthController] Login: Invalid argument for identifier {Identifier}", request?.Identifier);
            return BadRequest(new { error = "Invalid login credentials provided" });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "[AuthController] Login: Unauthorized access attempt for identifier {Identifier}", request?.Identifier);
            return Unauthorized(new { error = "Invalid credentials" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthController] Login: Unexpected error during login for {Identifier}", request?.Identifier);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An unexpected error occurred during login. Please try again later." });
        }
    }

    /// <summary>
    /// Logs out the current user by creating a new audit log record.
    /// 
    /// CRITICAL BEHAVIOR:
    /// - Requires valid JWT authentication ([Authorize] attribute)
    /// - Extracts UserId, SessionId, and Role from JWT claims
    /// - Validates role claim is one of RoleConstants.AllRoles
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
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var sessionId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
            {
                logger.LogWarning("[AuthController] Logout: Invalid token claims. UserId: {UserId}, SessionId: {SessionId}",
                    userId ?? "(missing)", sessionId ?? "(missing)");
                return Unauthorized(new { error = "Invalid token claims" });
            }

            // Validate role if present
            if (!string.IsNullOrWhiteSpace(role) && !RoleConstants.IsValidRole(role))
            {
                logger.LogWarning("[AuthController] Logout: User {UserId} has invalid role in JWT: {Role}",
                    userId, role);
            }

            logger.LogInformation("[AuthController] Logout: User {UserId} logging out (role: {Role})",
                userId, role ?? "(unknown)");

            // Creates a NEW audit log row with EventType="Logout" (append-only, never updates)
            await authService.LogoutAsync(userId, sessionId, role, Request.Headers.UserAgent.ToString(), GetClientIpAddress());

            return Ok(new { message = "Logged out successfully" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[AuthController] Logout: Invalid operation");
            return BadRequest(new { error = "Logout cannot be completed at this time" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthController] Logout: Unexpected error during logout");
            return BadRequest(new { error = "An error occurred during logout. Please try again later." });
        }
    }

    /// <summary>
    /// Notifies the backend that a JWT token has expired on the client.
    /// Creates a SessionExpired audit log entry.
    /// Called by TokenAuthenticationStateProvider when the 10-minute session timer fires.
    /// 
    /// Endpoint behavior:
    /// - Accepts expired token (no [Authorize] attribute required)
    /// - Extracts userId, sessionId, and role from token claims
    /// - Validates role claim is one of RoleConstants.AllRoles
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
        try
        {
            if (request == null)
            {
                logger.LogWarning("[AuthController] TokenExpired: Request is null");
                return BadRequest(new { error = "Invalid request" });
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                logger.LogWarning("[AuthController] TokenExpired: No token provided");
                return BadRequest(new { error = "Token is required" });
            }

            logger.LogInformation("[AuthController] TokenExpired: Processing expired token notification");

            await authService.HandleTokenExpiredAsync(request.Token, Request.Headers.UserAgent.ToString(), GetClientIpAddress());
            return Ok(new { message = "Token expiry recorded" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "[AuthController] TokenExpired: Invalid operation");
            return BadRequest(new { error = "Unable to process token expiry notification" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AuthController] TokenExpired: Unexpected error processing token expiry");
            return BadRequest(new { error = "An error occurred processing token expiry. This is non-critical." });
        }
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// 
    /// IDEMPOTENCY GUARANTEE:
    /// - Same refresh token submitted multiple times within 30 seconds
    ///   returns identical new tokens (deduplicated on backend)
    /// - Safe for automatic retries from UI
    /// - Prevents duplicate token families
    /// 
    /// TOKEN ROTATION:
    /// - Old refresh token immediately revoked (marked inactive)
    /// - New refresh token issued (rotated for security)
    /// - Client MUST store the new refresh token
    /// - Using old token after rotation indicates theft (will revoke entire family)
    /// 
    /// RATE LIMITING:
    /// - 10 refresh requests per minute per IP address
    /// - Prevents token refresh abuse
    /// 
    /// USAGE:
    /// POST /api/auth/refresh
    /// { "refreshToken": "..." }
    /// 
    /// Response:
    /// {
    ///   "accessToken": "new JWT (10 min)",
    ///   "accessTokenExpiresAt": "2025-02-01T...",
    ///   "refreshToken": "new token (7 days, rotated)",
    ///   "refreshTokenExpiresAt": "2025-02-08T..."
    /// }
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        try
        {
            // Validate input
            if (request == null)
            {
                logger.LogWarning("[AuthController] Refresh: Request is null");
                return BadRequest(new { error = "Invalid request" });
            }

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                logger.LogWarning("[AuthController] Refresh: No refresh token provided");
                return BadRequest(new { error = "Refresh token is required" });
            }

            logger.LogInformation("[AuthController] Refresh: Attempting to refresh access token");

            var result = await authService.RefreshAccessTokenAsync(
                request.RefreshToken,
                Request.Headers.UserAgent.ToString(),
                GetClientIpAddress());

            logger.LogInformation("[AuthController] Refresh: Successfully refreshed access token");

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("[AuthController] Refresh: Invalid refresh token - {Error}", ex.Message);
            return Unauthorized(new { error = "Refresh token is invalid, expired, or revoked" });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("[AuthController] Refresh: Invalid argument - {Error}", ex.Message);
            return BadRequest(new { error = "Invalid refresh request" });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("[AuthController] Refresh: Unauthorized - {Error}", ex.Message);
            return Unauthorized(new { error = "Refresh token is invalid or expired" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthController] Refresh: Unexpected error during token refresh");
            return BadRequest(new { error = "An error occurred during token refresh. Please try again later." });
        }
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

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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






