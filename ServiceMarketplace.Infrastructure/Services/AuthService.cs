using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Application.Validators;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Data.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class AuthService(
    IConfiguration configuration,
    IAuditLogService auditLogService,
    ITokenRefreshService tokenRefreshService,
    IFileUploadService fileUploadService,
    AppDbContext dbContext,
    ILogger<AuthService> logger) : IAuthService
{
    private const int PasswordHashIterations = 100_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int AccessTokenLifetimeMinutes = 10;

    public async Task<AuthRegisterResult> RegisterAsync(
        string email,
        string password,
        string role,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        object? governmentIdImage = null)
    {
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == null)
        {
            logger.LogWarning("[AuthService] Invalid role provided during registration: {Role}", role);
            return new AuthRegisterResult(false, "Invalid role provided", null);
        }

        var userType = MapRoleToUserType(normalizedRole);
        if (userType == null)
            return new AuthRegisterResult(false, "Invalid user type provided", null);

        var ageValidation = AgeValidator.ValidateAge(dateOfBirth, userType.Value);
        if (!ageValidation.IsValid)
            return new AuthRegisterResult(false, ageValidation.Error, null);

        var normalizedEmail = NormalizeEmail(email);
        var existing = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (existing != null)
            return new AuthRegisterResult(true, null, null);

        IDbContextTransaction? tx = null;
        if (dbContext.Database.IsRelational())
            tx = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                DateOfBirth = DateTime.SpecifyKind(dateOfBirth.Date, DateTimeKind.Utc),
                PasswordHash = HashPassword(password),
                PhoneNumber = null,
                NormalizedPhoneNumber = null,
                SecondaryPhoneNumber = null,
                UserType = userType.Value,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                IsKycSubmitted = false,
                IsKycApproved = false
            };

            if (governmentIdImage != null)
            {
                var uploaded = await fileUploadService.UploadGovernmentIdAsync(governmentIdImage, user.Id.ToString());
                if (!string.IsNullOrWhiteSpace(uploaded))
                {
                    user.GovIdFilePath = uploaded;
                    user.IsKycSubmitted = true;
                }
            }

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            await EnsureUserRolesAsync(user, normalizedRole);
            await auditLogService.LogRegistrationAsync(user.Id.ToString(), normalizedRole);
            if (tx != null)
                await tx.CommitAsync();

            return new AuthRegisterResult(true, null, null);
        }
        catch (Exception ex)
        {
            if (tx != null)
                await tx.RollbackAsync();

            logger.LogError(ex, "[AuthService] Registration failed for {Email}", email);
            return new AuthRegisterResult(false, "Registration failed due to a system error.", null);
        }
        finally
        {
            if (tx != null)
                await tx.DisposeAsync();
        }
    }

    public Task<AuthLoginResult> LoginAsync(string email, string password, string? userAgent, string? ipAddress)
    {
        return LoginWithIdentifierAsync(email, password, userAgent, ipAddress);
    }

    public async Task<AuthLoginResult> LoginWithIdentifierAsync(string identifier, string password, string? userAgent, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            return new AuthLoginResult(false, null, "Invalid credentials");

        var user = await FindUserByIdentifierAsync(identifier);
        if (user == null)
            return new AuthLoginResult(false, null, "Invalid credentials");

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
            return new AuthLoginResult(false, null, "Account is locked. Try again later.");

        if (!VerifyPassword(password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);

            await dbContext.SaveChangesAsync();
            return new AuthLoginResult(false, null, "Invalid credentials");
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        await dbContext.SaveChangesAsync();

        if (!user.IsActive)
            return new AuthLoginResult(false, null, "Account is not active.");

        if (user.UserType == UserType.Provider && user.IsKycSubmitted && !user.IsKycApproved)
            return new AuthLoginResult(false, null, "KYC approval pending.");

        var sessionId = Guid.NewGuid().ToString("N");
        var roles = GetRoleNames(user).ToList();
        var primaryRole = roles.FirstOrDefault() ?? user.UserType.GetPrimaryRole();
        var expiresAt = DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes);
        var accessToken = GenerateAccessToken(user, roles, sessionId, expiresAt);
        var refreshToken = await tokenRefreshService.IssueRefreshTokenAsync(
            user.Id.ToString(),
            sessionId,
            ipAddress,
            userAgent);

        await auditLogService.LogLoginAsync(user.Id.ToString(), primaryRole, sessionId, ipAddress, userAgent);

        return new AuthLoginResult(true, new AuthResultDto
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        }, null);
    }

    public async Task LogoutAsync(string userId, string sessionId, string? role, string? userAgent, string? ipAddress)
    {
        await auditLogService.LogLogoutAsync(userId, role, sessionId, ipAddress, userAgent);
        await tokenRefreshService.RevokeAllTokensForUserAsync(userId, "Logout");
    }

    public async Task HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(tokenValue)) return;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenValue);

            var userId = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                         ?? token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var sessionId = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var role = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            if (string.IsNullOrWhiteSpace(userId)) return;

            await auditLogService.LogSessionExpiredAsync(userId, role, sessionId, ipAddress, userAgent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AuthService] Failed to record token-expired audit event");
        }
    }

    public Task<RefreshTokenResponse> RefreshAccessTokenAsync(string refreshToken, string? userAgent, string? ipAddress)
    {
        return tokenRefreshService.RefreshAccessTokenAsync(refreshToken, ipAddress, userAgent);
    }

    private async Task<User?> FindUserByIdentifierAsync(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@'))
        {
            var normalizedEmail = NormalizeEmail(trimmed);
            return await dbContext.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        }

        var normalizedPhone = NormalizePhone(trimmed);
        return await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedPhoneNumber == normalizedPhone);
    }

    private async Task EnsureUserRolesAsync(User user, string requestedRole)
    {
        var roleNames = requestedRole == RoleConstants.Both
            ? new[] { RoleConstants.User, RoleConstants.ServiceProvider }
            : new[] { requestedRole };

        foreach (var roleName in roleNames)
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                role = new Role
                {
                    Name = roleName,
                    Description = $"{roleName} account role",
                    CreatedDate = DateTime.UtcNow
                };
                dbContext.Roles.Add(role);
                await dbContext.SaveChangesAsync();
            }

            var exists = await dbContext.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);
            if (!exists)
                dbContext.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await dbContext.SaveChangesAsync();
    }

    private static IEnumerable<string> GetRoleNames(User user)
    {
        var assignedRoles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (assignedRoles.Count > 0)
            return assignedRoles;

        return new[] { user.UserType.GetPrimaryRole() };
    }

    private string GenerateAccessToken(User user, IReadOnlyCollection<string> roles, string sessionId, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, sessionId),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("UserType", ((short)user.UserType).ToString())
        };

        var primaryRole = roles.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(primaryRole))
            claims.Add(new Claim("role", primaryRole));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserType? MapRoleToUserType(string role)
    {
        return role switch
        {
            var r when string.Equals(r, RoleConstants.User, StringComparison.OrdinalIgnoreCase) => UserType.Customer,
            var r when string.Equals(r, RoleConstants.ServiceProvider, StringComparison.OrdinalIgnoreCase) => UserType.Provider,
            var r when string.Equals(r, RoleConstants.Both, StringComparison.OrdinalIgnoreCase) => UserType.Provider,
            var r when string.Equals(r, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase) => UserType.Admin,
            _ => null
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return $"v1${PasswordHashIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
