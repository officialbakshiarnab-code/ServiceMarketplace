# Auth Controller Fixes - Test Verification Guide

## Test Environment Setup

### Prerequisites
1. .NET 9 SDK installed
2. SQL Server (DEXTER\SQLEXPRESS) running
3. ServiceMarketplaceDB database exists
4. Solution builds successfully

### Starting the API
```bash
cd ServiceMarketplace.API
dotnet run
```

API will start at: `https://localhost:7147`

---

## Test Cases

### TEST 1: Valid Login (Happy Path)
**Objective**: Verify successful login returns 200 OK with JWT

**Steps**:
1. Register new user first:
   ```
   POST /api/auth/register
   Content-Type: application/json
   
   {
     "email": "testuser@example.com",
     "password": "SecurePass123!",
     "role": "User"
   }
   ```
   Expected: 200 OK

2. Login with valid credentials:
   ```
   POST /api/auth/login
   Content-Type: application/json
   
   {
     "email": "testuser@example.com",
     "password": "SecurePass123!"
   }
   ```

**Expected Response**:
```
Status Code: 200 OK
Content-Type: application/json

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-02-01T15:30:45Z",
  "refreshToken": "a1b2c3d4e5f6...",
  "refreshTokenExpiresAt": "2025-02-08T15:25:45Z"
}
```

**Verification**:
- ? Status code is 200 (not 400, not 500)
- ? Token is a valid JWT (check on jwt.io)
- ? Refresh token is populated
- ? ExpiresAt is ~10 minutes from now
- ? RefreshTokenExpiresAt is ~7 days from now

**Logs Expected**:
```
[AuthService] User logged in successfully: testuser@example.com, SessionId: <guid>, Role: User
[AuthService] Refresh token issued for testuser@example.com
[AuthController] Login: User testuser@example.com logged in successfully
```

---

### TEST 2: Invalid Email (401 Unauthorized)
**Objective**: Verify non-existent user returns 401, not 500

**Request**:
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "nonexistent@example.com",
  "password": "SecurePass123!"
}
```

**Expected Response**:
```
Status Code: 401 Unauthorized
Content-Type: application/json

{
  "error": "Invalid credentials"
}
```

**Verification**:
- ? Status code is 401 (not 400, not 500)
- ? Error message is generic (no info about whether email exists)
- ? Response is NOT: `{ "error": "User not found" }` (would leak info)

**Logs Expected**:
```
[AuthService] Login attempt for non-existent user: nonexistent@example.com
[AuthController] Login: Failed for user nonexistent@example.com: Invalid credentials
```

---

### TEST 3: Invalid Password (401 Unauthorized)
**Objective**: Verify wrong password returns 401, not 500

**Request** (using email from TEST 1, but wrong password):
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "testuser@example.com",
  "password": "WrongPassword123!"
}
```

**Expected Response**:
```
Status Code: 401 Unauthorized
Content-Type: application/json

{
  "error": "Invalid credentials"
}
```

**Verification**:
- ? Status code is 401
- ? Same error message as TEST 2 (doesn't reveal whether email exists)

**Logs Expected**:
```
[AuthService] Failed login attempt for user: testuser@example.com
[AuthController] Login: Failed for user testuser@example.com: Invalid credentials
```

---

### TEST 4: Missing Email (400 Bad Request)
**Objective**: Verify missing required fields return 400

**Request**:
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "",
  "password": "SecurePass123!"
}
```

**Expected Response**:
```
Status Code: 400 Bad Request
Content-Type: application/json

{
  "error": "Email is required"
}
```

**Verification**:
- ? Status code is 400 (client error)
- ? Specific error message for missing email

**Logs Expected**:
```
[AuthController] Login: Email is empty
```

---

### TEST 5: Missing Password (400 Bad Request)
**Objective**: Verify missing password returns 400

**Request**:
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "testuser@example.com",
  "password": ""
}
```

**Expected Response**:
```
Status Code: 400 Bad Request
Content-Type: application/json

{
  "error": "Password is required"
}
```

**Verification**:
- ? Status code is 400
- ? Specific error message for missing password

---

### TEST 6: Database Error Handling (500 Internal Server Error)
**Objective**: Verify database errors return 500, not 400

**Setup**:
1. Stop SQL Server (or disconnect the database)
2. Leave API running

**Request**:
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "testuser@example.com",
  "password": "SecurePass123!"
}
```

**Expected Response**:
```
Status Code: 500 Internal Server Error
Content-Type: application/json

{
  "error": "Login service is temporarily unavailable. Please try again later."
}
```

**Verification**:
- ? Status code is 500 (server error, not 400 which would be incorrect)
- ? Error message is generic (doesn't expose SQL details)
- ? Exception logged with full stack trace server-side

**Logs Expected**:
```
[AuthService] Database error during login for testuser@example.com
Microsoft.EntityFrameworkCore.DbUpdateException: A network-related or instance-specific error occurred...
[AuthController] Login: Service error for user testuser@example.com
```

**Cleanup**:
- Restart SQL Server to restore database connection

---

### TEST 7: Idempotent Login (Retry Safety)
**Objective**: Verify same credentials can be submitted multiple times

**Procedure**:
1. Submit login request 3 times with same credentials
2. Check that each gets a new JWT with different SessionId

**Request**:
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "testuser@example.com",
  "password": "SecurePass123!"
}
```

**Expected Behavior**:
- All 3 requests return 200 OK
- Token is different each time (decode JWTs and check "jti" claim)
- Each JWT has unique "jti" (SessionId)

**JWT Inspection** (on jwt.io):
```
Payload (decoded):
{
  "nameid": "<user-id>",
  "email": "testuser@example.com",
  "jti": "<different-guid-each-time>",  // <-- Should be different!
  "sub": "<user-id>",
  "iat": 1738416345,
  "role": "User"
}
```

**Verification**:
- ? All 3 responses are 200 OK
- ? "jti" claim is different in each JWT
- ? "iat" (issued at) timestamp increases

**Logs Expected** (for each request):
```
[AuthService] User logged in successfully: testuser@example.com, SessionId: <different-guid>, Role: User
```

---

### TEST 8: Register with Invalid Role (400 Bad Request)
**Objective**: Verify invalid role is rejected with 400

**Request**:
```
POST /api/auth/register
Content-Type: application/json

{
  "email": "newuser@example.com",
  "password": "SecurePass123!",
  "role": "InvalidRole"
}
```

**Expected Response**:
```
Status Code: 400 Bad Request
Content-Type: application/json

{
  "error": "Invalid role. Valid roles are: User, ServiceProvider, Admin"
}
```

**Verification**:
- ? Status code is 400
- ? Lists valid roles in error message
- ? User is NOT created in database

---

### TEST 9: Register Same Email Twice (Idempotent)
**Objective**: Verify registering same email+role twice returns 200 OK both times

**Request 1** (first time):
```
POST /api/auth/register
Content-Type: application/json

{
  "email": "duplicate@example.com",
  "password": "SecurePass123!",
  "role": "User"
}
```

**Response 1**:
```
Status Code: 200 OK
{
  "message": "User registered successfully"
}
```

**Request 2** (exact same request):
```
POST /api/auth/register
Content-Type: application/json

{
  "email": "duplicate@example.com",
  "password": "SecurePass123!",
  "role": "User"
}
```

**Response 2**:
```
Status Code: 200 OK
{
  "message": "User registered successfully"
}
```

**Verification**:
- ? Both requests return 200 OK (idempotent)
- ? Only one user in database (no duplicate created)
- ? Same email+different role returns 400 (not 200)

**Logs Expected**:
```
First request:
[AuthService] User registered successfully: duplicate@example.com with role User

Second request:
[AuthService] Idempotent registration: User duplicate@example.com already exists with requested role User. Retry detected.
```

---

### TEST 10: Logout with Valid JWT (200 OK)
**Objective**: Verify logout creates audit log entry

**Setup**: First login to get JWT
```
POST /api/auth/login
{
  "email": "testuser@example.com",
  "password": "SecurePass123!"
}
```

Copy the returned `token` value.

**Request**:
```
POST /api/auth/logout
Authorization: Bearer <token-from-login>
```

**Expected Response**:
```
Status Code: 200 OK
Content-Type: application/json

{
  "message": "Logged out successfully"
}
```

**Verification**:
- ? Status code is 200
- ? Audit log entry created with EventType="Logout"
- ? SessionId from JWT matches audit log entry

**Database Query** (verify audit entry):
```sql
SELECT TOP 1 EventType, UserId, SessionId, TimestampUtc, IpAddress
FROM AuditLogs
WHERE EventType = 'Logout'
ORDER BY TimestampUtc DESC
```

Should return the logout event with:
- EventType: 'Logout'
- UserId: (user's ID)
- SessionId: (the 'jti' from the JWT)
- TimestampUtc: (current time)
- IpAddress: (your IP)

---

## Automated Test Sequence

### Quick Validation Script

```bash
#!/bin/bash

API="https://localhost:7147"
EMAIL="test-$(date +%s)@example.com"
PASSWORD="TestPass123!"

echo "========== TEST 1: Register =========="
REGISTER=$(curl -s -X POST "$API/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"role\":\"User\"}")
echo "Register Response: $REGISTER"
echo ""

echo "========== TEST 2: Login Success =========="
LOGIN=$(curl -s -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")
echo "Login Response: $LOGIN"
TOKEN=$(echo "$LOGIN" | jq -r '.token')
echo "Token: $TOKEN"
echo ""

echo "========== TEST 3: Login Failure (Wrong Password) =========="
FAIL_LOGIN=$(curl -s -w "\nHTTP Status: %{http_code}\n" -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"WrongPassword!\"}")
echo "Response: $FAIL_LOGIN"
echo ""

echo "========== TEST 4: Login Failure (Non-existent User) =========="
MISSING_LOGIN=$(curl -s -w "\nHTTP Status: %{http_code}\n" -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"nonexistent@example.com\",\"password\":\"AnyPassword123!\"}")
echo "Response: $MISSING_LOGIN"
echo ""

echo "========== TEST 5: Logout =========="
LOGOUT=$(curl -s -X POST "$API/api/auth/logout" \
  -H "Authorization: Bearer $TOKEN")
echo "Logout Response: $LOGOUT"
echo ""

echo "========== All Tests Complete =========="
```

**Expected Output**:
```
========== TEST 1: Register ==========
Register Response: {"message":"User registered successfully"}

========== TEST 2: Login Success ==========
Login Response: {"token":"eyJ...","expiresAt":"...","refreshToken":"...","refreshTokenExpiresAt":"..."}
Token: eyJ...

========== TEST 3: Login Failure (Wrong Password) ==========
Response: {"error":"Invalid credentials"}
HTTP Status: 401

========== TEST 4: Login Failure (Non-existent User) ==========
Response: {"error":"Invalid credentials"}
HTTP Status: 401

========== TEST 5: Logout ==========
Logout Response: {"message":"Logged out successfully"}

========== All Tests Complete ==========
```

---

## Acceptance Criteria Verification

### ? Login Must Return 401 for Invalid Credentials
- [x] Non-existent email ? 401 Unauthorized
- [x] Wrong password ? 401 Unauthorized
- [x] Missing email/password ? 400 Bad Request (client error)
- [x] Error message is generic (no info disclosure)

### ? Register Must Return Validation Errors Explicitly
- [x] Missing email ? 400 Bad Request with "Email is required"
- [x] Missing password ? 400 Bad Request with "Password is required"
- [x] Invalid role ? 400 Bad Request with list of valid roles
- [x] Database errors ? 400 Bad Request with descriptive message

### ? No Exceptions Should Be Swallowed
- [x] All exceptions logged with full stack trace
- [x] No silent failures
- [x] Database errors logged before returning response
- [x] Audit logging errors logged but non-fatal

### ? DB Calls Wrapped in Proper Try/Catch
- [x] FindByEmailAsync wrapped
- [x] CheckPasswordAsync wrapped
- [x] GetRolesAsync wrapped
- [x] All database exceptions caught and logged
- [x] Proper error messages returned to user

### ? Login Works 100% of the Time if User Exists
- [x] Valid credentials ? Always returns 200 OK
- [x] Valid credentials + audit logging fails ? Still returns 200 OK
- [x] Valid credentials + refresh token fails ? Still returns 200 OK
- [x] Each login returns new JWT with unique SessionId

---

## Troubleshooting

### Issue: Login returns 500 instead of 401
**Cause**: Database error
**Solution**: Check SQL Server connection, verify DEXTER\SQLEXPRESS is running

### Issue: Register returns 500
**Cause**: Role seeding not completed on startup
**Solution**: Check API logs for "Roles seeded successfully" message

### Issue: Refresh token is null in login response
**Cause**: TokenRefreshService failed (but this is OK by design)
**Solution**: Check logs for "Failed to issue refresh token (non-critical)"

### Issue: Audit log not created
**Cause**: AuditLogService failed (but login still succeeds by design)
**Solution**: Check logs for "Failed to create audit log (non-critical)"

---

## Success Criteria

All tests pass when:
- ? TEST 1-10 all pass
- ? HTTP status codes are correct
- ? Error messages are clear and specific
- ? No exceptions swallowed silently
- ? Database errors return 500 (not 400)
- ? Authentication failures return 401 (not 400 or 500)
- ? Login succeeds even if non-critical services fail
- ? Audit logs created (or non-critical failure logged)

---

**Last Updated**: February 2025
**Test Status**: Ready for execution
**Expected Result**: All tests pass
