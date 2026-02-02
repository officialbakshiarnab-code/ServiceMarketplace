# Audit Logging - Manual Testing Guide

## ?? How to Validate the Audit Logging System

This guide provides step-by-step instructions to manually test and validate that the audit logging system works correctly.

---

## Prerequisites

1. **Database Running:** SQL Server instance accessible
2. **API Running:** `dotnet run` from `ServiceMarketplace.API` directory
3. **UI Running:** `dotnet run` from `ServiceMarketplace.UI.Web` directory
4. **Database Tool:** SQL Server Management Studio or Azure Data Studio

---

## Test 1: Login Creates New Audit Row

### Steps
1. Navigate to `https://localhost:7241/login` in browser
2. Open browser DevTools ? Network tab
3. Enter credentials:
   - **Email:** `user@test.com`
   - **Password:** `Password123!`
4. Click **Login**

### Expected Results
**Network Tab:**
```
POST /api/auth/login
Status: 200 OK
Response: {
  "token": "eyJhbGc...",
  "expiresAt": "2026-02-01T12:10:00Z"
}
```

**Database Query:**
```sql
SELECT TOP 1 * 
FROM AuditLogs 
WHERE EventType = 'Login'
ORDER BY TimestampUtc DESC
```

**Expected Row:**
```
Id: [GUID]
UserId: [User GUID from AspNetUsers]
Role: User
EventType: Login
TimestampUtc: 2026-02-01 12:00:00
SessionId: [GUID from JWT jti claim]
IpAddress: 127.0.0.1 (or ::1 for IPv6)
UserAgent: Mozilla/5.0...
```

? **Pass Criteria:** New row inserted with EventType='Login'

---

## Test 2: Manual Logout Creates New Audit Row

### Steps
1. After logging in (Test 1), stay on dashboard
2. Open browser DevTools ? Network tab
3. Click **Sign Out** button

### Expected Results
**Network Tab:**
```
POST /api/auth/logout
Status: 200 OK
Response: {
  "message": "Logged out successfully"
}
```

**Database Query:**
```sql
SELECT TOP 2 * 
FROM AuditLogs 
WHERE SessionId = '[SessionId from Test 1]'
ORDER BY TimestampUtc DESC
```

**Expected Rows:**
```
Row 1:
  EventType: Logout
  TimestampUtc: 2026-02-01 12:05:00
  SessionId: [Same as Login]

Row 2:
  EventType: Login
  TimestampUtc: 2026-02-01 12:00:00
  SessionId: [Same as Logout]
```

? **Pass Criteria:** Two separate rows (Login and Logout) with same SessionId

---

## Test 3: Session Timeout Creates New Audit Row

### Steps
1. Login again (creates new session)
2. Note the SessionId from JWT (use jwt.io to decode)
3. Wait **10 minutes** (or change token expiry in AuthService.cs to 30 seconds for faster testing)
4. Try to navigate to any protected page

### Fast Testing Alternative
**Modify AuthService.cs temporarily:**
```csharp
// Line 123 in AuthService.cs
var expirationTime = DateTime.UtcNow.AddSeconds(30); // Changed from .AddMinutes(10)
```

Then:
1. Login
2. Wait 30 seconds
3. Try to navigate to `/user/my-requests`

### Expected Results
**Browser:**
- Token cleared from localStorage
- Redirected to `/login`
- Message: "Not authorized"

**Network Tab:**
```
POST /api/auth/token-expired
Status: 200 OK
Response: {
  "message": "Token expiry recorded"
}
```

**Database Query:**
```sql
SELECT TOP 3 * 
FROM AuditLogs 
WHERE SessionId = '[SessionId from this test]'
ORDER BY TimestampUtc ASC
```

**Expected Rows:**
```
Row 1:
  EventType: Login
  TimestampUtc: 2026-02-01 12:10:00

Row 2:
  EventType: SessionExpired
  TimestampUtc: 2026-02-01 12:10:30
```

? **Pass Criteria:** Two separate rows (Login and SessionExpired)

---

## Test 4: No Audit Rows Are Updated

### Verification Query
```sql
-- Check for any UPDATE triggers or procedures on AuditLogs
SELECT * 
FROM sys.triggers 
WHERE parent_id = OBJECT_ID('AuditLogs')

-- Expected: No results (no triggers)

-- Check audit log data integrity
SELECT 
    UserId,
    SessionId,
    COUNT(*) as EventCount,
    COUNT(DISTINCT EventType) as UniqueEvents
FROM AuditLogs
GROUP BY UserId, SessionId
HAVING COUNT(*) > 1
```

**Expected Results:**
- Multiple rows per SessionId (Login, Logout, SessionExpired)
- Each row has identical SessionId but different EventType and TimestampUtc
- No rows have modified timestamps that suggest UPDATE operations

? **Pass Criteria:** All audit rows are independent INSERT operations

---

## Test 5: Role-Based UI Works

### Test User Role
1. Login as User
2. Navigate to `https://localhost:7241/user/dashboard`

**Expected:**
- ? Page loads successfully
- ? "Create New Service Request" button visible
- ? "View My Requests" button visible
- ? "Sign Out" button visible

3. Try to access `https://localhost:7241/provider/dashboard`

**Expected:**
- ?? "Unauthorized Access" message displayed
- ?? No provider features visible

### Test ServiceProvider Role
1. Logout
2. Register new account with email `provider@test.com` and role "ServiceProvider"
3. Login as ServiceProvider
4. Navigate to `https://localhost:7241/provider/dashboard`

**Expected:**
- ? Page loads successfully
- ? "Browse Available Requests" button visible
- ? "View My Bids" button visible
- ? "Sign Out" button visible

5. Try to access `https://localhost:7241/user/dashboard`

**Expected:**
- ?? "Unauthorized Access" message displayed
- ?? No user features visible

? **Pass Criteria:** Each role can only access their designated dashboard

---

## Test 6: Build Has Zero Warnings

### Command Line Test
```bash
# From solution root
dotnet build

# Expected output:
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

### Visual Studio Test
1. Open solution in Visual Studio
2. Build ? Rebuild Solution
3. Check Output window

**Expected:**
```
Build started...
1>------ Build started: Project: ServiceMarketplace.Domain ------
1>  ServiceMarketplace.Domain -> bin\Debug\net9.0\ServiceMarketplace.Domain.dll
...
========== Build: 7 succeeded, 0 failed, 0 up-to-date, 0 skipped ==========
```

? **Pass Criteria:** 0 Warnings, 0 Errors

---

## Test 7: No 500 Errors in Network Tab

### Test All Auth Endpoints

#### 1. POST /api/auth/login
```bash
curl -X POST https://localhost:7147/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@test.com","password":"Password123!"}'
```

**Expected:** 200 OK or 401 Unauthorized (if credentials wrong)  
**Not Expected:** 500 Internal Server Error

#### 2. POST /api/auth/logout
```bash
curl -X POST https://localhost:7147/api/auth/logout \
  -H "Authorization: Bearer [TOKEN]"
```

**Expected:** 200 OK or 401 Unauthorized (if token invalid)  
**Not Expected:** 500 Internal Server Error

#### 3. POST /api/auth/token-expired
```bash
curl -X POST https://localhost:7147/api/auth/token-expired \
  -H "Content-Type: application/json" \
  -d '{"token":"[EXPIRED_TOKEN]"}'
```

**Expected:** 200 OK or 400 Bad Request (if token format invalid)  
**Not Expected:** 500 Internal Server Error

### Browser Test
1. Open browser DevTools ? Network tab
2. Perform login, navigate, logout
3. Filter by Status Code ? Should see only 200, 401, 403
4. No 500 errors

? **Pass Criteria:** No 500 Internal Server Error responses

---

## Database Verification Queries

### Query 1: Verify Insert-Only Pattern
```sql
-- Check that each session has separate rows for each event
SELECT 
    SessionId,
    EventType,
    TimestampUtc,
    DATEDIFF(SECOND, LAG(TimestampUtc) OVER (PARTITION BY SessionId ORDER BY TimestampUtc), TimestampUtc) as SecondsSincePrevious
FROM AuditLogs
ORDER BY SessionId, TimestampUtc
```

**Expected:** Multiple rows per SessionId with increasing timestamps

### Query 2: Session Duration Analysis
```sql
-- Calculate session duration (Login to Logout or SessionExpired)
WITH SessionEvents AS (
    SELECT 
        SessionId,
        UserId,
        MIN(CASE WHEN EventType = 'Login' THEN TimestampUtc END) as LoginTime,
        MIN(CASE WHEN EventType = 'Logout' THEN TimestampUtc END) as LogoutTime,
        MIN(CASE WHEN EventType = 'SessionExpired' THEN TimestampUtc END) as ExpiredTime
    FROM AuditLogs
    GROUP BY SessionId, UserId
)
SELECT 
    SessionId,
    UserId,
    LoginTime,
    COALESCE(LogoutTime, ExpiredTime) as EndTime,
    DATEDIFF(MINUTE, LoginTime, COALESCE(LogoutTime, ExpiredTime)) as SessionDurationMinutes,
    CASE 
        WHEN LogoutTime IS NOT NULL THEN 'Explicit Logout'
        WHEN ExpiredTime IS NOT NULL THEN 'Session Expired'
        ELSE 'Still Active'
    END as SessionStatus
FROM SessionEvents
ORDER BY LoginTime DESC
```

### Query 3: Duplicate SessionExpired Check
```sql
-- Verify no duplicate SessionExpired events
SELECT 
    SessionId,
    EventType,
    COUNT(*) as EventCount
FROM AuditLogs
WHERE EventType = 'SessionExpired'
GROUP BY SessionId, EventType
HAVING COUNT(*) > 1
```

**Expected:** No results (no duplicates)

---

## Troubleshooting

### Issue: No audit rows created after login
**Check:**
1. AuditLogService registered in Program.cs
2. Database connection string correct
3. AuditLogs table exists in database
4. Check API logs for exceptions

### Issue: 500 error on logout
**Check:**
1. JWT token is valid and not expired
2. SessionId claim exists in token
3. Check AuthController.Logout() for null checks

### Issue: Role-based UI not working
**Check:**
1. JWT contains role claim (decode at jwt.io)
2. Role value matches "User" or "ServiceProvider" exactly
3. AuthorizeView Roles attribute matches role claim

---

## Success Criteria Summary

| Test | Pass Criteria | Status |
|------|--------------|--------|
| 1. Login | New AuditLog row with EventType='Login' | ? |
| 2. Logout | New AuditLog row with EventType='Logout' | ? |
| 3. Timeout | New AuditLog row with EventType='SessionExpired' | ? |
| 4. No Updates | All audit rows independent (INSERT only) | ? |
| 5. Role UI | User and ServiceProvider dashboards work | ? |
| 6. Warnings | 0 build warnings | ? |
| 7. No 500s | All API calls return 200/401/403, no 500s | ? |

---

**Testing Completed By:** _____________  
**Date:** _____________  
**Overall Result:** ? PASS  ? FAIL

---

## Next Steps After Validation

If all tests pass:
1. ? System is ready for production
2. ? Database migration can be applied to production
3. ? Monitoring and alerting can be enabled
4. ? Audit log retention policy should be defined

If any tests fail:
1. ? Review VALIDATION_RESULTS.md for expected behavior
2. ? Check AUDIT_LOGGING_CENTRALIZATION.md for implementation details
3. ? Consult AUDIT_LOGGING_QUICK_REFERENCE.md for usage examples
4. ? Contact development team for support
