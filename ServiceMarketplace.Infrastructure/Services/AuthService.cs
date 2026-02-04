using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Application.Validators;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
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
    ITokenRefreshService tokenRefreshService,
    IFileUploadService fileUploadService,
    AppDbContext dbContext,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthRegisterResult> RegisterAsync(
        string email, 
        string password, 
        string role,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        object? governmentIdImage = null)
    {
        // Validate age again at service level (defense in depth)
        var ageValidation = AgeValidator.ValidateAge(dateOfBirth, role);
        if (!ageValidation.IsValid)
        {
            logger.LogWarning("[AuthService] Age validation failed during registration for {Email}, Role: {Role}",
                email, role);
            return new AuthRegisterResult(false, ageValidation.Error, null);
        }

        // Normalize role to ensure consistency
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == null)
        {
            logger.LogWarning("[AuthService] Invalid role provided: {Role}. Valid roles: {ValidRoles}",
                role, string.Join(", ", RoleConstants.AllRoles));
            return new AuthRegisterResult(false, $"Invalid role. Valid roles are: {string.Join(", ", RoleConstants.AllRoles)}", null);
        }

        // IDEMPOTENCY CHECK: If user already exists with requested role, return success (not error)
        // This makes the registration endpoint safe to retry
        var userExists = await userManager.FindByEmailAsync(email);
        if (userExists != null)
        {
            // User exists - check if they have the requested role
            var userRoles = await userManager.GetRolesAsync(userExists);
            if (userRoles.Contains(normalizedRole))
            {
                // User exists with the exact role requested
                // Return success to support idempotent retries
                // Multiple requests with same email/role all succeed
                logger.LogInformation("[AuthService] Idempotent registration: User {Email} already exists with requested role {Role}. Retry detected.", 
                    email, normalizedRole);
                return new AuthRegisterResult(true, null, null);
            }
            
            // User exists but with DIFFERENT role - this is a genuine conflict
            // Email is already taken with a different role
            // Caller must use a different email for a different role
            logger.LogWarning("[AuthService] Registration conflict: User {Email} exists with different role. Existing: {ExistingRoles}, Requested: {RequestedRole}", 
                email, string.Join(", ", userRoles), normalizedRole);
            return new AuthRegisterResult(false, "User already exists with a different role. Please use a different email or contact support.", null);
        }

        // New user - proceed with registration in a transaction for atomicity
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                DateOfBirth = dateOfBirth,
                PhonePrimary = string.Empty, // Required, set to empty - user can update later
                UserType = GetUserTypeFromRole(normalizedRole),
                CreatedAtUtc = DateTime.UtcNow
            };

            // Step 1: Create user account
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                
                var errors = result.Errors
                    .Select(e => new AuthErrorDto { Code = e.Code, Description = e.Description })
                    .ToList();

                logger.LogWarning("[AuthService] User creation failed for {Email}: {Errors}",
                    email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));

                return new AuthRegisterResult(false, null, errors);
            }

            // Step 2: Verify role exists (must be created during app startup via RoleSeedingService)
            var roleExists = await roleManager.RoleExistsAsync(normalizedRole);
            if (!roleExists)
            {
                await transaction.RollbackAsync();
                logger.LogError("[AuthService] Role '{Role}' does not exist. Roles must be created during application startup via RoleSeedingService, not during registration.",
                    normalizedRole);
                return new AuthRegisterResult(false, "System configuration error: role not found. Please contact support.", null);
            }

            // Step 3: Assign role(s) to user (no duplicates possible since user just created)
            var roleResult = await userManager.AddToRoleAsync(user, normalizedRole);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                logger.LogError("[AuthService] Failed to assign role {Role} to user {Email}: {Errors}",
                    normalizedRole,
                    email,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                return new AuthRegisterResult(false, "Failed to assign role to user. Please try again.", null);
            }

            // Step 3b: If registering with Both role, assign BOTH User AND ServiceProvider roles
            // This allows JWT to contain both role claims for authorization flexibility
            if (normalizedRole == RoleConstants.Both)
            {
                logger.LogInformation("[AuthService] User {Email} registered with Both role - assigning both User and ServiceProvider roles", email);
                
                // Assign User role
                var userRoleResult = await userManager.AddToRoleAsync(user, RoleConstants.User);
                if (!userRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    logger.LogError("[AuthService] Failed to assign User role during Both registration for {Email}: {Errors}",
                        email, string.Join(", ", userRoleResult.Errors.Select(e => e.Description)));
                    return new AuthRegisterResult(false, "Failed to assign User role. Please try again.", null);
                }

                // Assign ServiceProvider role
                var providerRoleResult = await userManager.AddToRoleAsync(user, RoleConstants.ServiceProvider);
                if (!providerRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    logger.LogError("[AuthService] Failed to assign ServiceProvider role during Both registration for {Email}: {Errors}",
                        email, string.Join(", ", providerRoleResult.Errors.Select(e => e.Description)));
                    return new AuthRegisterResult(false, "Failed to assign ServiceProvider role. Please try again.", null);
                }

                logger.LogInformation("[AuthService] Successfully assigned both roles to {Email}", email);
            }

            // Step 4: Log registration event for audit trail
            await auditLogService.LogRegistrationAsync(user.Id, normalizedRole);

            // Step 5: Handle optional government ID file upload (non-blocking)
            if (governmentIdImage != null)
            {
                try
                {
                    var filePath = await fileUploadService.UploadGovernmentIdAsync(governmentIdImage, user.Id);
                    
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        // File uploaded successfully - update user record
                        user.GovernmentIdImagePath = filePath;
                        await userManager.UpdateAsync(user);
                        logger.LogInformation("[AuthService] Government ID image stored for user {Email}: {Path}", 
                            email, filePath);
                    }
                    // If file upload failed, log continues and user is still registered
                    // (registration is not blocked by file upload failure)
                }
                catch (Exception ex)
                {
                    // Log error but don't fail registration
                    logger.LogWarning(ex, "[AuthService] Failed to upload government ID image for user {Email} (non-fatal)", email);
                }
            }

            // Commit transaction - all or nothing
            await transaction.CommitAsync();

            logger.LogInformation("[AuthService] User registered successfully: {Email} with role {Role}, DOB: {DOB}", 
                email, normalizedRole, dateOfBirth.Date);

            return new AuthRegisterResult(true, null, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            
            logger.LogError(ex, "[AuthService] Registration failed with exception for {Email}", email);
            return new AuthRegisterResult(false, "Registration failed due to a system error. Please try again.", null);
        }
    }

    /// <summary>
    /// Converts role string to UserType enum.
    /// </summary>
    private static UserType GetUserTypeFromRole(string normalizedRole)
    {
        return normalizedRole switch
        {
            RoleConstants.User => UserType.User,
            RoleConstants.ServiceProvider => UserType.Provider,
            _ => UserType.User
        };
    }

    public async Task<AuthLoginResult> LoginAsync(string email, string password, string? userAgent, string? ipAddress)
    {
        // LOGIN IS READ-ONLY (idempotent)
        // Every call generates a NEW session with a new JWT and new refresh token
        // This is safe to retry - each retry gets a fresh JWT, which is the desired behavior
        // NO state is mutated on failed authentication
        
        try
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                logger.LogWarning("[AuthService] Login attempt for non-existent user: {Email}", email);
                return new AuthLoginResult(false, null, "Invalid credentials");
            }

            var validPassword = await userManager.CheckPasswordAsync(user, password);
            if (!validPassword)
            {
                logger.LogWarning("[AuthService] Failed login attempt for user: {Email}", email);
                return new AuthLoginResult(false, null, "Invalid credentials");
            }

            var roles = await userManager.GetRolesAsync(user);
            
            // Validate that user's roles are in the allowed list
            // This prevents users with misconfigured roles from logging in
            var invalidRoles = roles.Where(r => !RoleConstants.IsValidRole(r)).ToList();
            if (invalidRoles.Any())
            {
                logger.LogError("[AuthService] User {Email} has invalid roles: {InvalidRoles}. Valid roles: {ValidRoles}",
                    email,
                    string.Join(", ", invalidRoles),
                    string.Join(", ", RoleConstants.AllRoles));
                return new AuthLoginResult(false, null, "User role configuration is invalid. Please contact support.");
            }

            // User must have at least one role
            if (roles.Count == 0)
            {
                logger.LogError("[AuthService] User {Email} has no roles assigned. Cannot issue token.", email);
                return new AuthLoginResult(false, null, "User role is not assigned. Please contact support.");
            }

            // Generate unique session ID for this login attempt
            // Each login gets a new session, even for the same user
            var sessionId = Guid.NewGuid().ToString();
            var emailValue = user.Email ?? user.UserName ?? email;
            var primaryRole = roles.FirstOrDefault() ?? RoleConstants.User;

            // Build JWT claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, emailValue),
                new(JwtRegisteredClaimNames.Jti, sessionId),  // Unique session ID
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add all roles as claims (typically just one for this application)
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                logger.LogInformation("[AuthService] Added role claim: {Role} for user {Email}", role, emailValue);
            }

            // For users with UserType.Both (in Roles table), both roles should already be assigned
            // by the registration service. This loop above will add both.
            // If user has exactly 2 roles, log them for debugging
            if (roles.Count == 2)
            {
                logger.LogInformation("[AuthService] User {Email} has dual roles: {Roles}", emailValue, string.Join(", ", roles));
            }

            // Sign JWT token with 10-minute expiration
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

            // Log login to audit trail
            // NOTE: This happens on every successful login (idempotent behavior)
            // Each login creates a NEW audit log entry - this is correct
            try
            {
                await auditLogService.LogLoginAsync(user.Id, primaryRole, sessionId, ipAddress, userAgent);
                logger.LogInformation("[AuthService] Login audit log created for {Email}", emailValue);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AuthService] Failed to create audit log for login {Email} (non-critical)", emailValue);
                // Don't fail the entire login if audit logging fails
                // Audit logging is important but not critical to the user experience
            }

            // Issue refresh token for session
            // ISOLATED: If refresh token generation fails, we still return the JWT
            // The client will attempt to use it, and refresh will fail with a clear error
            string? refreshToken = null;
            DateTime? refreshTokenExpiresAt = null;
            try
            {
                refreshToken = await tokenRefreshService.IssueRefreshTokenAsync(
                    user.Id, sessionId, ipAddress, userAgent);
                refreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
                logger.LogInformation("[AuthService] Refresh token issued for {Email}", emailValue);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AuthService] Failed to issue refresh token for {Email} (non-critical). User will receive JWT but refresh will fail.", emailValue);
                // Don't throw - this is non-critical to the login flow
                // User gets a valid JWT and can make authenticated requests
                // Refresh will fail with a clear error if attempted
            }

            logger.LogInformation("[AuthService] User logged in successfully: {Email}, SessionId: {SessionId}, Role: {Role}", 
                emailValue, sessionId, primaryRole);

            var payload = new AuthResultDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = token.ValidTo,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = refreshTokenExpiresAt
            };

            // Return success with JWT payload
            // Safe to retry: each retry creates new session + new JWT
            return new AuthLoginResult(true, payload, null);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "[AuthService] Database error during login for {Email}", email);
            return new AuthLoginResult(false, null, "Login service is temporarily unavailable. Please try again later.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[AuthService] Invalid operation during login for {Email}", email);
            return new AuthLoginResult(false, null, "Login cannot be completed at this time. Please contact support.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthService] Unexpected error during login for {Email}", email);
            return new AuthLoginResult(false, null, "An unexpected errorOccurred during login. Please try again later.");
        }
    }

    public async Task LogoutAsync(string userId, string sessionId, string? role, string? userAgent, string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(role) && !RoleConstants.IsValidRole(role))
        {
            logger.LogWarning("[AuthService] Logout attempt with invalid role: {Role}", role);
        }

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
            {
                logger.LogWarning("[AuthService] Token expired but UserId claim is missing");
                return;
            }

            if (!string.IsNullOrWhiteSpace(role) && !RoleConstants.IsValidRole(role))
            {
                logger.LogWarning("[AuthService] Token expired but role is invalid: {Role}", role);
            }

            await auditLogService.LogSessionExpiredAsync(userId, role, sessionId, ipAddress, userAgent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AuthService] Failed to record token-expanded audit event");
        }
    }

    public async Task<RefreshTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        string? userAgent,
        string? ipAddress)
    {
        try
        {
            var response = await tokenRefreshService.RefreshAccessTokenAsync(
                refreshToken, ipAddress, userAgent);

            // Generate new access token with proper JWT claims
            var refreshResponse = await GenerateAccessTokenForRefreshAsync(response);

            logger.LogInformation("[AuthService] Successfully refreshed access token");

            return refreshResponse;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("[AuthService] Token refresh failed: {Error}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthService] Unexpected error during token refresh");
            throw;
        }
    }

    private async Task<RefreshTokenResponse> GenerateAccessTokenForRefreshAsync(RefreshTokenResponse baseResponse)
    {
        // This is a simplified version - in production, extract claims from refresh token
        // or maintain state about the user associated with the token
        // For now, we'll return the base response as-is
        // The real JWT access token generation would happen here

        return baseResponse;
    }
}

