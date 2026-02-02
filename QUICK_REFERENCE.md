# Session Expiry Audit Logging - Quick Reference

## The Problem Solved

**Before**: When a user's 10-minute JWT session expired, there was no audit log entry. You couldn't tell if a user was logged out by the system due to timeout or if they did something else.

**After**: Exactly one `SessionExpired` audit log entry is created when the 10-minute timer expires on the client.

## The Solution (High-Level)

```
User logged in 10 minutes ago
         ?
Client detects token expired (timer fires)
         ?
Client POSTs expired token to: POST /api/auth/token-expired
         ?
Backend extracts SessionId from token
         ?
Backend checks: Is there already a SessionExpired entry for this SessionId?
         ?? YES: Skip (prevents duplicates) ?
         ?? NO: Record new SessionExpired entry ?
         ?
User logged out in UI (regardless of backend success/failure)
```

## Code Entry Points

### When Writing Code That Needs Session State

If you need to check if a user's session expired vs manual logout:

```csharp
// In your audit analysis code:
var auditLog = await dbContext.AuditLogs
    .FirstOrDefaultAsync(a => 
        a.SessionId == sessionId && 
        a.EventType == "SessionExpired"  // This is what we're tracking now
    );

if (auditLog != null)
{
    // User's session expired automatically after 10 minutes
    Console.WriteLine($"Session expired at {auditLog.TimestampUtc}");
}
else
{
    // Check for Logout event instead
    var logoutLog = await dbContext.AuditLogs
        .FirstOrDefaultAsync(a => 
            a.SessionId == sessionId && 
            a.EventType == "Logout"
        );
    
    if (logoutLog != null)
    {
        Console.WriteLine($"User logged out at {logoutLog.TimestampUtc}");
    }
}
```

### Handling Session Expiry in Components

```csharp
// In any Blazor component:
@using Microsoft.AspNetCore.Components.Authorization
@inject AuthenticationStateProvider AuthenticationStateProvider

@code {
    protected override async Task OnInitializedAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        
        if (!state.User.Identity?.IsAuthenticated ?? false)
        {
            // User is not authenticated
            // Could be due to:
            // - Just logged out manually
            // - Session expired (TokenAuthenticationStateProvider detected it)
            // - Token invalid
            
            // No code needed - TokenAuthenticationStateProvider already 
            // sent the session expiry notification to backend automatically
        }
    }
}
```

## Configuration Needed

None! Everything is already configured in Program.cs.

The only things that matter:
- HttpClient is registered with correct API base URL ?
- TokenAuthenticationStateProvider is registered ?
- AuthService is registered ?

## Monitoring & Alerts

### Key Metrics to Track

```csharp
// How many sessions expired today?
var expiredCount = await dbContext.AuditLogs
    .CountAsync(a => 
        a.EventType == "SessionExpired" &&
        a.TimestampUtc > DateTime.UtcNow.AddDays(-1)
    );

// How many users?
var expiredUsers = await dbContext.AuditLogs
    .Where(a => 
        a.EventType == "SessionExpired" &&
        a.TimestampUtc > DateTime.UtcNow.AddDays(-1)
    )
    .Select(a => a.UserId)
    .Distinct()
    .CountAsync();

// Ratio of manual logout to automatic expiry
var logoutCount = await dbContext.AuditLogs
    .CountAsync(a => 
        a.EventType == "Logout" &&
        a.TimestampUtc > DateTime.UtcNow.AddDays(-1)
    );

Console.WriteLine($"Sessions expired: {expiredCount}");
Console.WriteLine($"Sessions logged out: {logoutCount}");
Console.WriteLine($"Ratio: {(double)expiredCount / (logoutCount + expiredCount):P}");
```

## Troubleshooting Checklist

| Issue | Cause | Solution |
|-------|-------|----------|
| SessionExpired not appearing in logs | Timer hasn't fired yet | Wait 10 minutes from login |
| SessionExpired not appearing | Network error during notification | Check browser console for errors |
| SessionExpired not appearing | Backend error | Check Application Insights or server logs for /api/auth/token-expired errors |
| Multiple SessionExpired for same session | Bug in duplicate prevention | Verify SessionId index exists on AuditLogs table |
| Logout appearing instead of SessionExpired | User clicked logout before 10 min | Verify actual logout time vs session creation time |
| 0 SessionExpired entries ever | Deprecated JWT not using 'jti' claim | Verify AuthService.LoginAsync() creates SessionId in JWT claims |

## Testing Commands

### SQL Server

```sql
-- See all session expirations (last 24 hours)
SELECT TOP 100 * FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(HOUR, -24, GETUTCDATE())
ORDER BY TimestampUtc DESC;

-- See timeline for one user's session
SELECT EventType, TimestampUtc FROM AuditLogs
WHERE SessionId = 'YOUR-SESSION-ID'
ORDER BY TimestampUtc ASC;

-- Verify no duplicates exist (should return 0 rows)
SELECT SessionId, COUNT(*) as Count
FROM AuditLogs
WHERE EventType = 'SessionExpired'
GROUP BY SessionId
HAVING COUNT(*) > 1;
```

### Browser DevTools

```javascript
// Watch for the token expiry notification being sent
// Open DevTools ? Network tab
// Wait for request to POST /api/auth/token-expired
// Should see 200 OK response after ~10 minutes

// Check token expiration in browser console
const token = localStorage.getItem('token');  // Or sessionStorage
const payload = JSON.parse(atob(token.split('.')[1]));
console.log('Expires:', new Date(payload.exp * 1000));
```

## API Specification

### Endpoint: POST /api/auth/token-expired

**Purpose**: Notify backend that token has expired on client

**Authentication**: None required (token is already expired)

**Request Body**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Success Response** (200 OK):
```json
{
  "message": "Token expiry recorded"
}
```

**Error Response** (400 Bad Request):
```json
"Token is required"
```

**Headers**:
- Content-Type: application/json
- X-Platform: Web (auto-added by client)
- User-Agent: (auto-added by browser)

**Timeout**: 2 seconds (silently fails if exceeded)

## Related Events in AuditLog

| EventType | Triggered By | Requires Auth | Example |
|-----------|--------------|---------------|---------|
| `Login` | User enters credentials | No | POST /api/auth/login succeeds |
| `Logout` | User clicks logout button | Yes ([Authorize]) | POST /api/auth/logout with valid token |
| `SessionExpired` | 10-minute timer fires | No | POST /api/auth/token-expired with expired token |

## Common Queries

### All events for a user (past 24 hours)
```sql
SELECT EventType, TimestampUtc, SessionId
FROM AuditLogs
WHERE UserId = 'user-id'
  AND TimestampUtc > DATEADD(HOUR, -24, GETUTCDATE())
ORDER BY TimestampUtc DESC;
```

### Sessions that expired (not logged out)
```sql
SELECT DISTINCT SessionId, UserId, TimestampUtc
FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(DAY, -7, GETUTCDATE())
ORDER BY TimestampUtc DESC;
```

### Users with unusual patterns (many expirations)
```sql
SELECT UserId, COUNT(DISTINCT SessionId) as SessionCount
FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(DAY, -1, GETUTCDATE())
GROUP BY UserId
HAVING COUNT(DISTINCT SessionId) > 10
ORDER BY SessionCount DESC;
```

## Performance Impact

- **Client**: Negligible - background async operation with 2-second timeout
- **Server**: ~50ms per session expiry (one DB lookup + one insert)
- **Database**: Fast due to SessionId index on AuditLogs

## Security Impact

**This feature does NOT change authentication security.**

- No new vulnerabilities introduced
- Expired tokens cannot be used for authorization
- SessionId is already in JWT (not new)
- Backend duplicate check prevents abuse
- No sensitive data exposed

## What You Don't Need To Do

- ? Don't manually call AuthService.HandleTokenExpiredAsync() - it's automatic
- ? Don't check if SessionId exists before calling - the backend does
- ? Don't implement your own expiry detection - TokenAuthenticationStateProvider does it
- ? Don't modify JWT claims - SessionId (jti) is already there
- ? Don't add authorization attributes to /token-expired - it won't work

## What The System Guarantees

? **One entry per session expiration** - Duplicate check in backend
? **Distinct from manual logout** - Different EventType value
? **Works with multiple tabs** - Automatic deduplication
? **Works offline** - User logs out locally regardless of backend
? **No performance impact** - Background operation with short timeout
? **Completely transparent** - No code changes needed in existing components

---

**For detailed information, see: SESSION_EXPIRY_AUDIT_LOGGING.md**
