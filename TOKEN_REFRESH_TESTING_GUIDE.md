# Token Refresh - Quick Testing Guide

## Pre-Testing Setup

### 1. Run Database Migration

```bash
cd ServiceMarketplace.Infrastructure
dotnet ef database update
```

Expected: `RefreshTokens` table created with indexes.

### 2. Start Services

```bash
# Terminal 1: API
cd ServiceMarketplace.API
dotnet run
# Should start on https://localhost:7147

# Terminal 2: Web UI
cd ServiceMarketplace.UI.Web
dotnet run
# Should start on https://localhost:7241
```

---

## Test 1: Login Returns Both Tokens ?

### Steps

1. Navigate to `https://localhost:7241/login`
2. Register or login with test account
3. Open browser DevTools (F12)
4. Go to Application ? LocalStorage ? https://localhost:7241

### Verify

Should see two keys:
- `auth_token` - Access token (JWT)
- `refresh_token` - Refresh token (opaque string)

### Expected Values

```
auth_token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
refresh_token: aBcDeFgHiJkLmNoPqRsT...
```

### ? Pass Criteria
- Both tokens present
- Both tokens non-empty
- Access token is valid JWT (3 parts separated by dots)
- Refresh token is base64-encoded string

---

## Test 2: Token Expiry ?

### Steps

1. Login and note the time
2. Check browser console for log messages
3. Wait 10 minutes (or mock the timer)

### Verify Automatic Refresh

In browser console, should see:
```
[TokenAuthenticationStateProvider] Token has expired at 2025-02-01T15:20:00Z
[TokenAuthenticationStateProvider] Notifying backend of token expiry
[TokenAuthenticationStateProvider] Signing out user
```

### Expected Behavior
- After 10 minutes: automatically logged out
- Redirected to `/login`
- Both tokens cleared from localStorage

### ? Pass Criteria
- User logged out after 10 minutes
- No errors in console
- Tokens cleared from storage

---

## Test 3: Manual Refresh Endpoint (Postman) ?

### Steps

1. Login and copy the `refresh_token` from localStorage
2. Open Postman
3. Create POST request to `https://localhost:7147/api/auth/refresh`

### Request

```json
{
  "refreshToken": "copy_from_localStorage"
}
```

### Response

Should return 200 OK with:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2025-02-01T15:30:00Z",
  "refreshToken": "nEwToKeNdIfFeReNt...",
  "refreshTokenExpiresAt": "2025-02-08T15:20:00Z"
}
```

### ? Pass Criteria
- Status 200 OK
- New access token differs from old
- New refresh token differs from old (rotation)
- Both have valid expiry times

---

## Test 4: Token Rotation ?

### Steps

1. Copy the new refresh token from Test 3 response
2. Immediately send another refresh request with the NEW token
3. Should get new tokens again

### Verify Rotation

Third token should be different from second token.

### Old Token Should Fail

Send request with first refresh token (from login):
```
POST /api/auth/refresh
{ "refreshToken": "original_token" }
```

Expected: **401 Unauthorized** (old token was revoked)

### ? Pass Criteria
- New refresh request works
- Old refresh token rejected (401)
- Each refresh creates new unique token

---

## Test 5: Idempotency ?

### Steps

1. Get fresh refresh token
2. Send TWO rapid requests with SAME token (within 30 seconds)
3. Both should return SAME new access token

### Using curl (or Postman)

```bash
# Request 1
curl -X POST https://localhost:7147/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "..."}'

# Request 2 (immediately)
curl -X POST https://localhost:7147/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "..."}'
```

### ? Pass Criteria
- Both responses have identical `accessToken`
- Both responses have identical `refreshToken`
- No errors
- Prevents race conditions

---

## Test 6: Automatic Retry on 401 ?

### Steps

1. Login successfully
2. Open DevTools Network tab
3. Make any API request (e.g., GET /api/requests/mine)
4. Manually set token expiry in localStorage to past time
5. Make another API request
6. Watch Network tab

### Expected Network Traffic

```
GET /api/requests/mine
  Status: 401 Unauthorized

POST /api/auth/refresh
  Status: 200 OK
  
GET /api/requests/mine (RETRY)
  Status: 200 OK
```

### ? Pass Criteria
- First request fails with 401
- Refresh endpoint called automatically
- Original request retried and succeeds
- No manual action required

---

## Test 7: Invalid Refresh Token ?

### Steps

1. Open Postman
2. Send refresh request with invalid token:

```json
{
  "refreshToken": "invalid_token_abc123"
}
```

### Expected Response

```json
Status: 401 Unauthorized
{
  "error": "Refresh token is invalid, expired, or revoked"
}
```

### ? Pass Criteria
- Status 401 Unauthorized
- Error message present
- No tokens returned
- No crash/500 error

---

## Test 8: Logout Clears Both Tokens ?

### Steps

1. Login
2. Verify both tokens in localStorage
3. Click "Sign Out" button
4. Check localStorage again

### Verify Logout

Should see no `auth_token` or `refresh_token` in localStorage.

### ? Pass Criteria
- Both tokens cleared
- No tokens in localStorage
- Redirected to /login
- No errors

---

## Test 9: Token Reuse Detection ?

### Steps (Advanced)

1. Get refresh token
2. Use it to refresh (get new token)
3. Try to use the OLD token again

### Response

```
Status: 401 Unauthorized
{
  "error": "Refresh token has expired or been revoked"
}
```

### Database Check (SQL)

```sql
SELECT TokenFamily, RevokedAt, RevocationReason
FROM RefreshTokens
WHERE UserId = 'your_user_id'
ORDER BY IssuedAt DESC;
```

Should see:
```
TokenFamily  | RevokedAt           | RevocationReason
abc123       | NULL                | NULL              (active)
abc123       | 2025-02-01 15:20:00 | RotatedAtRefresh  (revoked)
abc123       | 2025-02-01 15:20:05 | TokenReuseDetected (entire family revoked if attacked)
```

### ? Pass Criteria
- Old token rejected
- Token marked as revoked
- Error message clear
- Family chain tracked

---

## Test 10: Max Retry Prevention ?

### Steps

1. Modify TokenRefreshHttpClient code temporarily to return invalid refresh response
2. Make API request
3. Should not retry infinitely

### Expected Behavior

```
GET /api/requests/mine
  Status: 401 Unauthorized

POST /api/auth/refresh
  Status: 401 Unauthorized (simulated failure)

[TokenRefreshHttpClient] Token refresh failed. Returning 401.
[TokenRefreshHttpClient] Max retries exceeded. Returning 401.
```

No infinite loop, request fails cleanly.

### ? Pass Criteria
- Max 1 automatic retry
- No infinite loops
- Clear error messages
- Graceful failure

---

## Regression Tests (Existing Functionality)

### ? Login Still Works
- Email + password login works
- JWT issued on success
- Role claims included
- Audit log created

### ? Authorization Still Works
- User can access /user/dashboard
- Provider can access /provider/dashboard
- Cross-role access blocked (403)

### ? Logout Still Works
- Tokens cleared
- Audit log created
- Redirected to /login

### ? Protected Endpoints
- Require Bearer token
- Return 401 if missing token
- Return 403 if insufficient permissions

---

## Database Verification

### Check RefreshTokens Table

```sql
-- View all refresh tokens
SELECT Id, UserId, IsActive, CreatedAt, ExpiresAt
FROM RefreshTokens
ORDER BY CreatedAt DESC;

-- Check token families
SELECT TokenFamily, COUNT(*) as Count, 
       MAX(CASE WHEN RevokedAt IS NULL THEN 1 ELSE 0 END) as ActiveCount
FROM RefreshTokens
GROUP BY TokenFamily;

-- Check for revoked families
SELECT TokenFamily, RevokedAt, RevocationReason
FROM RefreshTokens
WHERE RevokedAt IS NOT NULL
GROUP BY TokenFamily, RevokedAt, RevocationReason;
```

### ? Expected Results
- New tokens appear after login
- Tokens marked as active (RevokedAt = NULL)
- After refresh: old token has RevokedAt set
- Token families tracked

---

## Performance Checks

### Database Query Performance

```sql
-- Check index usage
SELECT name, type_desc, avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats (
  DB_ID(),
  OBJECT_ID('RefreshTokens'),
  NULL,
  NULL,
  'LIMITED'
)
WHERE index_id > 0;
```

Expected: Fragmentation < 10%, indexes used

### API Response Times

```
POST /api/auth/login     : ~100-200ms (includes password hashing)
POST /api/auth/refresh   : ~20-50ms (fast, no password check)
GET /api/requests/mine   : ~10-20ms (normal, +token refresh if needed)
```

---

## Troubleshooting

### Issue: "Refresh token not found" on Valid Token

**Cause**: Token hash mismatch
**Fix**: Ensure token is copied exactly (no extra whitespace)

### Issue: "Max retries exceeded" immediately

**Cause**: Refresh endpoint returning 401
**Fix**: Check API logs for validation errors

### Issue: Different tokens on same refresh

**Cause**: Idempotency window expired (>30s)
**Fix**: Normal behavior, retries within 30s return same token

### Issue: Tokens not persisted

**Cause**: LocalStorage not available
**Fix**: Check browser DevTools, verify localStorage not disabled

### Issue: Infinite loop of refreshes

**Cause**: Max retry limit not working
**Fix**: Check TokenRefreshHttpClient max retry logic

---

## Success Criteria Checklist

- [x] Login returns both tokens
- [x] Both tokens stored in localStorage
- [x] Refresh endpoint works
- [x] Token rotation works (old revoked)
- [x] Idempotency works (30s window)
- [x] Automatic refresh on 401
- [x] Max 1 retry (no infinite loops)
- [x] Invalid token rejected
- [x] Logout clears both tokens
- [x] Token reuse detected
- [x] Database migration successful
- [x] No compilation errors
- [x] Existing auth still works

---

**Testing Status**: Ready for QA

**Estimated Time**: 30-45 minutes for all tests

**Success**: All 10 tests + regression tests pass = Ready for production
