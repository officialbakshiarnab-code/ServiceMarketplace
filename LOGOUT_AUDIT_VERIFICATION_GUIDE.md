# Logout Audit Trail - Verification Guide

## ?? Purpose

This guide provides step-by-step instructions to verify that the logout audit functionality is working correctly.

---

## ? Implementation Summary

### What Has Been Fixed

1. **UI Logout Flow** (`AuthApiClient.cs`):
   - ? Token is retrieved from storage BEFORE API call
   - ? Bearer token attached to Authorization header
   - ? API endpoint called with valid authentication
   - ? Token cleared AFTER API call completes

2. **API Endpoint** (`AuthController.cs`):
   - ? `[Authorize]` attribute validates JWT
   - ? UserId, Role, SessionId extracted from claims
   - ? Returns 401 Unauthorized if claims are invalid
   - ? Calls AuditLogService to create record

3. **Audit Service** (`AuditLogService.cs`):
   - ? Creates NEW audit row (append-only)
   - ? NEVER updates existing rows
   - ? Uses `DbContext.Add()` not `Update()`
   - ? EventType = "Logout"
   - ? TimestampUtc = DateTime.UtcNow

---

## ?? Testing Instructions

### Step 1: Start the Application

1. **Start the API Project**
   ```
   cd C:\MyProject\ServiceMarketplace\ServiceMarketplace.API
   dotnet run
   ```
   
   Expected output:
   ```
   Now listening on: https://localhost:7001
   ```

2. **Start the Web UI Project** (in a separate terminal)
   ```
   cd C:\MyProject\ServiceMarketplace\ServiceMarketplace.UI.Web
   dotnet run
   ```
   
   Expected output:
   ```
   Now listening on: https://localhost:7241
   ```

### Step 2: Login to the Application

1. Navigate to `https://localhost:7241` in your browser
2. Click "Login" or "Register"
3. Enter credentials:
   - Email: `user@test.com`
   - Password: `Test123!`
   - Role: `User` or `ServiceProvider`
4. Click "Login"

**Expected Console Logs:**

**Browser Console** (F12 ? Console):
```
[Login successful]
```

**API Console**:
```
AUDIT INSERTED: Login | UserId: abc123... | SessionId: def456... | Timestamp: 2025-02-01 14:30:15 UTC
```

### Step 3: Click "Sign Out"

1. Find the "Sign Out" button (typically in the header/navigation)
2. Click "Sign Out"

**Expected Console Logs:**

**Browser Console** (F12 ? Console):
```
[LogoutButton] ========== LOGOUT FLOW STARTED ==========
[LogoutButton] STEP 1: Calling AuthApi.LogoutAsync()...
[AuthApiClient] Calling logout API with Bearer token...
[AuthApiClient] Logout API call succeeded - audit record created
[AuthApiClient] Clearing token from storage...
[AuthApiClient] Token cleared from storage
[LogoutButton] STEP 1: AuthApi.LogoutAsync() completed
[LogoutButton] STEP 2: Updating auth state...
[LogoutButton] STEP 2: Auth state updated
[LogoutButton] STEP 3: Navigating to /login...
[LogoutButton] ========== LOGOUT FLOW COMPLETED ==========
```

**API Console**:
```
AUDIT INSERTED: Logout | UserId: abc123... | SessionId: def456... | Timestamp: 2025-02-01 14:35:22 UTC
```

### Step 4: Verify Database Records

#### SQL Query 1: Check Latest Audit Events

```sql
-- View the most recent audit events
SELECT TOP 10
    Id,
    EventType,
    UserId,
    Role,
    SessionId,
    TimestampUtc,
    IpAddress,
    UserAgent
FROM AuditLogs
ORDER BY TimestampUtc DESC;
```

**Expected Result:**
```
EventType | TimestampUtc           | UserId  | SessionId
----------|------------------------|---------|----------
Logout    | 2025-02-01 14:35:22   | abc123  | def456
Login     | 2025-02-01 14:30:15   | abc123  | def456
```

#### SQL Query 2: Verify Logout Records

```sql
-- Count logout events
SELECT 
    COUNT(*) AS TotalLogouts,
    COUNT(DISTINCT UserId) AS UniqueUsers,
    COUNT(DISTINCT SessionId) AS UniqueSessions
FROM AuditLogs
WHERE EventType = 'Logout';
```

**Expected Result:**
```
TotalLogouts | UniqueUsers | UniqueSessions
-------------|-------------|---------------
1            | 1           | 1
```

#### SQL Query 3: Check Session Lifecycle

```sql
-- View complete session lifecycle
WITH SessionEvents AS (
    SELECT 
        SessionId,
        UserId,
        EventType,
        TimestampUtc,
        ROW_NUMBER() OVER (PARTITION BY SessionId ORDER BY TimestampUtc) AS EventOrder
    FROM AuditLogs
    WHERE SessionId IS NOT NULL
)
SELECT 
    SessionId,
    UserId,
    MAX(CASE WHEN EventType = 'Login' THEN TimestampUtc END) AS LoginTime,
    MAX(CASE WHEN EventType = 'Logout' THEN TimestampUtc END) AS LogoutTime,
    MAX(CASE WHEN EventType = 'SessionExpired' THEN TimestampUtc END) AS ExpiredTime,
    DATEDIFF(SECOND, 
        MAX(CASE WHEN EventType = 'Login' THEN TimestampUtc END),
        COALESCE(
            MAX(CASE WHEN EventType = 'Logout' THEN TimestampUtc END),
            MAX(CASE WHEN EventType = 'SessionExpired' THEN TimestampUtc END)
        )
    ) AS SessionDurationSeconds
FROM SessionEvents
GROUP BY SessionId, UserId
ORDER BY LoginTime DESC;
```

**Expected Result:**
```
SessionId | LoginTime            | LogoutTime           | SessionDurationSeconds
----------|----------------------|----------------------|----------------------
def456    | 2025-02-01 14:30:15 | 2025-02-01 14:35:22 | 307
```

#### SQL Query 4: Verify Append-Only Design

```sql
-- Verify no updates occurred (check for duplicate SessionId + EventType)
SELECT 
    SessionId,
    EventType,
    COUNT(*) AS RecordCount
FROM AuditLogs
WHERE SessionId IS NOT NULL
GROUP BY SessionId, EventType
HAVING COUNT(*) > 1;
```

**Expected Result:**
```
(Empty result set - no duplicates)
```

---

## ? Success Criteria

### Console Logs
- ? Browser console shows complete logout flow steps
- ? API console shows "AUDIT INSERTED: Logout" message
- ? No error messages in either console

### Database Verification
- ? Latest row in AuditLogs has `EventType = 'Logout'`
- ? New row created every time user clicks "Sign Out"
- ? Each logout has unique `Id` (GUID)
- ? `TimestampUtc` is populated with current UTC time
- ? `UserId`, `SessionId`, `Role` are populated
- ? No duplicate records for same `SessionId + EventType`

### Behavior Verification
- ? User is redirected to `/login` after logout
- ? User cannot access protected pages after logout
- ? JWT token is cleared from browser storage
- ? Logout works even if API is temporarily unavailable (graceful degradation)

---

## ? Troubleshooting

### Issue 1: No "AUDIT INSERTED" message in API console

**Possible Causes:**
1. Logout endpoint not being called
2. API endpoint returning error before reaching AuditLogService

**Diagnosis:**
```sql
-- Check if ANY audit records are being created
SELECT COUNT(*) FROM AuditLogs WHERE EventType = 'Login';
```

If Login records exist but Logout doesn't:
- Check browser console for HTTP errors (401, 403, 500)
- Verify Bearer token is attached to logout request (check Network tab)
- Ensure `[Authorize]` attribute is working

**Solution:**
- Review browser console errors
- Check that `AuthApiClient.LogoutAsync()` attaches Bearer token
- Verify JWT is valid at time of logout call

### Issue 2: 401 Unauthorized on logout

**Possible Causes:**
1. Token already expired
2. Token not attached to request
3. JWT validation failing

**Diagnosis:**
Check browser console and Network tab (F12):
```
[AuthApiClient] Logout API call failed: 401
```

**Solution:**
1. Verify token is not expired (check JWT expiration time)
2. Confirm Bearer token is in Authorization header
3. Review `AuthApiClient.LogoutAsync()` implementation

### Issue 3: Database records not appearing

**Possible Causes:**
1. Database connection issue
2. Transaction not committed
3. EF Core change tracking issue

**Diagnosis:**
```sql
-- Check database connection
SELECT GETDATE() AS CurrentServerTime;

-- Check if DbContext is saving
SELECT COUNT(*) FROM AuditLogs;
```

**Solution:**
1. Verify connection string in `appsettings.json`
2. Ensure `SaveChangesAsync()` is awaited
3. Check for EF Core exceptions in API logs

### Issue 4: Multiple logout records per session

**Possible Causes:**
1. User clicking "Sign Out" multiple times
2. Race condition in logout handling
3. Browser tab duplication

**Diagnosis:**
```sql
-- Check for duplicates
SELECT SessionId, EventType, COUNT(*) 
FROM AuditLogs 
GROUP BY SessionId, EventType 
HAVING COUNT(*) > 1;
```

**Solution:**
1. Disable "Sign Out" button during logout (`_busy` flag)
2. Add client-side duplicate prevention
3. Consider server-side idempotency check

---

## ?? Testing Multiple Logouts

To verify that EVERY logout creates a new record:

1. **Login** ? Check database (1 Login record)
2. **Logout** ? Check database (1 Login + 1 Logout record)
3. **Login** ? Check database (2 Login + 1 Logout record)
4. **Logout** ? Check database (2 Login + 2 Logout record)
5. **Login** ? Check database (3 Login + 2 Logout record)
6. **Logout** ? Check database (3 Login + 3 Logout record)

**SQL Query:**
```sql
-- Count events by type
SELECT 
    EventType,
    COUNT(*) AS RecordCount,
    MIN(TimestampUtc) AS FirstEvent,
    MAX(TimestampUtc) AS LastEvent
FROM AuditLogs
GROUP BY EventType
ORDER BY EventType;
```

**Expected Result:**
```
EventType      | RecordCount | FirstEvent           | LastEvent
---------------|-------------|----------------------|----------------------
Login          | 3           | 2025-02-01 14:30:15 | 2025-02-01 14:50:00
Logout         | 3           | 2025-02-01 14:35:22 | 2025-02-01 14:55:30
SessionExpired | 0           | NULL                 | NULL
```

---

## ?? Expected Audit Trail Pattern

### Normal User Session
```
Time         | Event          | Description
-------------|----------------|------------------------------------
14:30:15 UTC | Login          | User logs in, JWT issued
14:35:22 UTC | Logout         | User clicks "Sign Out"
```

### Session Expiry (10 Minutes)
```
Time         | Event          | Description
-------------|----------------|------------------------------------
14:30:15 UTC | Login          | User logs in, JWT issued
14:40:15 UTC | SessionExpired | Token expires automatically
```

### Mixed Pattern
```
Time         | Event          | Description
-------------|----------------|------------------------------------
14:30:15 UTC | Login          | Session 1 starts
14:35:22 UTC | Logout         | User logs out manually
14:40:00 UTC | Login          | Session 2 starts
14:50:00 UTC | SessionExpired | Session 2 expires (10 min)
14:51:00 UTC | Login          | Session 3 starts
14:53:30 UTC | Logout         | User logs out manually
```

---

## ?? Security Notes

1. **Append-Only Design**: Audit logs are NEVER updated or deleted
2. **UTC Timestamps**: All times stored in UTC for consistency
3. **Session Tracking**: Each login/logout pair linked by SessionId
4. **IP & User-Agent**: Captured for forensic analysis
5. **Role Recording**: User's role at time of event is preserved

---

## ?? SQL Reference

### Quick Verification Script

```sql
-- ============================================
-- LOGOUT AUDIT VERIFICATION SCRIPT
-- ============================================

-- 1. Check latest events
PRINT '1. Latest Audit Events:';
SELECT TOP 10
    EventType,
    TimestampUtc,
    UserId,
    SessionId
FROM AuditLogs
ORDER BY TimestampUtc DESC;

-- 2. Count events by type
PRINT '2. Event Counts by Type:';
SELECT 
    EventType,
    COUNT(*) AS Total
FROM AuditLogs
GROUP BY EventType;

-- 3. Find sessions with Logout
PRINT '3. Sessions with Logout Records:';
SELECT 
    SessionId,
    COUNT(*) AS EventCount,
    STRING_AGG(EventType, ', ') AS Events
FROM AuditLogs
WHERE SessionId IN (
    SELECT SessionId 
    FROM AuditLogs 
    WHERE EventType = 'Logout'
)
GROUP BY SessionId
ORDER BY MAX(TimestampUtc) DESC;

-- 4. Verify append-only (no duplicates)
PRINT '4. Check for Duplicates (should be empty):';
SELECT 
    SessionId,
    EventType,
    COUNT(*) AS DuplicateCount
FROM AuditLogs
WHERE SessionId IS NOT NULL
GROUP BY SessionId, EventType
HAVING COUNT(*) > 1;

-- 5. Recent logout activity
PRINT '5. Recent Logout Activity:';
SELECT 
    TimestampUtc,
    UserId,
    SessionId,
    IpAddress,
    UserAgent
FROM AuditLogs
WHERE EventType = 'Logout'
  AND TimestampUtc >= DATEADD(HOUR, -1, GETUTCDATE())
ORDER BY TimestampUtc DESC;
```

---

## ? Final Checklist

Before marking as complete, verify:

- [ ] "AUDIT INSERTED: Logout" appears in API console
- [ ] New row in AuditLogs table with EventType = 'Logout'
- [ ] TimestampUtc is populated with current UTC time
- [ ] UserId matches the logged-in user
- [ ] SessionId matches the JWT jti claim
- [ ] Role is populated
- [ ] IpAddress is captured
- [ ] UserAgent is captured
- [ ] Each logout creates a NEW row (not update)
- [ ] No duplicate records for same SessionId + EventType
- [ ] User is redirected to /login after logout
- [ ] Token is cleared from browser storage
- [ ] User cannot access protected pages after logout

---

## ?? Success!

If all verification steps pass, the logout audit functionality is working correctly:

? **Manual logout ALWAYS creates an AuditLogs record**
? **Append-only design ensures audit integrity**
? **Complete audit trail for compliance**

---

## ?? Support

If issues persist after following this guide:

1. Check the comprehensive documentation:
   - `LOGOUT_AUDIT_IMPLEMENTATION.md`
   - `SESSION_EXPIRY_AUDIT_IMPLEMENTATION.md`
   - `AUDIT_IMPLEMENTATION_VERIFICATION.md`

2. Review the code files:
   - `AuthController.cs` - API endpoint
   - `AuthApiClient.cs` - UI HTTP client
   - `LogoutButton.razor` - UI component
   - `AuditLogService.cs` - Database persistence

3. Verify the complete flow with console logs enabled

---

**Document Version**: 1.0  
**Last Updated**: 2025-02-01  
**Status**: Ready for Testing
