using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Domain.Entities;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for Identity + marketplace entities.
/// Uses ApplicationUser for extended profile information.
/// Includes audit logging for authentication sessions and refresh token management.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ========================================
        // RENAME IDENTITY TABLES
        // ========================================
        // Rename AspNet* tables to cleaner, more readable names
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        // ========================================
        // CONFIGURE APPLICATION USER
        // ========================================
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.PhonePrimary)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(u => u.PhoneSecondary)
                .HasMaxLength(20);

            entity.Property(u => u.DateOfBirth)
                .IsRequired();

            entity.Property(u => u.GovernmentIdImagePath)
                .HasMaxLength(500);

            entity.Property(u => u.UserType)
                .IsRequired();

            entity.Property(u => u.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // Create index for common queries
            entity.HasIndex(u => u.UserType)
                .HasDatabaseName("IX_Users_UserType");
        });

        // ========================================
        // CONFIGURE BID ENTITY
        // ========================================
        builder.Entity<Bid>()
            .Property(b => b.Amount)
            .HasPrecision(18, 2);

        // ========================================
        // CONFIGURE AUDIT LOG
        // ========================================
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_AuditLogs_UserId");

            entity.HasIndex(e => e.TimestampUtc)
                .IsDescending()
                .HasDatabaseName("IX_AuditLogs_TimestampUtc");

            entity.HasIndex(e => e.SessionId)
                .HasDatabaseName("IX_AuditLogs_SessionId");

            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Role)
                .HasMaxLength(50);

            entity.Property(e => e.SessionId)
                .HasMaxLength(100);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);
        });

        // ========================================
        // CONFIGURE REFRESH TOKEN ENTITY
        // ========================================
        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Index for quick lookups by token hash
            entity.HasIndex(e => e.TokenHash)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_TokenHash");

            // Index for finding tokens by user (IsActive is computed, so we index UserId only)
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");

            // Index for finding tokens by family (rotation chain)
            entity.HasIndex(e => e.TokenFamily)
                .HasDatabaseName("IX_RefreshTokens_TokenFamily");

            // Index for cleanup queries (find expired tokens)
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.SessionId)
                .IsRequired();

            entity.Property(e => e.TokenHash)
                .IsRequired();

            entity.Property(e => e.TokenFamily)
                .IsRequired();

            entity.Property(e => e.RevocationReason)
                .HasMaxLength(100);

            entity.Property(e => e.IssuedFromIpAddress)
                .HasMaxLength(45);

            entity.Property(e => e.IssuedFromUserAgent)
                .HasMaxLength(500);

            entity.Property(e => e.LastUsedIpAddress)
                .HasMaxLength(45);
        });
    }
}


