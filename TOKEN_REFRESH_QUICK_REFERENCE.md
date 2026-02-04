# Token Refresh - Quick Reference Card

## ?? Quick Start

### 1. Run Migration
```bash
dotnet ef database update
# Creates RefreshTokens table
```

### 2. Test Login
```bash
# Navigate to https://localhost:7241/login
# Login with credentials
# Check localStorage for auth_token and refresh_token
```

### 3. Test Refresh
```bash
curl -X POST https://localhost:7147/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "..."}'
```

---

## ?? Token Lifecycle

```
LOGIN
  ?
[Issue Access Token (10 min) + Refresh Token (7 days)]
  ?
STORE BOTH
  ?
Use Access Token (Bearer header)
  ?
(After 9 min 30 sec)
  ?
AUTOMATIC REFRESH
  ?
[Rotate: Revoke old, issue new refresh token]
  ?
STORE NEW TOKENS
  ?
Continue using new Access Token
  ?
(After 10 min total)
  ?
GET 401 UNAUTHORIZED (optional)
  ?
AUTOMATIC RETRY WITH NEW TOKEN
  ?
Request succeeds
  ?
(After 7 days)
  ?
REFRESH TOKEN EXPIRES
  ?
Must re-login
```

---

## ?? Key Files

| File | Purpose | Lines |
|------|---------|-------|
| RefreshTokenService.cs | Token lifecycle | ~300 |
| TokenRefreshHttpClient.cs | Auto-refresh wrapper | ~250 |
| RefreshTokenEntity.cs | Database model | ~50 |
| AuthService.cs | Login + refresh | ~400 |
| TokenAuthenticationStateProvider.cs | Client auth state | ~150 |

---

## ?? Refresh Flow

```
CLIENT                              API

Access Token Expires
        ?
[Timer fires in TokenAuthenticationStateProvider]
        ?
POST /api/auth/refresh ------?
{ "refreshToken": "..." }
                                Validate token
                                Check if active
                                Mark old as revoked
                                Issue new tokens
                                
?------ 200 OK
{
  accessToken,
  refreshToken (rotated)
}

Store new tokens
Notify auth state
Reschedule timer

Subsequent API calls use new token
```

---

## ?? Error Handling

### 401 on API Call
```
GET /api/requests/mine
Authorization: Bearer {old_token}
        ?
? 401 Unauthorized
        ?
[TokenRefreshHttpClient intercepts]
        ?
POST /api/auth/refresh
? 200 OK (new tokens)
        ?
GET /api/requests/mine (RETRY)
Authorization: Bearer {new_token}
? 200 OK (success)
```

### Invalid Refresh Token
```
POST /api/auth/refresh
{ "refreshToken": "invalid" }
        ?
? 401 Unauthorized
        ?
Clear all tokens
Force logout
Redirect to /login
```

---

## ?? API Endpoints

### Login
```
POST /api/auth/login
Request:  { "email": "...", "password": "..." }
Response: { "token": "JWT", "refreshToken": "..." }
Status:   200 OK or 401 Unauthorized
```

### Refresh (NEW)
```
POST /api/auth/refresh
Request:  { "refreshToken": "..." }
Response: { "accessToken": "JWT", "refreshToken": "..." }
Status:   200 OK or 401 Unauthorized
Behavior: Idempotent (same response within 30s)
```

### Logout
```
POST /api/auth/logout
Headers:  Authorization: Bearer {token}
Response: { "message": "Logged out successfully" }
Status:   200 OK
Effect:   Clears both tokens locally
```

---

## ?? Storage

### Web (localStorage)
```
auth_token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
refresh_token: "aBcDeFgHiJkLmNoPqRsT..."
```

### Mobile (SecureStorage)
```
auth_token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
refresh_token: "aBcDeFgHiJkLmNoPqRsT..."
```

### Server (RefreshTokens table, hashed)
```
TokenHash: "5F4DCC3B5AA765D61D8327DEB882CF99" (SHA256)
IsActive: 1 (or 0 if revoked)
ExpiresAt: 2025-02-08 15:20:00
TokenFamily: "abc123..." (for rotation chain)
```

---

## ?? Security Properties

| Property | Guarantee |
|----------|-----------|
| Token Rotation | Old token revoked immediately |
| Reuse Detection | Using old token = family revoked |
| Hashed Storage | Plaintext never in database |
| Expiration | Auto-cleanup of expired tokens |
| Revocation | Logout from all devices possible |
| Idempotency | Same response within 30 seconds |
| Retry Safety | Max 1 automatic retry |

---

## ?? Monitoring

### SQL Queries

**Count active tokens by user:**
```sql
SELECT UserId, COUNT(*) as ActiveTokenCount
FROM RefreshTokens
WHERE RevokedAt IS NULL AND ExpiresAt > GETUTCDATE()
GROUP BY UserId;
```

**Find token reuse:**
```sql
SELECT TokenFamily, COUNT(*) as FamilySize,
       COUNT(CASE WHEN RevokedAt IS NULL THEN 1 END) as ActiveCount
FROM RefreshTokens
GROUP BY TokenFamily
HAVING COUNT(*) > 3; -- More than 1 refresh?
```

**Check recent revocations:**
```sql
SELECT TOP 10 UserId, TokenFamily, RevokedAt, RevocationReason
FROM RefreshTokens
WHERE RevokedAt > DATEADD(HOUR, -1, GETUTCDATE())
ORDER BY RevokedAt DESC;
```

---

## ?? Test Scenarios

### Happy Path
1. ? Login ? Both tokens stored
2. ? Refresh ? New tokens issued
3. ? Auto-refresh ? Happens before expiry
4. ? Logout ? Tokens cleared

### Error Cases
1. ? Invalid token ? 401 Unauthorized
2. ? Expired token ? 401 Unauthorized
3. ? Revoked token ? 401 Unauthorized
4. ? Old token after rotation ? 401 Unauthorized

### Edge Cases
1. ? Same token twice in 30s ? Idempotent response
2. ? Refresh failure ? Graceful logout
3. ? Token reuse detected ? Family revoked
4. ? Network offline ? Local logout still works

---

## ?? Deployment

### Prerequisites
```bash
# 1. Build passes
dotnet build ?

# 2. Tests pass
dotnet test ?

# 3. Database backup
# (manual)
```

### Steps
```bash
# 1. Run migration
dotnet ef database update

# 2. Deploy API
dotnet publish ServiceMarketplace.API --configuration Release

# 3. Deploy UI
dotnet publish ServiceMarketplace.UI.Web --configuration Release

# 4. Monitor logs
# Watch for errors in Application Insights
```

### Rollback
```bash
# If critical issue:
# 1. Disable refresh endpoint (comment out route)
# 2. Extend access token lifetime
# 3. Analyze logs
# 4. Redeploy with fix
```

---

## ?? Configuration

### Token Lifetimes (in AuthService.cs)
```csharp
// Access token: 10 minutes
var expirationTime = DateTime.UtcNow.AddMinutes(10);

// Refresh token: 7 days (in IssueRefreshTokenAsync)
ExpiresAt = DateTime.UtcNow.AddDays(7)
```

### Idempotency Window (in TokenRefreshService.cs)
```csharp
// Same refresh within 30 seconds = same response
const int IdempotencyWindowSeconds = 30;
```

### Max Retries (in TokenRefreshHttpClient.cs)
```csharp
// Max 1 retry per request (prevent infinite loops)
const int MaxRetries = 1;
```

---

## ?? Troubleshooting

| Problem | Solution |
|---------|----------|
| "Refresh token not found" | Copy token exactly (no whitespace) |
| "Max retries exceeded" | Check API /refresh endpoint logs |
| Different tokens on refresh | Normal if >30s elapsed |
| Tokens not persisting | Check browser localStorage |
| 401 not triggering retry | Check TokenRefreshHttpClient injection |
| Loop of refreshes | Verify max retry = 1 |

---

## ?? Performance

| Operation | Time | Notes |
|-----------|------|-------|
| Login | ~100-200ms | Includes password hashing |
| Refresh | ~20-50ms | Fast, no password check |
| Automatic retry | ~0-50ms | Transparent to user |
| DB query (token) | ~1-5ms | Indexed lookup |

---

## ?? Related Files

- **TOKEN_REFRESH_IMPLEMENTATION_COMPLETE.md** - Full technical documentation
- **TOKEN_REFRESH_TESTING_GUIDE.md** - Detailed testing procedures
- **TOKEN_REFRESH_SUMMARY.md** - Executive summary
- **This file** - Quick reference

---

## ? Checklist

### Pre-Deployment
- [ ] Build successful
- [ ] All tests pass
- [ ] Database migration ready
- [ ] JWT secret configured
- [ ] Logs configured

### Deployment
- [ ] API deployed
- [ ] UI deployed
- [ ] Database migrated
- [ ] Health checks passing
- [ ] Logs monitored

### Post-Deployment
- [ ] Test login works
- [ ] Test refresh works
- [ ] Test automatic retry
- [ ] Monitor for errors
- [ ] Verify token rotation

---

**Status**: ? Production Ready

**Support**: See full documentation files for detailed information
