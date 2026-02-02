# Audit Logging Quick Reference

## ?? Quick Access

### New Files Created
1. `ServiceMarketplace.Application\Interfaces\IAuditLogService.cs` - Service interface
2. `ServiceMarketplace.Infrastructure\Services\AuditLogService.cs` - Service implementation
3. `AUDIT_LOGGING_CENTRALIZATION.md` - Detailed implementation summary
4. `AUDIT_LOGGING_VERIFICATION.md` - Flow verification and metrics

### Modified Files
1. `ServiceMarketplace.Infrastructure\Services\AuthService.cs` - Uses IAuditLogService
2. `ServiceMarketplace.API\Program.cs` - Registers AuditLogService in DI

---

## ?? How to Use AuditLogService

### 1. Inject the Service
```csharp
public class MyService
{
    private readonly IAuditLogService _auditLogService;
    
    public MyService(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }
}
```

### 2. Log Login Event
```csharp
await _auditLogService.LogLoginAsync(
    userId: "user123",
    role: "User",
    sessionId: "session-guid",
    ipAddress: "192.168.1.1",
    userAgent: "Mozilla/5.0"
);
```

### 3. Log Logout Event
```csharp
await _auditLogService.LogLogoutAsync(
    userId: "user123",
    role: "User",
    sessionId: "session-guid",
    ipAddress: "192.168.1.1",
    userAgent: "Mozilla/5.0"
);
```

### 4. Log Session Expired Event
```csharp
bool logged = await _auditLogService.LogSessionExpiredAsync(
    userId: "user123",
    role: "User",
    sessionId: "session-guid",
    ipAddress: "192.168.1.1",
    userAgent: "Mozilla/5.0"
);

// logged = false if duplicate detected
```

---

## ?? How to Test

### Unit Test Example
```csharp
[Fact]
public async Task MyMethod_LogsAuditEvent()
{
    // Arrange
    var mockAuditService = new Mock<IAuditLogService>();
    var service = new MyService(mockAuditService.Object);
    
    // Act
    await service.DoSomething();
    
    // Assert
    mockAuditService.Verify(
        x => x.LogLoginAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()
        ),
        Times.Once
    );
}
```

---

## ?? Do NOT

? Write audit logs directly in controllers
```csharp
// BAD - Do NOT do this
_context.AuditLogs.Add(new AuditLog { ... });
await _context.SaveChangesAsync();
```

? Write audit logs directly in services
```csharp
// BAD - Do NOT do this
public class MyService
{
    private readonly AppDbContext _context;
    
    public async Task DoSomething()
    {
        _context.AuditLogs.Add(...); // ? Wrong!
    }
}
```

? Duplicate audit logic
```csharp
// BAD - Do NOT do this
private async Task RecordEventAsync(...) // ? Already in AuditLogService
{
    _context.AuditLogs.Add(...);
}
```

---

## ? Do

? Use IAuditLogService interface
```csharp
// GOOD
public class MyService
{
    private readonly IAuditLogService _auditLogService;
    
    public async Task DoSomething()
    {
        await _auditLogService.LogLoginAsync(...); // ? Correct!
    }
}
```

? Register service in DI
```csharp
// GOOD
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
```

? Mock interface in tests
```csharp
// GOOD
var mockAuditService = new Mock<IAuditLogService>();
```

---

## ?? Audit Event Types

| Event Type       | When to Use                           | Duplicate Check |
|------------------|---------------------------------------|-----------------|
| Login            | User successfully authenticates       | No              |
| Logout           | User explicitly logs out             | No              |
| SessionExpired   | JWT token expires (10-minute timeout)| Yes (by SessionId) |

---

## ?? Database Schema

### AuditLogs Table
```sql
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    Role NVARCHAR(50) NULL,
    EventType NVARCHAR(50) NOT NULL,      -- 'Login', 'Logout', 'SessionExpired'
    TimestampUtc DATETIME2 NOT NULL,      -- Always UTC
    SessionId NVARCHAR(100) NULL,         -- From JWT jti claim
    IpAddress NVARCHAR(45) NULL,          -- IPv4 or IPv6
    UserAgent NVARCHAR(500) NULL          -- HTTP User-Agent header
);

-- Indexes
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
CREATE INDEX IX_AuditLogs_SessionId ON AuditLogs(SessionId);
CREATE INDEX IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc DESC);
```

---

## ?? Related Documentation

- `AUDIT_LOGGING_CENTRALIZATION.md` - Full implementation details
- `AUDIT_LOGGING_VERIFICATION.md` - Flow verification and metrics
- `SESSION_EXPIRY_AUDIT_LOGGING.md` - Session expiry details
- `AUTHENTICATION_FIXES_SUMMARY.md` - Authentication implementation

---

## ?? Benefits

1. **Single Source of Truth** - All audit logic in one place
2. **Testability** - Easy to mock IAuditLogService
3. **Maintainability** - Changes in one location
4. **Consistency** - All logs follow same pattern
5. **Extensibility** - Easy to add new event types

---

## ?? Adding New Audit Event Types

### 1. Add Method to Interface
```csharp
// ServiceMarketplace.Application\Interfaces\IAuditLogService.cs
Task LogPasswordChangeAsync(string userId, string? role, string? ipAddress, string? userAgent);
```

### 2. Implement Method
```csharp
// ServiceMarketplace.Infrastructure\Services\AuditLogService.cs
public async Task LogPasswordChangeAsync(string userId, string? role, string? ipAddress, string? userAgent)
{
    await RecordEventAsync(userId, role, "PasswordChange", null, ipAddress, userAgent);
    _logger.LogInformation("PasswordChange event recorded for user {UserId}", userId);
}
```

### 3. Use in Service
```csharp
// Anywhere in your services
await _auditLogService.LogPasswordChangeAsync(userId, role, ipAddress, userAgent);
```

---

**Last Updated:** 2026-02-01  
**Status:** ? Production Ready  
**Build:** ? Successful
