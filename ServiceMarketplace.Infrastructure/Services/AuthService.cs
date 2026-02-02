using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ServiceMarketplace.Infrastructure.Services;

// Purpose: Authentication workflows with audit logging and JWT issuance.
public sealed class AuthService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    IAuditLogService auditLogService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthRegisterResult> RegisterAsync(string email, string password, string role)
    {
        var userExists = await userManager.FindByEmailAsync(email);
        if (userExists != null)
        {
            logger.LogWarning("Registration attempt for existing email: {Email}", email);
            return new AuthRegisterResult(false, "User already exists", null);
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(e => new AuthErrorDto { Code = e.Code, Description = e.Description })
                .ToList();

            logger.LogWarning("User creation failed for {Email}: {Errors}",
                email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new AuthRegisterResult(false, null, errors);
        }

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            logger.LogInformation("Created new role: {Role}", role);
        }

        await userManager.AddToRoleAsync(user, role);

        logger.LogInformation("User registered successfully: {Email} with role {Role}", email, role);

        return new AuthRegisterResult(true, null, null);
    }

    public async Task<AuthLoginResult> LoginAsync(string email, string password, string? userAgent, string? ipAddress)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            logger.LogWarning("Login attempt for non-existent user: {Email}", email);
            return new AuthLoginResult(false, null, "Invalid credentials");
        }

        var validPassword = await userManager.CheckPasswordAsync(user, password);
        if (!validPassword)
        {
            logger.LogWarning("Failed login attempt for user: {Email}", email);
            return new AuthLoginResult(false, null, "Invalid credentials");
        }

        var roles = await userManager.GetRolesAsync(user);
        var sessionId = Guid.NewGuid().ToString();
        var emailValue = user.Email ?? user.UserName ?? email;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, emailValue),
            new(JwtRegisteredClaimNames.Jti, sessionId),
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationTime = DateTime.UtcNow.AddMinutes(10);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        await auditLogService.LogLoginAsync(user.Id, roles.FirstOrDefault(), sessionId, ipAddress, userAgent);

        logger.LogInformation("User logged in successfully: {Email}, SessionId: {SessionId}", emailValue, sessionId);

        var payload = new AuthResultDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = token.ValidTo
        };

        return new AuthLoginResult(true, payload, null);
    }

    public async Task LogoutAsync(string userId, string sessionId, string? role, string? userAgent, string? ipAddress)
    {
        await auditLogService.LogLogoutAsync(userId, role, sessionId, ipAddress, userAgent);
    }

    public async Task HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
            return;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenValue);

            var userId = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                         ?? token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var sessionId = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var role = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return;

            await auditLogService.LogSessionExpiredAsync(userId, role, sessionId, ipAddress, userAgent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record token-expired audit event");
        }
    }
}
