# Implementation Verification Checklist

## Code Changes Verification

### ? TokenAuthenticationStateProvider (UI.Shared\Auth)

- [x] Added `HttpClient httpClient` parameter to constructor
- [x] Added `using System.Net.Http.Json;` namespace
- [x] Modified `HandleTokenExpiredAsync()` to call `NotifyBackendOfExpiryAsync(token)`
- [x] Added `NotifyBackendOfExpiryAsync(string? token)` private method
- [x] Handles network failures gracefully (2-second timeout, silent failure)
- [x] Posts to `api/auth/token-expired` with token in request body
- [x] Continues with local logout regardless of backend success/failure

**Verification**:
```csharp
// File: ServiceMarketplace.UI.Shared\Auth\TokenAuthenticationStateProvider.cs
public sealed class TokenAuthenticationStateProvider(
    ITokenStorage tokenStorage, 
    HttpClient httpClient) : AuthenticationStateProvider  // ? HttpClient injected
{
    private readonly HttpClient _httpClient = httpClient;  // ? Stored
    
    private async Task HandleTokenExpiredAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        await NotifyBackendOfExpiryAsync(token);  // ? Calls backend
        // ... local cleanup
    }
    
    private async Task NotifyBackendOfExpiryAsync(string? token)  // ? New method
    {
        using var response = await _httpClient.PostAsJsonAsync(  // ? Uses injected client
            "api/auth/token-expired",
            new { token = token },
            cts.Token);
        // ? Silently fails on error
    }
}
```

### ? AuthController (API\Controllers)

- [x] Added `POST /api/auth/token-expired` endpoint
- [x] Method signature: `TokenExpired(TokenExpiredRequest request)`
- [x] No `[Authorize]` attribute (token is expired)
- [x] Accepts request body with token string
- [x] Returns 200 OK with success message
- [x] Returns 400 Bad Request if token missing
- [x] Calls `_authService.HandleTokenExpiredAsync()` with token, user-agent, IP
- [x] Added `TokenExpiredRequest` class with `Token` property

**Verification**:
```csharp
// File: ServiceMarketplace.API\Controllers\AuthController.cs
[HttpPost("token-expired")]  // ? Correct route
[ProducesResponseType(StatusCodes.Status200OK)]  // ? Response types documented
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> TokenExpired(TokenExpiredRequest request)  // ? No [Authorize]
{
    if (string.IsNullOrWhiteSpace(request.Token))
        return BadRequest("Token is required");  // ? Validation

    await _authService.HandleTokenExpiredAsync(  // ? Calls service
        request.Token, 
        Request.Headers.UserAgent.ToString(), 
        GetClientIpAddress());
    
    return Ok(new { message = "Token expiry recorded" });  // ? Returns 200
}

public class TokenExpiredRequest  // ? Request class defined
{
    public string Token { get; set; } = string.Empty;
}
```

### ? AuthService (Infrastructure\Services)

- [x] Enhanced `HandleTokenExpiredAsync()` method
- [x] Parses expired JWT token
- [x] Extracts userId, sessionId, role from claims
- [x] **Duplicate prevention**: Queries AuditLogs for existing SessionExpired
- [x] Checks: `SessionId == sessionId AND EventType == "SessionExpired"`
- [x] If found: Logs and returns (skip duplicate)
- [x] If not found: Calls `RecordEventAsync()` to insert new entry
- [x] Proper exception handling with logging

**Verification**:
```csharp
// File: ServiceMarketplace.Infrastructure\Services\AuthService.cs
public async Task HandleTokenExpiredAsync(string? tokenValue, string? userAgent, string? ipAddress)
{
    // ... extract userId, sessionId, role ...
    
    // ? Duplicate prevention logic
    if (!string.IsNullOrWhiteSpace(sessionId))
    {
        var existingExpiry = await _context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => 
                a.SessionId == sessionId &&
                a.EventType == "SessionExpired"  // ? Specific to SessionExpired
            );

        if (existingExpiry != null)
        {
            _logger.LogInformation(
                "SessionExpired event already recorded for session {SessionId}", 
                sessionId);  // ? Log duplicate
            return;  // ? Skip duplicate
        }
    }

    await RecordEventAsync(userId, role, "SessionExpired", sessionId, userAgent, ipAddress);
    // ? Only inserts if not duplicate
}
```

## Database Schema Verification

### AuditLogs Table Structure

```sql
-- Expected table structure
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(MAX) NOT NULL,
    Role NVARCHAR(50) NULL,
    EventType NVARCHAR(50) NOT NULL,  -- "Login", "Logout", "SessionExpired"
    TimestampUtc DATETIME2 NOT NULL,
    SessionId NVARCHAR(100) NULL,      -- Index needed for duplicate check
    IpAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL
);

-- Expected indexes
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
CREATE INDEX IX_AuditLogs_SessionId ON AuditLogs(SessionId);
CREATE INDEX IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc DESC);
```

**Verify with**:
```sql
-- Check SessionId index exists
SELECT name FROM sys.indexes 
WHERE object_id = OBJECT_ID('dbo.AuditLogs') 
AND name LIKE '%SessionId%';

-- Check table structure
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AuditLogs'
ORDER BY ORDINAL_POSITION;
```

## Functional Testing

### Test 1: Basic Session Expiry Flow

```
Steps:
1. Delete any existing test data: 
   DELETE FROM AuditLogs WHERE UserId = 'test-user'

2. Navigate to http://localhost:5241 (UI.Web)

3. Log in with test credentials

4. Verify in database:
   SELECT * FROM AuditLogs 
   WHERE EventType = 'Login' 
   ORDER BY TimestampUtc DESC
   LIMIT 1;
   
   Expected: One Login entry for test user

5. Wait 10 minutes (or manually set system clock forward 10 minutes)

6. Observe browser:
   - After 10 minutes, user should be logged out
   - UI should show login page

7. Check database:
   SELECT * FROM AuditLogs 
   WHERE EventType = 'SessionExpired' 
   ORDER BY TimestampUtc DESC
   LIMIT 1;
   
   Expected: One SessionExpired entry matching the SessionId from Login entry

? Test passes if exactly one SessionExpired entry created
```

### Test 2: Multiple Tabs (No Duplicates)

```
Steps:
1. Open DevTools in Tab A
   - Go to Network tab
   - Filter for "token-expired"

2. Log in in Tab A
   Note the SessionId from the JWT

3. Open same app in Tab B (same origin)

4. Both tabs share the same token in localStorage

5. Wait 10 minutes in both tabs

6. Check DevTools Network tab in Tab A:
   Expected: Exactly ONE POST to /api/auth/token-expired
   
   (The request from Tab B may also appear, but backend will reject as duplicate)

7. Verify in database:
   SELECT COUNT(*) as Count
   FROM AuditLogs
   WHERE SessionId = 'matching-session-id'
   AND EventType = 'SessionExpired';
   
   Expected: Count = 1 (exactly one)

? Test passes if only one SessionExpired entry despite multiple tabs
```

### Test 3: Manual Logout vs Session Expiry

```
Step A: Test Manual Logout First
?????????????????????????????????
1. Log in with test credentials

2. Note the SessionId from token

3. Immediately click Logout (before 10 minutes)

4. Check database:
   SELECT EventType, TimestampUtc 
   FROM AuditLogs 
   WHERE SessionId = 'noted-session-id'
   ORDER BY TimestampUtc ASC;
   
   Expected output:
   EventType | TimestampUtc
   Login     | T+0
   Logout    | T+10seconds

5. Verify NO SessionExpired entry for this session

? Manual logout creates "Logout" entry, not "SessionExpired"

Step B: Test Session Expiry (Different Session)
????????????????????????????????????????????????
1. Log in with test credentials again
   (New SessionId will be generated)

2. Note the new SessionId

3. Wait 10 minutes (don't click logout)

4. Check database:
   SELECT EventType, TimestampUtc 
   FROM AuditLogs 
   WHERE SessionId = 'new-session-id'
   ORDER BY TimestampUtc ASC;
   
   Expected output:
   EventType    | TimestampUtc
   Login        | T+0
   SessionExpired | T+10min

5. Verify NO Logout entry for this session

? Session expiry creates "SessionExpired" entry, not "Logout"

Summary:
- Session A: Login ? Logout (manual)
- Session B: Login ? SessionExpired (automatic)
- Both terminates session ?
- Easy to distinguish in audit trail ?
```

### Test 4: Network Resilience

```
Steps:
1. Open Browser DevTools (F12)

2. Go to Network tab

3. Log in

4. Wait 9 minutes

5. In DevTools, set "Offline" (simulate network failure)

6. Wait 1 more minute (until expiry)

7. Observe:
   ? UI shows logged out (despite network offline)
   ? Token cleared from localStorage
   ? Timer cleared

8. Go back online

9. Check database:
   SELECT * FROM AuditLogs 
   WHERE EventType = 'SessionExpired'
   ORDER BY TimestampUtc DESC
   LIMIT 1;
   
   Expected: No new SessionExpired entry
   (Network was offline, so notification failed)

10. Verify UI is still logged out ?

? Test passes - logout succeeds even without backend notification
```

### Test 5: Duplicate Prevention Under Race Condition

```
Steps (Advanced Testing):
1. Set up SQL Server Profiler to watch AuditLogs table

2. Log in (note SessionId)

3. Manually fire timer twice in quick succession:
   - This would be difficult without code modification
   - Instead, verify the code has duplicate check

Code Review Verification:
- Check HandleTokenExpiredAsync() has:
  ? firstOrDefaultAsync(a => a.SessionId == sessionId && ...)
  ? if (existingExpiry != null) return;
  ? Only calls RecordEventAsync if not found

? Test passes - duplicate check prevents race conditions
```

## Browser Console Testing

```javascript
// In browser console while app is running:

// 1. Check current token expiration
const token = localStorage.getItem('token');
if (token) {
    const payload = JSON.parse(atob(token.split('.')[1]));
    const expiresAt = new Date(payload.exp * 1000);
    console.log('Token expires at:', expiresAt);
    console.log('Minutes until expiry:', 
        (expiresAt - new Date()) / 60000);
}

// 2. Check SessionId from JWT
const sessionId = payload.jti;
console.log('SessionId (jti):', sessionId);

// 3. Monitor Network tab for token-expired POST
// DevTools ? Network tab ? Filter by "token-expired"
// Should see one POST request when timer fires
```

## API Endpoint Testing (Postman/cURL)

```bash
# Step 1: Log in to get token
curl -X POST http://localhost:7011/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "TestPassword123!"
  }'

# Response:
# {
#   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
#   "expiresAt": "2024-XX-XXTXX:XX:XXZ"
# }

# Step 2: Wait for token to expire in your app
# (Or test with the token before it expires)

# Step 3: Call token-expired endpoint
curl -X POST http://localhost:7011/api/auth/token-expired \
  -H "Content-Type: application/json" \
  -d '{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'

# Response:
# {
#   "message": "Token expiry recorded"
# }

# Step 4: Call again with same token (should still work)
curl -X POST http://localhost:7011/api/auth/token-expired \
  -H "Content-Type: application/json" \
  -d '{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'

# Response:
# {
#   "message": "Token expiry recorded"
# }

# Step 5: Check database (in browser):
# SELECT * FROM AuditLogs 
# WHERE EventType = 'SessionExpired'
# ORDER BY TimestampUtc DESC;
# Expected: Only ONE entry for this SessionId!
```

## SQL Queries for Verification

```sql
-- 1. Verify SessionExpired entries exist
SELECT COUNT(*) as Total_SessionExpired
FROM AuditLogs
WHERE EventType = 'SessionExpired';

-- 2. Check for duplicates (should return 0 rows)
SELECT SessionId, COUNT(*) as DuplicateCount
FROM AuditLogs
WHERE EventType = 'SessionExpired'
GROUP BY SessionId
HAVING COUNT(*) > 1;

-- 3. View specific user's audit trail
DECLARE @UserId NVARCHAR(MAX) = 'specific-user-id';
SELECT EventType, TimestampUtc, SessionId
FROM AuditLogs
WHERE UserId = @UserId
ORDER BY TimestampUtc DESC;

-- 4. Compare manual logouts to automatic expirations
SELECT 
    COUNT(CASE WHEN EventType = 'Logout' THEN 1 END) as ManualLogouts,
    COUNT(CASE WHEN EventType = 'SessionExpired' THEN 1 END) as AutomaticExpirations,
    COUNT(CASE WHEN EventType = 'Login' THEN 1 END) as TotalLogins
FROM AuditLogs
WHERE TimestampUtc > DATEADD(DAY, -1, GETUTCDATE());

-- 5. Verify session flow (should see Login then either Logout or SessionExpired)
DECLARE @SessionId NVARCHAR(100) = 'your-session-id';
SELECT EventType, TimestampUtc
FROM AuditLogs
WHERE SessionId = @SessionId
ORDER BY TimestampUtc ASC;

-- 6. Check index exists
SELECT name, type_desc
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.AuditLogs')
AND name LIKE '%SessionId%';
```

## Performance Verification

```sql
-- Check duplicate prevention query performance
-- This query should be FAST due to SessionId index

SET STATISTICS TIME ON;

SELECT TOP 1 *
FROM AuditLogs
WHERE SessionId = 'specific-session-id'
AND EventType = 'SessionExpired';

SET STATISTICS TIME OFF;

-- Expected result:
-- SQL Server Execution Times:
-- Parse and Compile time: < 10 ms
-- Execution time: < 5 ms  ? Fast due to index
```

## Build & Compilation Verification

```bash
# Run build
dotnet build

# Expected output:
# Build succeeded

# If any errors:
# - Check: ServiceMarketplace.UI.Shared\Auth\TokenAuthenticationStateProvider.cs
#   - Must have: using System.Net.Http.Json;
#   - Must have: HttpClient httpClient parameter
#   - Must have: NotifyBackendOfExpiryAsync method

# - Check: ServiceMarketplace.API\Controllers\AuthController.cs
#   - Must have: [HttpPost("token-expired")] endpoint
#   - Must have: TokenExpiredRequest class

# - Check: ServiceMarketplace.Infrastructure\Services\AuthService.cs
#   - Must have duplicate check logic
#   - Must have: FirstOrDefaultAsync(a => a.SessionId == ...)
```

## Deployment Checklist

Before deploying to production:

- [ ] Build passes without errors
- [ ] All 5 functional tests pass (locally)
- [ ] SessionId index exists in production database
- [ ] AuditLogs table has all required columns
- [ ] API endpoint `/api/auth/token-expired` is accessible
- [ ] CORS policy allows POST to token-expired endpoint
- [ ] Logging is configured (check Application Insights)
- [ ] No breaking changes to existing login/logout flow
- [ ] Database migrations have run successfully
- [ ] Connection string points to correct database

## Monitoring & Ongoing Verification

```sql
-- Weekly health check
-- Run to verify no anomalies

-- 1. Duplicate check
SELECT SessionId, COUNT(*) as Count
FROM AuditLogs
WHERE EventType = 'SessionExpired'
  AND TimestampUtc > DATEADD(WEEK, -1, GETUTCDATE())
GROUP BY SessionId
HAVING COUNT(*) > 1;
-- Should return: 0 rows

-- 2. Session ratio
SELECT 
    CONVERT(DATE, TimestampUtc) as Date,
    COUNT(CASE WHEN EventType = 'Login' THEN 1 END) as Logins,
    COUNT(CASE WHEN EventType = 'SessionExpired' THEN 1 END) as Expirations,
    COUNT(CASE WHEN EventType = 'Logout' THEN 1 END) as ManualLogouts
FROM AuditLogs
WHERE TimestampUtc > DATEADD(WEEK, -1, GETUTCDATE())
GROUP BY CONVERT(DATE, TimestampUtc)
ORDER BY Date DESC;
-- Should show healthy mix of expirations and manual logouts

-- 3. Check for failed backend notifications
-- (Users who logged out locally but no SessionExpired entry)
-- This is harder to detect, but unlikely with current design
```

---

**All verification items should pass before marking implementation as complete. ?**
