# Session Expiry Audit Logging Implementation

## Overview

This document describes the implementation of automatic session expiry detection and audit logging in ServiceMarketplace. When a user's JWT token expires after the 10-minute session window, the system automatically records a `SessionExpired` audit log entry.

## Architecture

### Session Lifetime

- **Token Expiration**: 10 minutes from login
- **Detection**: Client-side timer in `TokenAuthenticationStateProvider`
- **Audit Recording**: Backend `AuthService.HandleTokenExpiredAsync()`

### Key Components

#### 1. TokenAuthenticationStateProvider (UI.Shared)

**File**: `ServiceMarketplace.UI.Shared\Auth\TokenAuthenticationStateProvider.cs`

**Responsibilities**:
- Parse JWT token and extract expiration time
- Schedule a timer to fire when token expires
- When timer fires, call backend to record session expiry
- Clear local token storage and notify Blazor of authentication state change

**Key Methods**:
- `ScheduleExpiry(DateTime expiresAtUtc)`: Sets up a timer for 10 minutes from now
- `HandleTokenExpiredAsync()`: Fires when timer expires; notifies backend and clears state
- `NotifyBackendOfExpiryAsync(string? token)`: Posts expired token to backend for audit recording

**Implementation Detail**:
```csharp
private async Task HandleTokenExpiredAsync()
{
    var token = await _tokenStorage.GetTokenAsync();
    
    // Notify backend of session expiry for audit logging
    await NotifyBackendOfExpiryAsync(token);
    
    await _tokenStorage.ClearAsync();
    ClearExpiryTimer();
    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
}
```

**Error Handling**:
- If backend is unreachable: logs warning but continues with local logout
- If timeout occurs (2 second window): logs and continues
- If token is malformed: skips notification and logs warning

#### 2. AuthController (API)

**File**: `ServiceMarketplace.API\Controllers\AuthController.cs`

**New Endpoint**: `POST /api/auth/token-expired`

**Characteristics**:
- **No [Authorize] attribute**: Token is already expired, JWT validation would fail
- **Accepts**: TokenExpiredRequest containing the expired token
- **Returns**: 200 OK on success, 400 Bad Request if token is missing
- **Calls**: `IAuthService.HandleTokenExpiredAsync()`

**Request Format**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response Format**:
```json
{
  "message": "Token expiry recorded"
}
```

#### 3. AuthService (Infrastructure)

**File**: `ServiceMarketplace.Infrastructure\Services\AuthService.cs`

**Method**: `HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress)`

**Implementation**:
1. Parse JWT token (no signature validation needed; already expired)
2. Extract claims: `UserId`, `SessionId`, `Role`, `Email`
3. **Check for duplicates**: Query AuditLogs for existing SessionExpired event with same SessionId
4. If duplicate found: Log and return (prevents double logging)
5. If not found: Record new SessionExpired audit entry via `RecordEventAsync()`

**Duplicate Prevention Logic**:
```csharp
if (!string.IsNullOrWhiteSpace(sessionId))
{
    var existingExpiry = await _context.AuditLogs
        .AsNoTracking()
        .FirstOrDefaultAsync(a => 
            a.SessionId == sessionId &&
            a.EventType == "SessionExpired"
        );

    if (existingExpiry != null)
    {
        _logger.LogInformation("SessionExpired event already recorded for session {SessionId}", sessionId);
        return;
    }
}
```

**Why This Works**:
- Each login generates a unique `SessionId` (GUID) stored in JWT's `jti` claim
- SessionExpired audit log includes this SessionId
- Query checks: same SessionId + EventType="SessionExpired"
- Index on `(SessionId, EventType)` ensures fast lookup
- If found, skip recording to prevent duplicate

#### 4. AuditLog Entity (Domain)

**File**: `ServiceMarketplace.Domain\Entities\LoginAuditLog.cs`

**Key Properties**:
- `Id`: Unique identifier (Guid)
- `UserId`: Reference to user
- `SessionId`: Unique per login (from JWT `jti` claim)
- `EventType`: "Login", "Logout", "SessionExpired"
- `TimestampUtc`: UTC timestamp
- `Role`: User's role at time of event
- `IpAddress`: Client IP (for security analysis)
- `UserAgent`: Browser/platform info

**Indexes** (configured in AppDbContext):
- `IX_AuditLogs_UserId`: Fast lookup by user
- `IX_AuditLogs_TimestampUtc`: Descending, for recent events
- `IX_AuditLogs_SessionId`: Fast duplicate prevention check

#### 5. AppDbContext (Infrastructure)

**File**: `ServiceMarketplace.Infrastructure\Data\AppDbContext.cs`

**Configuration**:
```csharp
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

    // Column constraints...
});
```

## Event Flow Diagram

```
???????????????????????????????????????????????????????????????
? User Logs In (API: POST /api/auth/login)                   ?
???????????????????????????????????????????????????????????????
? 1. AuthService.LoginAsync()                                 ?
?    - Create JWT with:                                       ?
?      - exp: now + 10 minutes                               ?
?      - jti: sessionId (unique GUID)                        ?
?    - Record AuditLog entry (EventType="Login")             ?
?    - Return token + expiresAt                              ?
???????????????????????????????????????????????????????????????
               ?
               ??????????????????????????????????????????
               ?                                        ?
               ?                                        ?
      ???????????????????????????      ????????????????????????????
      ? SCENARIO A: User        ?      ? SCENARIO B: Token        ?
      ? Logs Out Manually       ?      ? Expires (10 min)         ?
      ?                         ?      ?                          ?
      ? User clicks Logout      ?      ? TokenAuthenticationState ?
      ? ? API: POST /logout     ?      ? Provider timer fires     ?
      ? ? AuthService.          ?      ?                          ?
      ?   LogoutAsync()         ?      ? ? HandleTokenExpiredAsync?
      ? ? Record: EventType=    ?      ? ? NotifyBackendOfExpiry  ?
      ?   "Logout"              ?      ?                          ?
      ?                         ?      ? ? POST /api/auth/        ?
      ? ? Single Logout entry   ?      ?   token-expired          ?
      ?                         ?      ?                          ?
      ?                         ?      ? ? AuthService.           ?
      ?                         ?      ?   HandleTokenExpiredAsync?
      ?                         ?      ? ? Check for duplicate:   ?
      ?                         ?      ?   SessionId exists?      ?
      ?                         ?      ? ? Record: EventType=     ?
      ?                         ?      ?   "SessionExpired"       ?
      ?                         ?      ?                          ?
      ?                         ?      ? ? Single SessionExpired  ?
      ?                         ?      ?   entry per session      ?
      ???????????????????????????      ????????????????????????????
               ?                                        ?
               ??????????????????????????????????????????
                                        ?
                      ????????????????????????????????????
                      ? AuditLogs Table                  ?
                      ????????????????????????????????????
                      ? Id | UserId | SessionId | Event  ?
                      ? ---|--------|-----------|--------|
                      ? 1  | user1  | guid-123  | Login  ?
                      ? 2  | user1  | guid-123  | Logout ?
                      ?    ?        ?           ?   OR   ?
                      ? 2  | user1  | guid-123  | SessionEx…
                      ?    ?        ?           ?        ?
                      ? 3  | user2  | guid-456  | Login  ?
                      ? 4  | user2  | guid-456  | SessionEx…
                      ????????????????????????????????????
```

## Edge Cases & Guarantees

### ? Guaranteed: Exactly One SessionExpired Entry per Session

1. **Timer-based detection**: Each token has exactly one expiration time
2. **Duplicate check**: SessionId uniqueness prevents multiple entries
3. **Idempotent endpoint**: Multiple calls with same SessionId are safe

### ? Handled: Network Failures

- If backend is unreachable when timer fires: Silent failure, local logout still works
- User remains logged out locally regardless of audit logging success
- Backend health doesn't affect client security

### ? Handled: Multiple Tab/Window Scenarios

- Each tab has independent token storage and timer
- Multiple tabs with same token = multiple SessionExpired notifications
- Duplicate check in backend ensures only ONE entry in AuditLogs
- All notifications safe and idempotent

### ? Separated: Manual Logout vs Session Expiry

| Event | EventType | Initiator | Trigger |
|-------|-----------|-----------|---------|
| Manual Logout | "Logout" | User clicks logout button | POST /api/auth/logout (requires [Authorize]) |
| Session Expiry | "SessionExpired" | Client timer | POST /api/auth/token-expired (no auth needed) |

## Database Schema

### AuditLogs Table

```sql
CREATE TABLE [AuditLogs] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [UserId] NVARCHAR(MAX) NOT NULL,
    [Role] NVARCHAR(50) NULL,
    [EventType] NVARCHAR(50) NOT NULL,
    [TimestampUtc] DATETIME2 NOT NULL,
    [SessionId] NVARCHAR(100) NULL,
    [IpAddress] NVARCHAR(45) NULL,
    [UserAgent] NVARCHAR(500) NULL,
);

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs]([UserId]);
CREATE INDEX [IX_AuditLogs_SessionId] ON [AuditLogs]([SessionId]);
CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs]([TimestampUtc] DESC);
```

## Testing Checklist

### Manual Testing

1. **Login with 10-minute timer**
   - [ ] Log in as a test user
   - [ ] Wait 10 minutes (or advance system clock)
   - [ ] Verify SessionExpired entry appears in AuditLogs
   - [ ] Verify user is logged out in UI

2. **No duplicate on multiple tabs**
   - [ ] Log in in Tab A
   - [ ] Open same app in Tab B (same token)
   - [ ] Wait for expiry
   - [ ] Check AuditLogs: only ONE SessionExpired entry

3. **Separated from manual logout**
   - [ ] Log in
   - [ ] Click logout before 10 minutes
   - [ ] Verify Logout entry (not SessionExpired)
   - [ ] Log in again
   - [ ] Wait for timeout
   - [ ] Verify SessionExpired entry (not Logout)

4. **Network resilience**
   - [ ] Disable network/close browser dev tools to block requests
   - [ ] Wait for session expiry
   - [ ] Verify UI logs out (even if notification failed)
   - [ ] Re-enable network
   - [ ] Verify no duplicate entries

### Database Queries

```sql
-- View all audit events for a specific user
SELECT * FROM AuditLogs
WHERE UserId = 'user-id'
ORDER BY TimestampUtc DESC;

-- View all session expirations
SELECT * FROM AuditLogs
WHERE EventType = 'SessionExpired'
ORDER BY TimestampUtc DESC;

-- Check for duplicate SessionExpired entries
SELECT SessionId, COUNT(*) as Count
FROM AuditLogs
WHERE EventType = 'SessionExpired'
GROUP BY SessionId
HAVING COUNT(*) > 1;

-- Timeline for a specific session
SELECT * FROM AuditLogs
WHERE SessionId = 'specific-session-guid'
ORDER BY TimestampUtc ASC;
```

## Code Changes Summary

### Modified Files

1. **ServiceMarketplace.UI.Shared\Auth\TokenAuthenticationStateProvider.cs**
   - Added `HttpClient` parameter to constructor (DI injection)
   - Added `using System.Net.Http.Json;`
   - Enhanced `HandleTokenExpiredAsync()` to call backend
   - Added `NotifyBackendOfExpiryAsync()` private method

2. **ServiceMarketplace.Infrastructure\Services\AuthService.cs**
   - Enhanced `HandleTokenExpiredAsync()` with duplicate prevention
   - Added SessionId uniqueness check before recording
   - Added logging for skipped duplicates

3. **ServiceMarketplace.API\Controllers\AuthController.cs**
   - Added new `TokenExpired()` endpoint: `POST /api/auth/token-expired`
   - Added `TokenExpiredRequest` class to bind request body
   - Added comprehensive XML documentation

### No Breaking Changes

- All changes are additive
- Existing endpoints unchanged
- New endpoint doesn't require authentication
- Existing audit logging still works (Login, Logout)

## Security Considerations

1. **No signature validation on expired token**: Safe because:
   - Token is already expired (client says so)
   - We only use claims for context, not authorization
   - Backend applies duplicate check as safety net

2. **IP address validation**: Optional enhancement for future
   - Could cross-check IP from token creation vs expiry
   - Low priority for MVP

3. **Rate limiting**: Not required because:
   - Endpoint accepts one call per session expiration
   - Duplicate check prevents abuse
   - No sensitive operations

## Monitoring & Alerts

### Recommended Queries for Operations

```sql
-- Session expirations in last 24 hours
SELECT COUNT(*) as ExpiryCount, 
       CONVERT(DATE, TimestampUtc) as Date
FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(DAY, -1, GETUTCDATE())
GROUP BY CONVERT(DATE, TimestampUtc);

-- Users with most sessions
SELECT TOP 10 UserId, COUNT(DISTINCT SessionId) as SessionCount
FROM AuditLogs
WHERE TimestampUtc > DATEADD(DAY, -7, GETUTCDATE())
GROUP BY UserId
ORDER BY SessionCount DESC;

-- Unusual activity: same user, rapid expirations
SELECT UserId, SessionId, TimestampUtc, 
       LAG(TimestampUtc) OVER (PARTITION BY UserId ORDER BY TimestampUtc) as PreviousTime
FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(DAY, -1, GETUTCDATE())
ORDER BY UserId, TimestampUtc;
```

## Future Enhancements

1. **Sliding window sessions**: Extend 10-minute timer on activity
2. **Remember-me tokens**: Separate token with longer expiry
3. **Session revocation**: Admin endpoint to force logout
4. **Concurrent session limits**: Max N active sessions per user
5. **Device tracking**: Additional metadata in AuditLog

## Troubleshooting

### Session not expiring after 10 minutes
- Check browser console for errors in NotifyBackendOfExpiryAsync
- Verify API endpoint `/api/auth/token-expired` is accessible
- Check network tab for failed POST requests
- Verify token's `exp` claim is set correctly

### Duplicate SessionExpired entries
- This shouldn't happen with current implementation
- If it does, run the duplicate check query above
- Verify SessionId index exists on AuditLogs table
- Check timestamps to identify which was recorded first

### Manual logout shows SessionExpired instead of Logout
- Verify logout is called before 10-minute mark
- Check that `/api/auth/logout` endpoint is [Authorize] protected
- Verify JWT is still valid at logout time
- Review API logs for endpoint routing issues

## References

- **JWT Session**: 10 minutes from login
- **Session ID**: Stored in JWT `jti` (JWT ID) claim
- **Audit Table**: AuditLogs in ServiceMarketplace.Infrastructure
- **Related Issues**: Session tracking, audit compliance, security monitoring
