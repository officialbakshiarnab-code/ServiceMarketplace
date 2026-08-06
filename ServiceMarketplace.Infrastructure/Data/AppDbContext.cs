using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using UserRoleEntity = ServiceMarketplace.Domain.Entities.UserRole;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// Application DbContext for marketplace persistence using custom auth tables.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("ServiceRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Location).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("IX_ServiceRequests_CustomerId");
        });

        builder.Entity<Bid>(entity =>
        {
            entity.ToTable("Bids");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceProviderId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.ProposedDateTime).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.ServiceRequestId)
                .HasDatabaseName("IX_Bids_ServiceRequestId");

            entity.HasIndex(e => e.ServiceProviderId)
                .HasDatabaseName("IX_Bids_ServiceProviderId");

            entity.HasOne(e => e.ServiceRequest)
                .WithMany(r => r.Bids)
                .HasForeignKey(e => e.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasIndex(e => e.SessionId)
                .HasDatabaseName("IX_AuditLogs_SessionId");

            entity.HasIndex(e => e.TimestampUtc)
                .IsDescending()
                .HasDatabaseName("IX_AuditLogs_TimestampUtc");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_AuditLogs_UserId");
        });

        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.SessionId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.TokenFamily)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.RevocationReason).HasMaxLength(100);
            entity.Property(e => e.IssuedFromIpAddress).HasMaxLength(45);
            entity.Property(e => e.IssuedFromUserAgent).HasMaxLength(500);
            entity.Property(e => e.LastUsedIpAddress).HasMaxLength(45);

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.TokenFamily)
                .HasDatabaseName("IX_RefreshTokens_TokenFamily");

            entity.HasIndex(e => e.TokenHash)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_TokenHash");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");
        });

        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).HasMaxLength(150);
            entity.Property(u => u.NormalizedEmail).HasMaxLength(150);
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.NormalizedPhoneNumber).HasMaxLength(20);
            entity.Property(u => u.SecondaryPhoneNumber).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.IsEmailVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.IsPhoneVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.TwoFactorEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(u => u.UserType)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(UserType.Customer);
            entity.Property(u => u.GovIdFilePath).HasMaxLength(500);
            entity.Property(u => u.KycApprovedByUserId).HasMaxLength(450);
            entity.Property(u => u.CreatedDate).IsRequired().HasDefaultValueSql("now()");

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedEmail");

            entity.HasIndex(u => u.NormalizedPhoneNumber)
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedPhoneNumber");

            entity.HasIndex(u => u.UserType)
                .HasDatabaseName("IX_Users_UserType");
        });

        builder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).HasMaxLength(250);
            entity.Property(r => r.CreatedDate).IsRequired().HasDefaultValueSql("now()");
            entity.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UX_Roles_Name");

            entity.HasData(
                new Role
                {
                    Id = new Guid("11111111-1111-1111-1111-111111111111"),
                    Name = "User",
                    Description = "Customer account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("22222222-2222-2222-2222-222222222222"),
                    Name = "ServiceProvider",
                    Description = "Service provider account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("33333333-3333-3333-3333-333333333333"),
                    Name = "Both",
                    Description = "Dual customer and service provider account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("44444444-4444-4444-4444-444444444444"),
                    Name = "Admin",
                    Description = "Administrator account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        builder.Entity<UserRoleEntity>(entity =>
        {
            entity.ToTable("UserRoles");

            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(ur => ur.RoleId)
                .HasDatabaseName("IX_UserRoles_RoleId");
        });
    }
}
