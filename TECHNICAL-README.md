# Technical Architecture Guide

This document explains the internal architecture, design decisions, and implementation patterns used in ServiceMarketplace.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Authentication & Authorization](#authentication--authorization)
3. [Business Rules & Entities](#business-rules--entities)
4. [Security Implementation](#security-implementation)
5. [Background Jobs](#background-jobs)
6. [Testing Strategy](#testing-strategy)

---

## Architecture Overview

ServiceMarketplace follows **Clean Architecture** principles with four distinct layers, each with clear responsibilities and minimal dependencies on other layers.

### Layer Structure

```
???????????????????????????????????????
?  UI Layer (Blazor WASM + MAUI)      ?  (ServiceMarketplace.UI.*)
?  • Razor components                 ?
?  • Authentication state provider    ?
?  • API client wrappers              ?
???????????????????????????????????????
                  ? (HTTP + JWT)
???????????????????????????????????????
?  API Layer (ASP.NET Core)           ?  (ServiceMarketplace.API)
?  • Controllers                      ?
?  • Middleware (auth, logging)       ?
?  • Health checks                    ?
?  • Rate limiting policies           ?
???????????????????????????????????????
                  ? (Dependency Injection)
???????????????????????????????????????
?  Application Layer                  ?  (ServiceMarketplace.Application)
?  • Use case implementations         ?
?  • DTOs (data transfer objects)     ?
?  • Validators (FluentValidation)    ?
?  • Exceptions                       ?
?  • Service interfaces               ?
???????????????????????????????????????
                  ?
???????????????????????????????????????
?  Domain Layer                       ?  (ServiceMarketplace.Domain)
?  • Entities (User, ServiceRequest)  ?
?  • Enums (UserType, Status)         ?
?  • Value objects                    ?
?  • Domain-level contracts           ?
???????????????????????????????????????
                  ?
???????????????????????????????????????
?  Infrastructure Layer               ?  (ServiceMarketplace.Infrastructure)
?  • EF Core DbContext                ?
?  • Identity integration             ?
?  • Application services             ?
?  • Background services              ?
?  • Database migrations              ?
???????????????????????????????????????
                  ? (SQL)
???????????????????????????????????????
?  SQL Server Database                ?
?  • Users (Identity + Profile)       ?
?  • ServiceRequests, Bids            ?
?  • AuditLogs                        ?
?  • RefreshTokens                    ?
???????????????????????????????????????
```

### Why This Structure?

1. **Separation of Concerns**: Each layer has one reason to change
2. **Testability**: Business logic (Application) is independent of infrastructure
3. **Flexibility**: Can swap implementations (e.g., different databases) without affecting domain
4. **Maintainability**: Clear responsibilities make code easier to understand
5. **Scalability**: Each layer can evolve independently

### Dependency Rules

- **Upper layers** (UI, API) depend on lower layers (Application, Domain, Infrastructure)
- **Lower layers** (Domain) never depend on upper layers
- **Never skip layers**: UI must not directly access Infrastructure

---

## Authentication & Authorization

ServiceMarketplace uses **JWT (JSON Web Token)** authentication with **refresh token rotation** for secure, stateless authentication.

### JWT Token Flow

```
1. User submits credentials (email + password)
   ?
2. Server validates via ASP.NET Identity
   ?
3. Server generates:
   - Access Token (JWT, 10-minute expiry)
   - Refresh Token (7-day expiry, hashed in database)
   - SessionId (unique GUID for audit trail)
   ?
4. Client stores both tokens securely
   (Access token in memory, Refresh token in secure storage)
   ?
5. Client includes Access Token in API request headers:
   Authorization: Bearer <access-token>
   ?
6. Server validates JWT signature and expiry
```

### Refresh Token Rotation

When the access token expires:

```
1. Client detects expiration (via token timestamps)
   ?
2. Client sends Refresh Token to POST /api/auth/refresh
   ?
3. Server validates:
   - Token hash matches stored value
   - Not expired
   - Not revoked
   ?
4. Server:
   - Issues new Access Token (10-minute expiry)
   - Issues new Refresh Token (7-day expiry)
   - Marks old Refresh Token as revoked
   ?
5. Client stores new tokens
```

### Authorization: Role-Based Access Control (RBAC)

The platform uses ASP.NET Identity roles with policy-based authorization:

```csharp
// In AppDbContext seeding:
// Creates roles: User, ServiceProvider, Admin

// In controllers:
[Authorize(Roles = "User")]
public async Task<IActionResult> CreateRequest(...)

[Authorize(Roles = "ServiceProvider")]
public async Task<IActionResult> SubmitBid(...)

[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetAuditLogs(...)
```

### Blazor AuthenticationStateProvider

The Blazor UI uses a custom `TokenAuthenticationStateProvider`:

```csharp
public class TokenAuthenticationStateProvider : AuthenticationStateProvider
{
    // Reads JWT from secure storage
    // Validates expiry
    // Notifies Blazor when auth state changes
    // Handles automatic token refresh
}
```

Components use `<AuthorizeView>` for role-based rendering:

```razor
<AuthorizeView Roles="User">
    <Authorized>
        <p>Hello, customer!</p>
    </Authorized>
    <NotAuthorized>
        <p>You need to be a customer to see this.</p>
    </NotAuthorized>
</AuthorizeView>
```

---

## Business Rules & Entities

### UserType Enum

Each user has a `UserType` that determines their platform capabilities:

```csharp
public enum UserType
{
    User = 1,           // Can post requests, hire providers
    ServiceProvider = 2, // Can browse requests, submit bids
    Both = 3,           // Can do both
    Admin = 4           // Platform administration
}
```

**Business Rules for UserType:**

- **Customer account requires**: Valid email, password, first/last name, date of birth
- **Provider account requires**: Age ? 18, valid government ID (optional)
- **Both account requires**: All of the above
- **Admin accounts**: Created manually by existing admins

### User Entity

```csharp
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; }                // Required
    public string LastName { get; set; }                 // Required
    public DateTime DateOfBirth { get; set; }            // Required, validated for age
    public string PhonePrimary { get; set; }             // Required
    public string PhoneSecondary { get; set; }           // Optional
    public UserType UserType { get; set; }               // User, Provider, Both, Admin
    public string GovernmentIdImagePath { get; set; }    // Optional, max 5MB image
    public DateTime CreatedAtUtc { get; set; }           // Audit trail
}
```

### ServiceRequest Entity

```csharp
public class ServiceRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; }                    // Service title
    public string Description { get; set; }              // Detailed description
    public string Category { get; set; }                 // Service category
    public string Location { get; set; }                 // Address or area
    public double Latitude { get; set; }                 // For geolocation
    public double Longitude { get; set; }                // For geolocation
    public string CustomerId { get; set; }               // FK to User who posted
    public ServiceRequestStatus Status { get; set; }     // Open, Accepted, Completed, Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<Bid> Bids { get; set; }           // Bids received
}
```

### Bid Entity

```csharp
public class Bid
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }           // FK to request
    public string ServiceProviderId { get; set; }        // FK to user
    public decimal Amount { get; set; }                  // Bid amount (18,2 precision)
    public DateTime ProposedDateTime { get; set; }       // When work can be done
    public string Message { get; set; }                  // Provider's bid message
    public BidStatus Status { get; set; }                // Pending, Accepted, Rejected
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Business Rules

| Rule | Implementation |
|------|---|
| Only customers (UserType.User or Both) can post requests | [Authorize(Roles = "User")] on controller |
| Only providers (UserType.ServiceProvider or Both) can bid | [Authorize(Roles = "ServiceProvider")] on controller |
| Providers must be 18+ years old | AgeValidator in Application layer |
| A provider can only bid once per request | BidService checks for duplicates |
| Only the customer can accept a bid | Authorization check in service |
| No sensitive data in JWT payload | Only claims: UserId, Email, Role, SessionId |

---

## Security Implementation

### Rate Limiting

Protects APIs from abuse by limiting requests per IP/user:

```csharp
// In Program.cs:
options.AddPolicy("auth", context =>
{
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: GetUserIdOrIpAddress(context),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1)
        }
    );
});

// Applied to endpoints:
[EnableRateLimiting("auth")]
[HttpPost("login")]
public async Task<IActionResult> Login(...)
```

### Retry-Safe Authentication

Login and registration are **idempotent** - safe to retry:

```csharp
// Login: Each call generates a new JWT
// Same credentials = different token each time
// Safe to retry on network failures

// Register: 
// - If user+role exists ? return 200 OK (idempotent)
// - No double-inserts, transaction-based atomicity
```

### Token Security

- **Access tokens** are short-lived (10 minutes) to limit exposure
- **Refresh tokens** are:
  - Hashed before storage (SHA256)
  - Rotated on each use (old token revoked)
  - Tracked with TokenFamily for attack detection
  - Stored with IssuedAt, ExpiresAt, RevokedAt timestamps
  
- **SessionId** (JWT `jti` claim):
  - Unique GUID per login
  - Used for audit trail tracking
  - Prevents session reuse attacks

### HTTPS & CORS

- **HTTPS**: Enforced in production (appsettings.Production.json)
- **CORS**: Whitelist specific origins, reject others
- **Security Headers**: Configured via SecurityHeadersMiddleware

### Audit Logging

All authentication events logged to `AuditLogs` table:

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; }              // Who
    public string EventType { get; set; }           // Login, Logout, SessionExpired
    public DateTime TimestampUtc { get; set; }      // When (UTC for consistency)
    public string SessionId { get; set; }           // Which session (JWT jti claim)
    public string Role { get; set; }                // User's role at the time
    public string IpAddress { get; set; }           // Where
    public string UserAgent { get; set; }           // What client
}
```

**Append-only pattern**: Never UPDATE audit records, only INSERT.

---

## Background Jobs

### Refresh Token Cleanup

**Purpose**: Delete expired refresh tokens to keep database clean

```csharp
public class RefreshTokenCleanupService : BackgroundService
{
    // Runs every 1 hour
    // Deletes tokens where ExpiresAt < DateTime.UtcNow
    // Logs count of tokens deleted
}
```

### Abandoned Session Cleanup

**Purpose**: Identify sessions inactive for 7+ days

```csharp
public class AbandonedSessionCleanupService : BackgroundService
{
    // Runs every 24 hours
    // Marks sessions as abandoned
    // Useful for analytics and fraud detection
}
```

### Audit Log Archival

**Purpose**: Archive old audit logs for compliance

```csharp
public class AuditLogArchivalService : BackgroundService
{
    // Configurable retention (default: 90 days)
    // Archives or deletes logs older than retention period
    // Supports regulatory compliance
}
```

### Safety Guarantees

- All services handle **graceful shutdown** via `CancellationToken`
- **Idempotent operations** - safe to run multiple times
- **Comprehensive logging** - all errors logged
- **No data loss** - backups created before deletion

---

## Testing Strategy

### Unit Tests

**NOT used** for authentication/authorization because:
- Tests would mock identity, losing real behavior verification
- Business logic depends heavily on EF Core/Identity behavior
- Mocking creates false confidence in integration

**Used for** validator and utility logic:
```csharp
[TestMethod]
public void AgeValidator_Under18_ShouldFail()
{
    var birthDate = DateTime.Now.AddYears(-17);
    var result = AgeValidator.ValidateAge(birthDate, UserType.ServiceProvider);
    Assert.IsFalse(result.IsValid);
}
```

### Integration Tests

**Comprehensive testing** with real database:

```csharp
[TestClass]
public class AuthenticationTests
{
    // Tests user registration with all validations
    // Tests login with valid/invalid credentials
    // Tests token refresh and rotation
    // Tests role-based access enforcement
    // Tests rate limiting
    
    // Uses real SQL Server (local or test container)
    // No mocking of auth internals
    // Verifies end-to-end flows
}
```

### What's Tested

? User registration (all validations)  
? Login success/failure  
? Token generation and structure  
? Token refresh and rotation  
? Role-based authorization  
? Rate limiting enforcement  
? Audit log creation  
? Bid workflows  
? Service request lifecycle  

### Running Tests

```bash
dotnet test
```

Tests run against local SQL Server instance (configured in test settings).

---

## Development Workflow

### Adding a New Feature

1. **Create domain entity** in `ServiceMarketplace.Domain`
2. **Create EF migration** in `ServiceMarketplace.Infrastructure`
3. **Create service interface** in `ServiceMarketplace.Application`
4. **Implement service** in `ServiceMarketplace.Infrastructure`
5. **Create controller** in `ServiceMarketplace.API`
6. **Create Razor components** in `ServiceMarketplace.UI.Shared`
7. **Add integration tests**

### Code Review Checklist

- [ ] Code follows clean architecture layers
- [ ] No circular dependencies
- [ ] UI components use `<AuthorizeView>` for access control
- [ ] API endpoints have `[Authorize]` attributes
- [ ] DTOs used for API requests/responses (never entities)
- [ ] All external inputs validated
- [ ] Sensitive data never logged
- [ ] Audit trail entries created for security events
- [ ] Tests pass: `dotnet test`
- [ ] Build succeeds: `dotnet build`

---

## Deployment Considerations

### Environment Configuration

**Development** (`appsettings.Development.json`):
- Local database connection
- JWT key (non-secret, dev-only)
- Detailed logging enabled
- CORS allows localhost

**Production** (`appsettings.Production.json`):
- Production database (RDS, Azure SQL, etc.)
- JWT key: Strong 256-bit secret
- Minimal logging (perf)
- CORS: Specific production origins only
- HTTPS: Required
- Rate limiting: Enforced
- Audit logging: Enabled

### Key Deployment Steps

1. Build: `dotnet build --configuration Release`
2. Test: `dotnet test`
3. Migrate: `dotnet ef database update --project Infrastructure`
4. Deploy API to app service / container / VM
5. Deploy UI (static files) to storage / CDN
6. Verify `/swagger` and `/health` endpoints
7. Monitor Application Insights
8. Review audit logs regularly

### Monitoring

Key metrics to track:

- **API response times** (p50, p95, p99)
- **Error rate** (should be < 1%)
- **Rate limit hits** (indicates attack attempt or misconfiguration)
- **Failed login attempts** (brute force detection)
- **Token refresh rate** (normal usage pattern)
- **Database connection pool** (capacity planning)

---

## Common Patterns

### Creating a Service

```csharp
// 1. Define interface in Application layer
public interface IYourService
{
    Task<YourDto> GetAsync(string id);
}

// 2. Implement in Infrastructure layer
public class YourService : IYourService
{
    private readonly AppDbContext _context;
    private readonly ILogger<YourService> _logger;
    
    public YourService(AppDbContext context, ILogger<YourService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<YourDto> GetAsync(string id)
    {
        var entity = await _context.YourEntities.FindAsync(id);
        if (entity == null)
            throw new NotFoundException("Entity not found");
        
        _logger.LogInformation("Retrieved entity: {Id}", id);
        return new YourDto { /* map */ };
    }
}

// 3. Register in Program.cs
builder.Services.AddScoped<IYourService, YourService>();

// 4. Use in controller
[ApiController]
public class YourController
{
    private readonly IYourService _service;
    
    public YourController(IYourService service) => _service = service;
    
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var result = await _service.GetAsync(id);
        return Ok(result);
    }
}
```

### Validating Input

```csharp
// Use FluentValidation in Application layer
public class CreateYourDtoValidator : AbstractValidator<CreateYourDto>
{
    public CreateYourDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
        
        RuleFor(x => x.Email)
            .EmailAddress();
    }
}

// Auto-validate in service layer
var validator = new CreateYourDtoValidator();
var validation = await validator.ValidateAsync(dto);
if (!validation.IsValid)
    throw new BadRequestException(validation.Errors[0].ErrorMessage);
```

### Role-Based Authorization

```razor
@* Component in UI.Shared *@
@if (user.UserType == UserType.User || user.UserType == UserType.Both)
{
    <p>You can post service requests</p>
}

@if (user.UserType == UserType.ServiceProvider || user.UserType == UserType.Both)
{
    <p>You can submit bids</p>
}
```

---

## Glossary

| Term | Meaning |
|------|---------|
| **JWT** | JSON Web Token - stateless auth token |
| **SessionId** | Unique ID for each login (JWT `jti` claim) |
| **RBAC** | Role-Based Access Control |
| **DTO** | Data Transfer Object - safe data structure for APIs |
| **Idempotent** | Safe to retry without side effects |
| **Token Rotation** | Issuing new token and revoking old one |
| **Audit Trail** | Immutable log of all security events |
| **Clean Architecture** | Layered design with clear separation of concerns |

---

## References

- [Microsoft Clean Architecture](https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)

---

**Last Updated**: February 2025 | **Audience**: Engineering Team
