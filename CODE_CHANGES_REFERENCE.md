# Code Changes Reference - Idempotent Registration

This file documents the exact code changes made for idempotent registration.

---

## File 1: ServiceMarketplace.Infrastructure\Services\AuthService.cs

### Change Summary
- Added AppDbContext parameter to constructor
- Enhanced RegisterAsync() with idempotency logic and transaction wrapping
- Added registration audit event logging

### Constructor Change
```csharp
// BEFORE
public sealed class AuthService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    IAuditLogService auditLogService,
    ILogger<AuthService> logger) : IAuthService

// AFTER
public sealed class AuthService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    IAuditLogService auditLogService,
    AppDbContext dbContext,  // ? ADDED
    ILogger<AuthService> logger) : IAuthService
```

### RegisterAsync() Method - COMPLETE REPLACEMENT

```csharp
public async Task<AuthRegisterResult> RegisterAsync(string email, string password, string role)
{
    // Idempotency check: If user already exists, return success instead of error
    var userExists = await userManager.FindByEmailAsync(email);
    if (userExists != null)
    {
        // User exists - check if they have the requested role
        var userRoles = await userManager.GetRolesAsync(userExists);
        if (userRoles.Contains(role))
        {
            // User exists with the exact role requested - return success (idempotent)
            logger.LogInformation("Idempotent registration: User already exists with requested role: {Email}, Role: {Role}", email, role);
            return new AuthRegisterResult(true, null, null);
        }
        
        // User exists but with different role - this is a genuine conflict
        logger.LogWarning("Registration attempted for existing email with different role: {Email}, Existing roles: {Roles}, Requested role: {Role}", 
            email, string.Join(",", userRoles), role);
        return new AuthRegisterResult(false, "User already exists with a different role", null);
    }

    // User doesn't exist - proceed with registration in a transaction
    using var transaction = await dbContext.Database.BeginTransactionAsync();
    try
    {
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        // 1. Create user
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            
            var errors = result.Errors
                .Select(e => new AuthErrorDto { Code = e.Code, Description = e.Description })
                .ToList();

            logger.LogWarning("User creation failed for {Email}: {Errors}",
                email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new AuthRegisterResult(false, null, errors);
        }

        // 2. Ensure role exists
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            logger.LogInformation("Created new role: {Role}", role);
        }

        // 3. Assign role to user
        await userManager.AddToRoleAsync(user, role);

        // 4. Log registration event
        await auditLogService.LogRegistrationAsync(user.Id, role);

        // Commit transaction
        await transaction.CommitAsync();

        logger.LogInformation("User registered successfully: {Email} with role {Role}", email, role);

        return new AuthRegisterResult(true, null, null);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        
        logger.LogError(ex, "Registration failed with exception for {Email}", email);
        return new AuthRegisterResult(false, "Registration failed due to a system error", null);
    }
}
```

### Additions to File
- Added using statement: `using Microsoft.EntityFrameworkCore;`

---

## File 2: ServiceMarketplace.Application\Interfaces\IAuditLogService.cs

### Change Summary
- Added LogRegistrationAsync() method to interface

### Change Details
Add this method to the IAuditLogService interface (at the beginning of method definitions):

```csharp
/// <summary>
/// Records a Registration event in the audit log.
/// </summary>
/// <param name="userId">User identifier from ASP.NET Identity</param>
/// <param name="role">User's assigned role</param>
Task LogRegistrationAsync(string userId, string role);
```

**Full updated interface**:
```csharp
namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Application contract for centralized audit logging.
/// All authentication and authorization events must be logged through this service.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records a Registration event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's assigned role</param>
    Task LogRegistrationAsync(string userId, string role);

    /// <summary>
    /// Records a Login event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of login</param>
    /// <param name="sessionId">Unique session identifier (from JWT jti claim)</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    Task LogLoginAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Records a Logout event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of logout</param>
    /// <param name="sessionId">Session identifier from JWT</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    Task LogLogoutAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Records a SessionExpired event in the audit log.
    /// Prevents duplicate entries for the same session.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of expiration</param>
    /// <param name="sessionId">Session identifier from JWT</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    /// <returns>True if event was logged, false if duplicate was detected</returns>
    Task<bool> LogSessionExpiredAsync(string userId, string? role, string? sessionId, string? ipAddress, string? userAgent);
}
```

---

## File 3: ServiceMarketplace.Infrastructure\Services\AuditLogService.cs

### Change Summary
- Added LogRegistrationAsync() method implementation

### Change Details
Add this method to the AuditLogService class (at the beginning, before LogLoginAsync):

```csharp
public async Task LogRegistrationAsync(string userId, string role)
{
    await RecordEventAsync(userId, role, "Registration", null, null, null);
    logger.LogInformation("Registration event recorded for user {UserId}, role {Role}", userId, role);
}
```

**Full class (updated)**:
```csharp
public sealed class AuditLogService(AppDbContext context, ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogRegistrationAsync(string userId, string role)
    {
        await RecordEventAsync(userId, role, "Registration", null, null, null);
        logger.LogInformation("Registration event recorded for user {UserId}, role {Role}", userId, role);
    }

    public async Task LogLoginAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent)
    {
        await RecordEventAsync(userId, role, "Login", sessionId, ipAddress, userAgent);
        logger.LogInformation("Login event recorded for user {UserId}, session {SessionId}", userId, sessionId);
    }

    public async Task LogLogoutAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent)
    {
        // ... existing implementation ...
    }

    public async Task<bool> LogSessionExpiredAsync(string userId, string? role, string? sessionId, string? ipAddress, string? userAgent)
    {
        // ... existing implementation ...
    }

    private async Task RecordEventAsync(string userId, string? role, string eventType, string? sessionId, string? ipAddress, string? userAgent)
    {
        // ... existing implementation ...
    }
}
```

---

## Summary of Changes

| File | Type | Method | Change |
|------|------|--------|--------|
| AuthService.cs | Service | Constructor | Added AppDbContext parameter |
| AuthService.cs | Service | RegisterAsync() | Complete rewrite with idempotency + transaction |
| AuthService.cs | Service | (namespace) | Added `using Microsoft.EntityFrameworkCore;` |
| IAuditLogService.cs | Interface | (new) | Added LogRegistrationAsync() method |
| AuditLogService.cs | Service | (new) | Added LogRegistrationAsync() implementation |

---

## Testing the Changes

### Unit Test Example

```csharp
[Fact]
public async Task RegisterAsync_WithExistingUser_ReturnsSuccess()
{
    // Arrange
    var userManager = new Mock<UserManager<IdentityUser>>();
    var roleManager = new Mock<RoleManager<IdentityRole>>();
    var config = new Mock<IConfiguration>();
    var auditLog = new Mock<IAuditLogService>();
    var dbContext = new Mock<AppDbContext>();
    var logger = new Mock<ILogger<AuthService>>();

    var user = new IdentityUser { Id = "123", Email = "test@example.com" };
    userManager.Setup(um => um.FindByEmailAsync("test@example.com"))
        .ReturnsAsync(user);
    userManager.Setup(um => um.GetRolesAsync(user))
        .ReturnsAsync(new List<string> { "User" });

    var service = new AuthService(userManager.Object, roleManager.Object, config.Object, 
        auditLog.Object, dbContext.Object, logger.Object);

    // Act
    var result = await service.RegisterAsync("test@example.com", "password", "User");

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(result.Error);
    // Verify no audit logging called (since user exists)
    auditLog.Verify(al => al.LogRegistrationAsync(It.IsAny<string>(), It.IsAny<string>()), 
        Times.Never);
}

[Fact]
public async Task RegisterAsync_WithNewUser_CreatesUserInTransaction()
{
    // Arrange
    var transaction = new Mock<IDbContextTransaction>();
    var dbContext = new Mock<AppDbContext>();
    dbContext.Setup(db => db.Database.BeginTransactionAsync(default))
        .ReturnsAsync(transaction.Object);

    // ... rest of setup ...

    // Act
    var result = await service.RegisterAsync("new@example.com", "password", "User");

    // Assert
    Assert.True(result.Succeeded);
    // Verify transaction started and committed
    dbContext.Verify(db => db.Database.BeginTransactionAsync(default), Times.Once);
    transaction.Verify(t => t.CommitAsync(default), Times.Once);
}
```

---

## Build Verification

After applying these changes, run:

```bash
dotnet build
```

Expected output:
```
========== Build: 7 succeeded, 0 failed, 0 up-to-date, 0 skipped ==========
```

---

## Integration Points

These changes integrate with:
- ? `IAuthService` interface (method signature matches)
- ? `IAuditLogService` interface (new method added)
- ? `AppDbContext` (transaction support)
- ? `UserManager<IdentityUser>` (existing, unchanged)
- ? `RoleManager<IdentityRole>` (existing, unchanged)

No breaking changes to any public APIs.

---

## Rollback

To rollback these changes:

1. Revert RegisterAsync() to original version (check user exists ? return error)
2. Remove AppDbContext parameter from constructor
3. Remove LogRegistrationAsync() from IAuditLogService interface
4. Remove LogRegistrationAsync() from AuditLogService implementation
5. Remove transaction handling
6. Rebuild

No database migration rollback needed (no schema changes).
