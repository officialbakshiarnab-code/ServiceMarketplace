using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Domain.Entities;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for Identity + marketplace entities.
/// Includes audit logging for authentication sessions.
/// </summary>
public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<LoginAuditLog> LoginAuditLogs => Set<LoginAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ========================================
        // RENAME IDENTITY TABLES
        // ========================================
        // Rename AspNet* tables to cleaner, more readable names
        builder.Entity<IdentityUser>().ToTable("Users");
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        // ========================================
        // CONFIGURE BID ENTITY
        // ========================================
        builder.Entity<Bid>()
            .Property(b => b.Amount)
            .HasPrecision(18, 2);

        // ========================================
        // CONFIGURE LOGIN AUDIT LOG
        // ========================================
        builder.Entity<LoginAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_LoginAuditLog_UserId");
            
            entity.HasIndex(e => e.LoginTime)
                .IsDescending()
                .HasDatabaseName("IX_LoginAuditLog_LoginTime");
            
            entity.HasIndex(e => e.SessionId)
                .HasDatabaseName("IX_LoginAuditLog_SessionId");

            entity.Property(e => e.LoginProvider)
                .HasMaxLength(50)
                .HasDefaultValue("JWT");

            entity.Property(e => e.SessionId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45); // IPv6 length

            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);

            entity.Property(e => e.Platform)
                .HasMaxLength(50);
        });
    }
}

