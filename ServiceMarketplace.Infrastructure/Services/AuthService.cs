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
using System.Text;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class AuthService(
    IConfiguration configuration,
    IAuditLogService auditLogService,
    ITokenRefreshService tokenRefreshService,
    IFileUploadService fileUploadService,
    IPasswordHasher passwordHasher,
    AppDbContext dbContext,
    ILogger<AuthService> logger) : IAuthService
{
    private const int AccessTokenLifetimeMinutes = 10;

    public async Task<AuthRegisterResult> RegisterAsync(
        string email,
        string password,
        string role,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        string phoneNumber,
        RegistrationCommercialOnboardingDto? commercialOnboarding = null,
        object? governmentIdImage = null)
    {
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == null || !RoleConstants.IsPublicRegistrationRole(normalizedRole))
        {
            logger.LogWarning("[AuthService] Role is not allowed during public registration: {Role}", role);
            return new AuthRegisterResult(false, "Invalid role provided", null);
        }

        var userType = MapRoleToUserType(normalizedRole);
        if (userType == null)
            return new AuthRegisterResult(false, "Invalid user type provided", null);

        var ageValidation = AgeValidator.ValidateAge(dateOfBirth, userType.Value);
        if (!ageValidation.IsValid)
            return new AuthRegisterResult(false, ageValidation.Error, null);

        var normalizedEmail = NormalizeEmail(email);
        string normalizedPhone;
        try
        {
            normalizedPhone = NormalizeRequiredPhone(phoneNumber);
        }
        catch (ArgumentException ex)
        {
            return new AuthRegisterResult(false, ex.Message, null);
        }

        var existing = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (existing != null)
            return new AuthRegisterResult(true, null, null);

        var phoneExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.NormalizedPhoneNumber == normalizedPhone);

        if (phoneExists)
            return new AuthRegisterResult(false, "Phone number is already registered.", null);

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
                PasswordHash = passwordHasher.HashPassword(password),
                PhoneNumber = phoneNumber.Trim(),
                NormalizedPhoneNumber = normalizedPhone,
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
            await EnsureProviderProfileAsync(user, normalizedRole, commercialOnboarding);
            await EnsureSellerProfileAsync(user, commercialOnboarding);
            await auditLogService.LogRegistrationAsync(user.Id.ToString(), normalizedRole);
            if (tx != null)
                await tx.CommitAsync();

            return new AuthRegisterResult(true, null, null);
        }
        catch (Exception ex)
        {
            if (tx != null)
                await tx.RollbackAsync();

            if (ex is ArgumentException)
                return new AuthRegisterResult(false, ex.Message, null);

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

        if (!passwordHasher.VerifyPassword(password, user.PasswordHash))
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

        var sessionId = Guid.NewGuid().ToString("N");
        var roles = GetRoleNames(user).ToList();
        var primaryRole = roles.FirstOrDefault() ?? user.UserType.GetPrimaryRole();
        var expiresAt = DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes);
        var accessToken = await GenerateAccessTokenAsync(user, roles, sessionId, expiresAt);
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

        var roles = await dbContext.Roles
            .Where(r => roleNames.Contains(r.Name))
            .ToListAsync();

        foreach (var roleName in roleNames)
        {
            if (roles.Any(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase)))
                continue;

            var role = new Role
            {
                Name = roleName,
                Description = $"{roleName} account role",
                CreatedDate = DateTime.UtcNow
            };
            roles.Add(role);
            dbContext.Roles.Add(role);
        }

        var roleIds = roles.Select(r => r.Id).ToList();
        var assignedRoleIds = await dbContext.UserRoles
            .Where(ur => ur.UserId == user.Id && roleIds.Contains(ur.RoleId))
            .Select(ur => ur.RoleId)
            .ToListAsync();
        var assignedRoleIdSet = assignedRoleIds.ToHashSet();

        foreach (var role in roles)
        {
            if (!assignedRoleIdSet.Contains(role.Id))
                dbContext.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureProviderProfileAsync(
        User user,
        string requestedRole,
        RegistrationCommercialOnboardingDto? commercialOnboarding)
    {
        if (requestedRole is not RoleConstants.ServiceProvider and not RoleConstants.Both)
            return;

        var exists = await dbContext.ServiceProviderProfiles.AnyAsync(p => p.UserId == user.Id);
        if (exists)
            return;

        var provider = commercialOnboarding?.Provider;
        var business = commercialOnboarding?.Business;
        var submitForReview = commercialOnboarding?.WantsProvider == true && provider != null;
        var now = DateTime.UtcNow;
        var businessName = FirstNonEmpty(business?.TradingName, business?.LegalName);

        dbContext.ServiceProviderProfiles.Add(new ServiceProviderProfile
        {
            UserId = user.Id,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            BusinessName = businessName,
            Bio = BuildProviderBio(provider, business),
            Skills = submitForReview ? NormalizeRequired(provider!.Skills, 500, "Provider skills") : string.Empty,
            PrimaryCategory = submitForReview ? NormalizeRequired(provider!.PrimaryCategory, 150, "Provider service category") : string.Empty,
            ServiceAreaCity = submitForReview ? NormalizeRequired(provider!.ServiceAreaCity, 100, "Provider service city") : string.Empty,
            ServiceAreaState = submitForReview ? NormalizeRequired(provider!.ServiceAreaState, 100, "Provider service state") : string.Empty,
            ServiceAreaZone = NormalizeOptional(provider?.ServiceAreaZone, 100),
            HourlyRate = submitForReview ? EnsurePositiveRate(provider!.Rate) : 0,
            Status = submitForReview ? ProviderApplicationStatus.Submitted : ProviderApplicationStatus.Draft,
            IdentityVerificationSubmitted = submitForReview || !string.IsNullOrWhiteSpace(user.GovIdFilePath),
            AddressVerificationSubmitted = submitForReview,
            BackgroundCheckConsent = submitForReview,
            SubmittedAt = submitForReview ? now : null,
            CreatedAt = now,
            UpdatedAt = submitForReview ? now : null
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureSellerProfileAsync(User user, RegistrationCommercialOnboardingDto? commercialOnboarding)
    {
        if (commercialOnboarding?.WantsSeller != true)
            return;

        var seller = commercialOnboarding.Seller;
        if (seller == null)
            return;

        var exists = await dbContext.SellerProfiles.AnyAsync(s => s.UserId == user.Id);
        if (exists)
            return;

        var business = commercialOnboarding.Business;
        var now = DateTime.UtcNow;
        var businessName = FirstNonEmpty(seller.BusinessName, business?.TradingName, business?.LegalName);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            UserId = user.Id,
            StoreName = NormalizeRequired(seller.StoreName, 150, "Store name"),
            BusinessName = NormalizeOptional(businessName, 150),
            Description = BuildSellerDescription(seller, business),
            Gstin = NormalizeOptional(FirstNonEmpty(seller.Gstin, business?.Gstin), 30),
            PickupAddress = NormalizeRequired(seller.PickupAddress, 500, "Pickup address"),
            City = NormalizeRequired(seller.City, 100, "Seller city"),
            State = NormalizeRequired(seller.State, 100, "Seller state"),
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BusinessVerificationSubmitted = true,
            Status = SellerApplicationStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

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

    private static string BuildProviderBio(RegistrationProviderDetailsDto? provider, RegistrationBusinessDetailsDto? business)
    {
        if (provider == null && business == null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(provider?.ProviderType))
            parts.Add($"Provider type: {provider.ProviderType.Trim()}");
        if (!string.IsNullOrWhiteSpace(provider?.Profession))
            parts.Add($"Profession: {provider.Profession.Trim()}");
        if (provider?.YearsOfExperience is > 0)
            parts.Add($"Experience: {provider.YearsOfExperience} years");
        if (!string.IsNullOrWhiteSpace(provider?.PricingType))
            parts.Add($"Pricing: {provider.PricingType.Trim()}");
        if (!string.IsNullOrWhiteSpace(provider?.Availability))
            parts.Add($"Availability: {provider.Availability.Trim()}");
        if (!string.IsNullOrWhiteSpace(provider?.Languages))
            parts.Add($"Languages: {provider.Languages.Trim()}");
        if (!string.IsNullOrWhiteSpace(business?.WebsiteOrDomain))
            parts.Add($"Business domain: {business.WebsiteOrDomain.Trim()}");
        if (business?.RequestedSeatLimit is > 0)
            parts.Add($"Requested seats: {business.RequestedSeatLimit}");

        return string.Join(Environment.NewLine, parts);
    }

    private static string? BuildSellerDescription(RegistrationSellerDetailsDto seller, RegistrationBusinessDetailsDto? business)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(seller.Description))
            parts.Add(seller.Description.Trim());
        if (!string.IsNullOrWhiteSpace(seller.ProductCategories))
            parts.Add($"Product categories: {seller.ProductCategories.Trim()}");
        if (!string.IsNullOrWhiteSpace(seller.ProductConditionFocus))
            parts.Add($"Product condition focus: {seller.ProductConditionFocus.Trim()}");
        if (!string.IsNullOrWhiteSpace(business?.WebsiteOrDomain))
            parts.Add($"Business domain: {business.WebsiteOrDomain.Trim()}");
        if (!string.IsNullOrWhiteSpace(business?.OperatingAddress))
            parts.Add($"Operating address: {business.OperatingAddress.Trim()}");
        if (business?.RequestedSeatLimit is > 0)
            parts.Add($"Requested seats: {business.RequestedSeatLimit}");

        return parts.Count == 0 ? null : Truncate(string.Join(Environment.NewLine, parts), 1000);
    }

    private static decimal EnsurePositiveRate(decimal rate)
    {
        if (rate <= 0)
            throw new ArgumentException("Provider rate must be greater than zero.");

        return rate;
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"{fieldName} cannot exceed {maxLength} characters.");

        return value;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Truncate(value.Trim(), maxLength);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task<string> GenerateAccessTokenAsync(User user, IReadOnlyCollection<string> roles, string sessionId, DateTime expiresAt)
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

        foreach (var capability in await GetMarketplaceCapabilitiesAsync(user.Id, roles))
            claims.Add(new Claim(MarketplaceCapabilityConstants.ClaimType, capability));

        foreach (var permission in AdministrativePermissionConstants.FromRoles(roles))
            claims.Add(new Claim(AdministrativePermissionConstants.ClaimType, permission));

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

    private async Task<IReadOnlyList<string>> GetMarketplaceCapabilitiesAsync(Guid userId, IReadOnlyCollection<string> roles)
    {
        var capabilities = MarketplaceCapabilityConstants.FromRoles(roles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var approvedSeller = await dbContext.SellerProfiles
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Status == SellerApplicationStatus.Approved);

        if (approvedSeller)
            capabilities.Add(MarketplaceCapabilityConstants.ProductSeller);

        return capabilities.ToList();
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

    private static string NormalizeRequiredPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required.");

        var normalized = NormalizePhone(phone);
        if (normalized.Length is < 10 or > 15)
            throw new ArgumentException("Phone number must contain 10 to 15 digits.");

        return normalized;
    }

    private static string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

}
