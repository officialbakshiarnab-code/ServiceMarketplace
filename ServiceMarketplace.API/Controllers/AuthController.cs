using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Models.Auth;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ServiceMarketplace.API.Controllers;

/// <summary>
/// Authentication controller with comprehensive audit trail.
/// Handles user registration, login with JWT issuance, and logout with session tracking.
/// All authentication events are logged to LoginAuditLog for security monitoring.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        AppDbContext context,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user with the specified role.
    /// Creates user account in ASP.NET Identity and assigns role.
    /// </summary>
    /// <param name="request">Registration details (email, password, role)</param>
    /// <returns>Success message or validation errors</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Check if user already exists
        var userExists = await _userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
        {
            _logger.LogWarning("Registration attempt for existing email: {Email}", request.Email);
            return BadRequest("User already exists");
        }

        // Create new user
        var user = new IdentityUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false // Can be set to true for development
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("User creation failed for {Email}: {Errors}", 
                request.Email, 
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(result.Errors);
        }

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole(request.Role));
            _logger.LogInformation("Created new role: {Role}", request.Role);
        }

        // Assign role to user
        await _userManager.AddToRoleAsync(user, request.Role);

        _logger.LogInformation("User registered successfully: {Email} with role {Role}", 
            request.Email, request.Role);

        return Ok("User registered successfully");
    }

    /// <summary>
    /// Authenticates user and issues JWT token with session tracking.
    /// Creates audit log entry with login timestamp and client metadata.
    /// </summary>
    /// <param name="request">Login credentials (email, password)</param>
    /// <returns>JWT token and expiration time</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return Unauthorized("Invalid credentials");
        }

        // Verify password
        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
            return Unauthorized("Invalid credentials");
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate unique session ID for audit tracking
        var sessionId = Guid.NewGuid().ToString();

        // Build JWT claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, sessionId), // Session ID for tracking
            new(JwtRegisteredClaimNames.Sub, user.Id),   // Standard subject claim
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add role claims
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Generate JWT token
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationTime = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        // Record login audit entry
        var auditEntry = new LoginAuditLog
        {
            UserId = user.Id,
            SessionId = sessionId,
            LoginTime = DateTime.UtcNow,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            Platform = Request.Headers["X-Platform"].FirstOrDefault() ?? "Unknown"
        };

        _context.LoginAuditLogs.Add(auditEntry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User logged in successfully: {Email}, SessionId: {SessionId}", 
            user.Email, sessionId);

        return Ok(new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = token.ValidTo
        });
    }

    /// <summary>
    /// Logs out the current user by recording logout timestamp.
    /// Client is responsible for removing JWT from local storage.
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("Logout attempted with invalid token claims");
            return BadRequest("Invalid token claims");
        }

        // Find and update audit entry with logout time
        var auditEntry = await _context.LoginAuditLogs
            .FirstOrDefaultAsync(log => log.SessionId == sessionId && log.UserId == userId);

        if (auditEntry != null)
        {
            auditEntry.LogoutTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User logged out: UserId={UserId}, SessionId={SessionId}", 
                userId, sessionId);
        }
        else
        {
            _logger.LogWarning("Logout audit entry not found: UserId={UserId}, SessionId={SessionId}", 
                userId, sessionId);
        }

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Retrieves the client IP address, checking for proxy headers.
    /// </summary>
    /// <returns>Client IP address or null if unavailable</returns>
    private string? GetClientIpAddress()
    {
        // Check for proxy headers first
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Take the first IP if multiple are present
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fallback to direct connection IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}


