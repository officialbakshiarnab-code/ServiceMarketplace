# Auth API Retry-Safe Validation & Testing

**Date**: February 1, 2025  
**Status**: ? **READY FOR TESTING**  
**Build**: ? Successful (0 errors, 0 warnings)

---

## Quick Validation Checklist

- [x] Registration: Idempotent (email + password + role = same result)
- [x] Login: Immutable (generates new JWT each time, never mutates state)
- [x] Logout: Append-only (creates new audit entries, never updates)
- [x] Token Expiry: Duplicate prevention (one SessionExpired per SessionId)
- [x] Role Assignment: Atomic (in transaction, assigned exactly once)
- [x] HTTP Codes: Consistent (200/400/401 for idempotent responses)
- [x] Database: Safe (no duplicate prevention needed at DB level for login)
- [x] Audit Trail: Complete (every attempt logged)

---

## Test Case 1: Idempotent Registration

### Objective
Verify that registering the same email+password+role multiple times creates user only once and returns success consistently.

### Setup
```bash
BASE_URL="https://localhost:7147"
EMAIL="retry-test-user@example.com"
PASSWORD="Test@123456"
ROLE="User"
```

### Test Steps

**Step 1: First Registration**
```bash
curl -X POST "$BASE_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email":"'$EMAIL'",
    "password":"'$PASSWORD'",
    "role":"'$ROLE'"
  }'
```

Expected Response:
```json
{
  "message": "User registered successfully"
}
HTTP 200 OK
```

Database Check:
```sql
SELECT COUNT(*) as UserCount FROM AspNetUsers WHERE Email='retry-test-user@example.com';
-- Expected: 1
```

**Step 2: Immediate Retry (Simulate Network Timeout)**
```bash
curl -X POST "$BASE_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email":"'$EMAIL'",
    "password":"'$PASSWORD'",
    "role":"'$ROLE'"
  }'
```

Expected Response:
```json
{
  "message": "User registered successfully"
}
HTTP 200 OK
```

Database Check:
```sql
SELECT COUNT(*) as UserCount FROM AspNetUsers WHERE Email='retry-test-user@example.com';
-- Expected: 1 (unchanged - no duplicate)
```

**Step 3: Multiple Retries**
```bash
for i in {1..5}; do
  curl -X POST "$BASE_URL/api/auth/register" \
    -H "Content-Type: application/json" \
    -d '{
      "email":"'$EMAIL'",
      "password":"'$PASSWORD'",
      "role":"'$ROLE'"
    }'
  sleep 0.5
done
```

Expected Result: All responses 200 OK

Database Check:
```sql
SELECT COUNT(*) as UserCount FROM AspNetUsers WHERE Email='retry-test-user@example.com';
-- Expected: 1 (still no duplicates!)
```

### Validation Criteria

? First request creates user (200 OK)  
? Subsequent requests return 200 OK  
? User count never exceeds 1  
? No error on retries  
? Database is clean  

---

## Test Case 2: Login Immutability

### Objective
Verify that logging in multiple times generates different JWTs with different SessionIds, but never mutates user state.

### Setup
```bash
BASE_URL="https://localhost:7147"
EMAIL="login-immutable-test@example.com"
PASSWORD="Test@123456"

# First register a user
curl -X POST "$BASE_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"User"}'
```

### Test Steps

**Step 1: First Login**
```bash
RESPONSE1=$(curl -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'"}')

JWT1=$(echo $RESPONSE1 | jq -r '.token')
EXPIRES1=$(echo $RESPONSE1 | jq -r '.expiresAt')

echo "JWT1: ${JWT1:0:50}..."
echo "Expires1: $EXPIRES1"

# Decode JWT to get SessionId
JWT1_PAYLOAD=$(echo $JWT1 | cut -d. -f2 | base64 -d)
SESSION_ID_1=$(echo $JWT1_PAYLOAD | jq -r '.jti')
echo "SessionId1: $SESSION_ID_1"
```

Expected Response:
```json
{
  "token": "eyJhbGc...",
  "expiresAt": "2025-02-01T15:20:30Z",
  "refreshToken": "..."
}
HTTP 200 OK
```

**Step 2: Immediate Retry (Network Timeout Recovery)**
```bash
sleep 1

RESPONSE2=$(curl -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'"}')

JWT2=$(echo $RESPONSE2 | jq -r '.token')
EXPIRES2=$(echo $RESPONSE2 | jq -r '.expiresAt')

echo "JWT2: ${JWT2:0:50}..."
echo "Expires2: $EXPIRES2"

JWT2_PAYLOAD=$(echo $JWT2 | cut -d. -f2 | base64 -d)
SESSION_ID_2=$(echo $JWT2_PAYLOAD | jq -r '.jti')
echo "SessionId2: $SESSION_ID_2"
```

Expected Response:
```json
{
  "token": "eyJhbGc...",  // ?? DIFFERENT!
  "expiresAt": "2025-02-01T15:25:30Z",  // ?? DIFFERENT!
  "refreshToken": "..."
}
HTTP 200 OK
```

### Validation

```bash
# Validate different JWTs
if [ "$JWT1" != "$JWT2" ]; then
  echo "? Different JWTs generated"
else
  echo "? Same JWT - immutability FAILED"
fi

# Validate different SessionIds
if [ "$SESSION_ID_1" != "$SESSION_ID_2" ]; then
  echo "? Different SessionIds"
else
  echo "? Same SessionId - immutability FAILED"
fi

# Validate user state unchanged
curl -s "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"'$EMAIL'","password":"Test@WRONG"}' | jq .error
# Expected: "Invalid credentials" (user state unchanged)
```

Database Audit Check:
```sql
SELECT SessionId, EventType, TimestampUtc FROM AuditLogs 
WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email='login-immutable-test@example.com')
ORDER BY TimestampUtc;

-- Expected:
-- SessionId1, "Login", 15:20:00
-- SessionId2, "Login", 15:20:01
-- (Two separate login entries, different sessions)
```

### Validation Criteria

? Different JWT each login  
? Different SessionId each login  
? Different expiration times  
? No user state mutation  
? Can retry on network failures  

---

## Test Case 3: Logout Append-Only

### Objective
Verify that logout creates new audit entries each time, never updates existing ones.

### Setup
```bash
BASE_URL="https://localhost:7147"
EMAIL="logout-append-test@example.com"
PASSWORD="Test@123456"

# Register and login
curl -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"User"}'

LOGIN=$(curl -X POST "$BASE_URL/api/auth/login" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'"}')

JWT=$(echo $LOGIN | jq -r '.token')
JWT_PAYLOAD=$(echo $JWT | cut -d. -f2 | base64 -d)
SESSION_ID=$(echo $JWT_PAYLOAD | jq -r '.jti')
```

### Test Steps

**Step 1: First Logout**
```bash
curl -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT"

# Response: 200 OK
```

Database Check:
```sql
SELECT COUNT(*) as LogoutCount FROM AuditLogs 
WHERE SessionId='$SESSION_ID' AND EventType='Logout';
-- Expected: 1
```

**Step 2: Logout Again (Expired JWT)**
```bash
curl -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT"

# Response: 401 Unauthorized (JWT cleared after first logout)
```

**Step 3: Verify Append-Only**
```bash
# Multiple logouts in new session
LOGIN2=$(curl -X POST "$BASE_URL/api/auth/login" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'"}')

JWT2=$(echo $LOGIN2 | jq -r '.token')
SESSION_ID_2=$(echo $JWT2 | cut -d. -f2 | base64 -d | jq -r '.jti')

# Logout 3 times
curl -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT2"

# (JWT now invalid, next calls fail)

curl -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT2"
# 401 Unauthorized

curl -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT2"
# 401 Unauthorized
```

Database Check:
```sql
SELECT SessionId, EventType, COUNT(*) as Count 
FROM AuditLogs 
WHERE SessionId='$SESSION_ID_2'
GROUP BY SessionId, EventType;

-- Expected:
-- SessionId_2, "Login", 1
-- SessionId_2, "Logout", 1 (only first logout succeeded)
```

### Validation Criteria

? First logout creates entry  
? Second logout fails (invalid JWT)  
? Only successful logouts create entries  
? No duplicate entries per session  
? Append-only (never updates)  

---

## Test Case 4: Token Expiry Duplicate Prevention

### Objective
Verify that SessionExpired events are created exactly once per session, with duplicate prevention working.

### Setup
```bash
BASE_URL="https://localhost:7147"
EMAIL="expiry-test@example.com"
PASSWORD="Test@123456"

# Register and login
curl -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"User"}'

LOGIN=$(curl -X POST "$BASE_URL/api/auth/login" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'"}')

JWT=$(echo $LOGIN | jq -r '.token')
```

### Test Steps

**Step 1: Call token-expired endpoint**
```bash
curl -X POST "$BASE_URL/api/auth/token-expired" \
  -H "Content-Type: application/json" \
  -d '{"token":"'$JWT'"}'

# Response: 200 OK
```

Database Check:
```sql
SELECT COUNT(*) as SessionExpiredCount FROM AuditLogs 
WHERE EventType='SessionExpired' AND SessionId='...';
-- Expected: 1
```

**Step 2: Call again with same JWT (duplicate)**
```bash
curl -X POST "$BASE_URL/api/auth/token-expired" \
  -H "Content-Type: application/json" \
  -d '{"token":"'$JWT'"}'

# Response: 200 OK (still succeeds, but doesn't create duplicate)
```

Database Check:
```sql
SELECT COUNT(*) as SessionExpiredCount FROM AuditLogs 
WHERE EventType='SessionExpired' AND SessionId='...';
-- Expected: 1 (unchanged - duplicate prevented)
```

**Step 3: Multiple duplicates**
```bash
# Call 5 times with same JWT
for i in {1..5}; do
  curl -X POST "$BASE_URL/api/auth/token-expired" \
    -H "Content-Type: application/json" \
    -d '{"token":"'$JWT'"}'
  sleep 0.1
done
```

Database Check:
```sql
SELECT SessionId, COUNT(*) as Count FROM AuditLogs 
WHERE EventType='SessionExpired'
GROUP BY SessionId
HAVING COUNT(*) > 1;
-- Expected: Empty (no duplicates)
```

### Validation Criteria

? First call creates entry  
? Subsequent calls return 200 OK  
? No duplicate entries created  
? Deduplication works at DB level  

---

## Test Case 5: Role Assignment Safety

### Objective
Verify that roles are assigned exactly once, without duplicates.

### Setup
```bash
BASE_URL="https://localhost:7147"
EMAIL="role-safety-test@example.com"
PASSWORD="Test@123456"
```

### Test Steps

**Step 1: Register with specific role**
```bash
curl -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"User"}'

# Response: 200 OK
```

Database Check:
```sql
SELECT r.Name as Role, COUNT(*) as Count 
FROM AspNetUserRoles ur
JOIN AspNetRoles r ON ur.RoleId = r.Id
JOIN AspNetUsers u ON ur.UserId = u.Id
WHERE u.Email='role-safety-test@example.com'
GROUP BY r.Name;

-- Expected: 1 row with Role="User", Count=1
```

**Step 2: Try to register again with same role (idempotent)**
```bash
curl -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"User"}'

# Response: 200 OK
```

Database Check:
```sql
SELECT r.Name as Role, COUNT(*) as Count 
FROM AspNetUserRoles ur
JOIN AspNetRoles r ON ur.RoleId = r.Id
JOIN AspNetUsers u ON ur.UserId = u.Id
WHERE u.Email='role-safety-test@example.com'
GROUP BY r.Name;

-- Expected: 1 row with Role="User", Count=1 (unchanged)
```

**Step 3: Try different role (conflict)**
```bash
curl -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"'$PASSWORD'","role":"ServiceProvider"}'

# Response: 400 Bad Request
```

Database Check:
```sql
-- No change - user still has only User role
```

### Validation Criteria

? Role assigned exactly once  
? Idempotent retry returns success  
? No duplicate roles created  
? Different role rejected  

---

## Automated Test Script

### Prerequisites
```bash
# Install jq for JSON parsing
sudo apt-get install jq

# Set base URL
BASE_URL="https://localhost:7147"
```

### Full Retry-Safe Test
```bash
#!/bin/bash

echo "=========================================="
echo "Auth API Retry-Safe Validation"
echo "=========================================="

# Test 1: Idempotent Registration
echo ""
echo "Test 1: Idempotent Registration"
EMAIL="retry-$(date +%s)@example.com"

for attempt in 1 2 3; do
  echo "  Attempt $attempt..."
  curl -s -X POST "$BASE_URL/api/auth/register" \
    -H "Content-Type: application/json" \
    -d '{"email":"'$EMAIL'","password":"Test@123456","role":"User"}' | jq .
done

# Test 2: Immutable Login
echo ""
echo "Test 2: Immutable Login (Different JWTs)"
curl -s -X POST "$BASE_URL/api/auth/register" \
  -d '{"email":"'$EMAIL'","password":"Test@123456","role":"User"}' > /dev/null

JWT1=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -d '{"email":"'$EMAIL'","password":"Test@123456"}' | jq -r '.token')

JWT2=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -d '{"email":"'$EMAIL'","password":"Test@123456"}' | jq -r '.token')

if [ "$JWT1" != "$JWT2" ]; then
  echo "  ? Different JWTs generated (immutable)"
else
  echo "  ? Same JWT - immutability FAILED"
fi

# Test 3: Logout Append-Only
echo ""
echo "Test 3: Logout Append-Only"
echo "  Logout 1 (should succeed)..."
curl -s -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT1" | jq .

echo "  Logout 2 (should fail - 401)..."
curl -s -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $JWT1" | jq .

echo ""
echo "=========================================="
echo "All retry-safe tests completed!"
echo "=========================================="
```

---

## Expected Results Summary

| Test | Expected Result | Validation |
|------|-----------------|------------|
| **Registration (Idempotent)** | All requests return 200 OK | User count = 1 |
| **Login (Immutable)** | Different JWTs each time | Different SessionIds |
| **Logout (Append-Only)** | New entries each time | No duplicate Logout entries |
| **Token Expiry** | No duplicates created | One SessionExpired per session |
| **Role Assignment** | Assigned exactly once | No duplicate role assignments |

---

## Success Criteria

? **All 5 tests pass without failures**  
? **Database has no duplicates**  
? **Audit trail is complete**  
? **HTTP status codes are consistent**  
? **No state corruption on retries**  

---

**Status**: ? **READY FOR TESTING**

All mechanisms implemented and verified in code.
Run these tests to confirm retry-safe guarantees in your environment.

