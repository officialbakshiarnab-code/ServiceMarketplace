# Database Configuration - Technical Reference Guide

## Quick Reference

| Item | Value |
|------|-------|
| **Database Server** | DEXTER\SQLEXPRESS |
| **Database Name** | ServiceMarketplaceDB |
| **Connection String** | `Server=DEXTER\SQLEXPRESS;Database=ServiceMarketplaceDB;Trusted_Connection=True;TrustServerCertificate=True;` |
| **Provider** | SQL Server |
| **Authentication** | Windows Authentication |
| **DbContext** | AppDbContext |
| **DbContext Location** | ServiceMarketplace.Infrastructure/Data/AppDbContext.cs |
| **Registration Point** | ServiceMarketplace.API/Program.cs (Line 46) |
| **Configuration File** | ServiceMarketplace.API/appsettings.json |
| **Design-Time Factory** | AppDbContextFactory |
| **Migration Location** | ServiceMarketplace.Infrastructure/Migrations/ |

---

## File Structure

```
ServiceMarketplace/
??? ServiceMarketplace.API/
?   ??? Program.cs ? DbContext registration (Line 46)
?   ??? appsettings.json ? Connection string (Line 8)
?   ??? appsettings.Development.json (if exists)
??? ServiceMarketplace.Infrastructure/
?   ??? Data/
?   ?   ??? AppDbContext.cs ? Main DbContext
?   ?   ??? AppDbContextFactory.cs ? Design-time factory
?   ??? Migrations/
?       ??? AppDbContextModelSnapshot.cs ? Schema snapshot
?       ??? [migration files]
??? ServiceMarketplace.UI.Web/ (no database)
??? ServiceMarketplace.UI.MAUI/ (no database)
??? [other projects] (no database access)
```

---

## Configuration Sources

### 1. Runtime Configuration
**File**: `ServiceMarketplace.API/appsettings.json`

```json
{
  "Jwt": {...},
  "ConnectionStrings": {
    "DefaultConnection": "Server=DEXTER\\SQLEXPRESS;Database=ServiceMarketplaceDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Cors": {...},
  "Security": {...},
  "Logging": {...},
  "AllowedHosts": "*"
}
```

### 2. Development Overrides (Optional)
**File**: `ServiceMarketplace.API/appsettings.Development.json`

If present, merges with base configuration using ASP.NET Core's configuration system.

### 3. Environment Variables (Optional)
Can override connection string:
```bash
set ConnectionStrings__DefaultConnection="Server=...;Database=...;"
```

### 4. User Secrets (Production)
In production, use:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
```

---

## DbContext Registration

### Location
**File**: `ServiceMarketplace.API/Program.cs` (Lines 46-51)

### Code
```csharp
// ==============================
// DATABASE
// ==============================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(new AuditInterceptor());
});
```

### What This Does
1. Registers `AppDbContext` in the DI container as a scoped service
2. Configures SQL Server provider
3. Reads connection string from IConfiguration ("DefaultConnection" key)
4. Attaches AuditInterceptor for automatic audit logging

### Scope
- **Scoped**: New instance per HTTP request
- **Disposed**: Automatically at end of request
- **Thread-safe**: Each request has its own instance

---

## DbContext Implementation

### Location
**File**: `ServiceMarketplace.Infrastructure/Data/AppDbContext.cs`

### Class Definition
```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Configuration...
    }
}
```

### Key Features
- **Primary Constructor**: Accepts `DbContextOptions<AppDbContext>`
- **Inheritance**: Extends `IdentityDbContext` (not just DbContext)
- **Entity Sets**: 4 DbSets for domain entities
- **Identity Support**: Includes all ASP.NET Core Identity tables
- **Model Configuration**: OnModelCreating for relationships

### Identity Tables (Automatic)
When inheriting from `IdentityDbContext`, these are automatically included:
- AspNetUsers ? Users
- AspNetRoles ? Roles
- AspNetUserRoles ? UserRoles
- AspNetUserClaims ? UserClaims
- AspNetUserLogins ? UserLogins
- AspNetRoleClaims ? RoleClaims
- AspNetUserTokens ? UserTokens

---

## Design-Time Factory

### Location
**File**: `ServiceMarketplace.Infrastructure/Data/AppDbContextFactory.cs`

### Purpose
Used by Entity Framework Core tools (migrations) when running outside of the application context.

### Code
```csharp
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Navigate to API project (where appsettings.json is)
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ServiceMarketplace.API");

        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Create DbContext with same connection
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

### Key Points
- Reads same `appsettings.json` as runtime
- Finds connection string in same location
- Uses same SQL Server provider
- Ensures migrations use correct database

### When It's Used
```bash
# These commands use the factory:
dotnet ef migrations add [MigrationName]
dotnet ef database update
dotnet ef dbcontext info
```

---

## Configuration Resolution Order

### ASP.NET Core Configuration Priority (Highest to Lowest)

```
1. Environment Variables
   ?
2. User Secrets (Development only)
   ?
3. appsettings.{Environment}.json (e.g., appsettings.Development.json)
   ?
4. appsettings.json (Base configuration)
   ?
5. Default hardcoded values (None in this app)
```

### For Connection String Specifically

```
Environment Variable: ConnectionStrings__DefaultConnection
    ? (or)
appsettings.Development.json: ConnectionStrings.DefaultConnection
    ? (or)
appsettings.json: ConnectionStrings.DefaultConnection
    ? (or)
Error: Connection string not found
```

---

## Service Injection Pattern

### How Services Get AppDbContext

```csharp
// 1. Service is registered in DI container
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// 2. AuditLogService constructor
public sealed class AuditLogService(AppDbContext context, ILogger<AuditLogService> logger)
{
    private readonly AppDbContext _context = context;
    // ...
}

// 3. When dependency is needed, DI container:
//    - Gets registered AppDbContext factory from #46 of Program.cs
//    - Creates new scoped AppDbContext instance
//    - Passes to AuditLogService constructor
//    - Service uses same instance for all operations in that request

// 4. At end of request:
//    - DbContext disposed automatically
//    - Database connection returned to connection pool
//    - All pending changes committed or rolled back
```

### Services Using AppDbContext
- AuthService
- AuditLogService
- ServiceRequestService
- BidService
- TokenRefreshService
- AdminKpiService
- CleanupService
- RoleSeedingService
- All background services
- All health checks

---

## Migration System

### AutoMigrate on Startup
**Location**: `ServiceMarketplace.API/Program.cs` (Lines 235-248)

```csharp
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            dbContext.Database.Migrate();
            app.Logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Error applying database migrations");
            throw;
        }
    }
}
```

### What This Does
1. Checks if running in Development
2. Gets AppDbContext instance from DI
3. Runs `Database.Migrate()` (applies pending migrations)
4. Logs success or error

### Migration Files Location
```
ServiceMarketplace.Infrastructure/
??? Migrations/
    ??? 20260125151224_AddIdentity.cs
    ??? 20250130_AddMissingColumnsToRequestsAndBids.cs
    ??? 20260128223201_RenameIdentityTablesToCleanNames.cs
    ??? AppDbContextModelSnapshot.cs ? Current schema
```

### Manual Migration Commands
```bash
# Add a new migration
dotnet ef migrations add [MigrationName] --project ServiceMarketplace.Infrastructure

# Update database to latest migration
dotnet ef database update --project ServiceMarketplace.Infrastructure

# View pending migrations
dotnet ef migrations list --project ServiceMarketplace.Infrastructure

# Rollback to previous migration
dotnet ef database update [PreviousMigrationName] --project ServiceMarketplace.Infrastructure

# Generate migration without applying
dotnet ef migrations script --output migration.sql
```

---

## Database Tables

### Identity Tables
| Table | Purpose | Key Column |
|-------|---------|-----------|
| Users | User accounts | Id (string) |
| Roles | Role definitions | Id (string) |
| UserRoles | User-Role mappings | UserId, RoleId |
| UserClaims | Additional claims | Id (int) |
| UserLogins | OAuth logins | LoginProvider, ProviderKey |
| RoleClaims | Role-based claims | Id (int) |
| UserTokens | OAuth tokens | UserId, LoginProvider, Name |

### Business Tables
| Table | Purpose | Key Column |
|-------|---------|-----------|
| ServiceRequests | Customer requests | Id (Guid) |
| Bids | Provider bids | Id (Guid) |

### System Tables
| Table | Purpose | Key Column |
|-------|---------|-----------|
| AuditLogs | Auth events | Id (Guid) |
| RefreshTokens | Session tokens | Id (Guid) |

---

## Connection String Format

### Standard Format
```
Server=DEXTER\SQLEXPRESS;Database=ServiceMarketplaceDB;Trusted_Connection=True;TrustServerCertificate=True;
```

### Component Breakdown
| Component | Value | Meaning |
|-----------|-------|---------|
| Server | DEXTER\SQLEXPRESS | SQL Server instance on DEXTER machine |
| Database | ServiceMarketplaceDB | Database name |
| Trusted_Connection | True | Windows Authentication (no username/password) |
| TrustServerCertificate | True | Accept self-signed certificates (dev only) |

### For Production
Replace with:
```
Server=prod-server.company.com,1433;Database=ServiceMarketplaceDB;User Id=sa;Password=***;Encrypt=True;TrustServerCertificate=False;
```

---

## Health Check Integration

### DatabaseHealthCheck
**Location**: `ServiceMarketplace.API/HealthChecks/DatabaseHealthCheck.cs`

**Purpose**: Verifies database connectivity

**Registered**: `ServiceMarketplace.API/Program.cs` (Line 259)

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        tags: new[] { "db", "sql", "ready" });
```

**Endpoint**: `GET /health`

**Expected Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": {
      "status": "Healthy"
    }
  }
}
```

---

## Transaction Handling

### Explicit Transactions
```csharp
using var transaction = await dbContext.Database.BeginTransactionAsync();
try
{
    // Multiple operations
    await dbContext.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Example: AuthService.RegisterAsync()
**Location**: `ServiceMarketplace.Infrastructure/Services/AuthService.cs` (Lines 24-74)

Uses explicit transaction for atomicity:
1. Create user account
2. Verify role exists
3. Assign role to user
4. Log registration
5. Commit all changes or rollback

---

## Common Issues & Solutions

### Issue: Connection String Not Found
**Error**: `"Connection string 'DefaultConnection' not found."`

**Solutions**:
1. Check `appsettings.json` has `ConnectionStrings.DefaultConnection`
2. Verify JSON syntax is correct
3. Restart the application
4. Check environment variables aren't conflicting

### Issue: Cannot Connect to Database
**Error**: `"A network-related or instance-specific error occurred while establishing a connection to SQL Server"`

**Solutions**:
1. Verify SQL Server instance is running: `Services.msc` ? SQL Server (MSSQLSERVER)
2. Check instance name: SQL Server Configuration Manager
3. Verify machine name and instance name in connection string
4. Test connection: `sqlcmd -S DEXTER\SQLEXPRESS`

### Issue: Migration Not Applied
**Error**: `"Invalid column name 'X'"`

**Solutions**:
1. Check pending migrations: `dotnet ef migrations list --project ServiceMarketplace.Infrastructure`
2. Apply manually: `dotnet ef database update --project ServiceMarketplace.Infrastructure`
3. Verify schema is current: Check `AppDbContextModelSnapshot.cs`

### Issue: DbContext Not Registered
**Error**: `"Unable to resolve service for type 'AppDbContext'"`

**Solutions**:
1. Check `Program.cs` line 46: `AddDbContext<AppDbContext>()`
2. Verify it's called before building the app
3. Ensure AppDbContext is in the correct namespace

---

## Monitoring & Diagnostics

### Check DbContext Configuration
```bash
dotnet ef dbcontext info --project ServiceMarketplace.Infrastructure
```

**Output**:
```
Build started...
Provider name: Microsoft.EntityFrameworkCore.SqlServer
Options: None
Database: ServiceMarketplaceDB
```

### List All Migrations
```bash
dotnet ef migrations list --project ServiceMarketplace.Infrastructure
```

### Connection String Resolution
```csharp
// In Program.cs after configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"Using connection: {connectionString}");
```

### Database Connection Test
```csharp
using (var context = new AppDbContext(options))
{
    if (await context.Database.CanConnectAsync())
        Console.WriteLine("? Connection successful");
    else
        Console.WriteLine("? Connection failed");
}
```

---

## Best Practices

### ? DO
- Use dependency injection for AppDbContext
- Use async/await for database operations
- Dispose DbContext properly (scoped lifetime)
- Use migrations for schema changes
- Log database errors
- Use connection pooling (automatic)
- Use parameterized queries (LINQ does this)

### ? DON'T
- Create multiple DbContext instances per request
- Share DbContext across threads
- Hardcode connection strings
- Ignore configuration files
- Skip migrations
- Commit database connection changes without testing

---

## Performance Tuning

### Connection Pooling (Automatic)
EF Core automatically pools connections using:
```
Max Pool Size: 100 (default)
Min Pool Size: 5 (default)
```

### Optimize Queries
```csharp
// Good: Specific projection
var users = context.Users
    .Where(u => u.Email == email)
    .Select(u => new { u.Id, u.Email })
    .FirstOrDefaultAsync();

// Avoid: Loading entire entity
var user = context.Users
    .Where(u => u.Email == email)
    .FirstOrDefaultAsync();
```

### Add Indexes
```csharp
builder.Entity<User>()
    .HasIndex(u => u.Email)
    .IsUnique();
```

---

## Security Considerations

### Connection String Security
- **Development**: OK to use Windows Auth in appsettings.json
- **Production**: Use Azure Key Vault or environment variables
- **Never**: Commit passwords to Git

### Trusted_Connection=True
- Uses Windows Authentication
- Only works on same network
- Requires service account in production

### TrustServerCertificate=True
- Accepts self-signed certificates
- **Development only** - not for production
- Use Encrypt=True;TrustServerCertificate=False; in production

---

## Version Information

| Component | Version |
|-----------|---------|
| .NET Framework | 9.0 |
| C# Language | 13.0 |
| EF Core | 9.0.0 |
| SQL Server Provider | Microsoft.EntityFrameworkCore.SqlServer 9.0.0 |
| ASP.NET Core | 9.0 |

---

## Reference Links

- [EF Core Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [SQL Server Connection Strings](https://learn.microsoft.com/en-us/sql/connect/ado-net/build-connection-string)
- [Migrations in EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [DbContext Lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/lifetime)

---

**Last Updated**: February 1, 2025  
**Status**: ? Current and Verified
