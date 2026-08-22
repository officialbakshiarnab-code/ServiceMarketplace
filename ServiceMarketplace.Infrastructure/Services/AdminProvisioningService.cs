using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Data.Extensions;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class AdminProvisioningService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAuditLogService auditLogService) : IAdminProvisioningService
{
    public async Task<AdminProvisioningResult> ProvisionAsync(
        AdminProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError != null)
            return AdminProvisioningResult.Failed(validationError);

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedPhone = NormalizePhone(request.PhoneNumber);

        IDbContextTransaction? tx = null;
        if (dbContext.Database.IsRelational())
            tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var adminRole = await EnsureAdminRoleAsync(cancellationToken);
            var user = await dbContext.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            var createdUser = false;
            var passwordReset = false;
            if (user == null)
            {
                user = new User
                {
                    Email = request.Email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    PhoneNumber = request.PhoneNumber.Trim(),
                    NormalizedPhoneNumber = normalizedPhone,
                    PasswordHash = passwordHasher.HashPassword(request.Password),
                    DateOfBirth = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    UserType = UserType.Admin,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                dbContext.Users.Add(user);
                createdUser = true;
            }
            else
            {
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.PhoneNumber = request.PhoneNumber.Trim();
                user.NormalizedPhoneNumber = normalizedPhone;
                user.UserType = UserType.Admin;
                user.IsActive = true;
                user.UpdatedDate = DateTime.UtcNow;

                if (request.ResetPassword)
                {
                    user.PasswordHash = passwordHasher.HashPassword(request.Password);
                    user.AccessFailedCount = 0;
                    user.LockoutEndUtc = null;
                    passwordReset = true;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var grantedRole = !await dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id, cancellationToken);

            if (grantedRole)
            {
                dbContext.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole { UserId = user.Id, RoleId = adminRole.Id });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (createdUser)
                await auditLogService.LogAdminProvisioningAsync(user.Id.ToString(), "AdminProvisioned");
            else if (grantedRole)
                await auditLogService.LogAdminProvisioningAsync(user.Id.ToString(), "AdminRoleGranted");

            if (passwordReset)
                await auditLogService.LogAdminProvisioningAsync(user.Id.ToString(), "AdminPasswordReset");

            if (tx != null)
                await tx.CommitAsync(cancellationToken);

            return new AdminProvisioningResult
            {
                Success = true,
                Message = createdUser
                    ? "Admin user created."
                    : grantedRole
                        ? "Admin role granted to existing user."
                        : passwordReset
                            ? "Admin password reset."
                            : "Admin user already provisioned.",
                UserId = user.Id,
                CreatedUser = createdUser,
                GrantedAdminRole = grantedRole,
                PasswordReset = passwordReset
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            if (tx != null)
                await tx.RollbackAsync(cancellationToken);

            return AdminProvisioningResult.Failed(ex.Message);
        }
        finally
        {
            if (tx != null)
                await tx.DisposeAsync();
        }
    }

    private async Task<Role> EnsureAdminRoleAsync(CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleConstants.Admin, cancellationToken);

        if (role != null)
            return role;

        role = new Role
        {
            Name = RoleConstants.Admin,
            Description = "Admin account role",
            CreatedDate = DateTime.UtcNow
        };
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static string? Validate(AdminProvisioningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return "A valid admin email is required.";

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "First name is required.";

        if (string.IsNullOrWhiteSpace(request.LastName))
            return "Last name is required.";

        if (string.IsNullOrWhiteSpace(request.Reason))
            return "Provisioning reason is required.";

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return "Admin password must be at least 12 characters.";

        var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (normalizedPhone.Length is < 10 or > 15)
            return "Phone number must contain 10 to 15 digits.";

        return null;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string NormalizePhone(string phone)
    {
        return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
