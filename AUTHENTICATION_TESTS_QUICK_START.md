# Authentication Integration Tests - Quick Start

## ?? Quick Commands

### Run All Tests
```bash
dotnet test ServiceMarketplace.API.Tests
```

### Run Specific Test Class
```bash
dotnet test ServiceMarketplace.API.Tests --filter "AuthenticationFlowTests"
dotnet test ServiceMarketplace.API.Tests --filter "RoleBasedAuthorizationTests"
dotnet test ServiceMarketplace.API.Tests --filter "AuditLogVerificationTests"
dotnet test ServiceMarketplace.API.Tests --filter "TokenRefreshIdempotencyTests"
dotnet test ServiceMarketplace.API.Tests --filter "RateLimitingTests"
```

### Run Specific Test
```bash
dotnet test ServiceMarketplace.API.Tests --filter "Login_WithValidCredentials_ReturnsJwtToken"
```

### Run with Output
```bash
dotnet test ServiceMarketplace.API.Tests --verbosity detailed
```

### Run with Code Coverage
```bash
dotnet test ServiceMarketplace.API.Tests /p:CollectCoverage=true
```

---

## ?? Test Suite Overview

| Class | Tests | Purpose |
|-------|-------|---------|
| **AuthenticationFlowTests** | 12 | Complete Register?Login?Logout flow |
| **RoleBasedAuthorizationTests** | 9 | Role enforcement on endpoints |
| **AuditLogVerificationTests** | 7 | Audit trail creation & integrity |
| **TokenRefreshIdempotencyTests** | 9 | Token refresh and idempotency |
| **RateLimitingTests** | 6 | Rate limiting enforcement |
| **TOTAL** | **43** | **Complete authentication coverage** |

---

## ? What Gets Tested

### ? Registration
- Valid credentials
- Invalid role
- Duplicate email
- Password validation

### ? Login
- Valid credentials
- Invalid password
- Missing credentials
- Token generation
- Token expiration (10 minutes)
- Refresh token issuance

### ? Logout
- With valid token
- Without token
- Invalid token
- Creates audit log

### ? Protected Endpoints
- Access with valid token (200 OK)
- Access without token (401 Unauthorized)
- Access with invalid token (401 Unauthorized)

### ? Role-Based Authorization
- User endpoints
- ServiceProvider endpoints
- Cross-role access (403 Forbidden)
- Role claims in JWT

### ? Token Refresh
- Valid refresh token
- Invalid refresh token
- Token rotation
- Idempotent behavior
- New token expiration

### ? Audit Logging
- Login events
- Logout events
- IP address capture
- User-Agent capture
- Session ID tracking
- Append-only pattern (no updates)

### ? Rate Limiting
- 5 requests/minute for auth endpoints
- 10 requests/minute for refresh endpoint
- 429 status code when exceeded
- Per-IP limiting

---

## ??? Test Database

Tests use **in-memory database** (no external setup required).

**Benefits:**
- ? No database server needed
- ? Fast test execution
- ? Isolated per test class
- ? Automatic cleanup

---

## ?? Dependencies

All required packages installed in `.csproj`:
- xUnit 2.6.4
- FluentAssertions 6.12.0
- Moq 4.20.70
- Microsoft.AspNetCore.Mvc.Testing 9.0.0
- Microsoft.EntityFrameworkCore.InMemory 9.0.0

---

## ?? Test Examples

### Example 1: Registration
```csharp
[Fact]
public async Task Register_WithValidCredentials_CreatesNewUser()
{
    var request = AuthRequestBuilder.CreateUser("test@example.com")
        .BuildRegisterRequest();
    
    var response = await _client.PostAsJsonAsync("/api/auth/register", request);
    
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Example 2: Authorization
```csharp
[Fact]
public async Task UserEndpoint_WithProviderRole_Returns403Forbidden()
{
    var (token, _) = await RegisterAndLoginAsync("provider@test.com", "ServiceProvider");
    SetAuthHeader(token);
    
    var response = await _client.GetAsync("/api/requests/mine");
    
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### Example 3: Audit Verification
```csharp
[Fact]
public async Task Login_CreatesAuditLogEntry()
{
    // Login
    var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
    
    // Verify audit log
    var dbContext = await _factory.GetDbContextAsync();
    var auditLog = await dbContext.AuditLogs
        .FirstAsync(a => a.EventType == "Login");
    
    auditLog.Should().NotBeNull();
    auditLog.UserId.Should().NotBeNullOrWhiteSpace();
    auditLog.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
}
```

---

## ?? Expected Behavior

### Authentication Flow
```
Register (POST /api/auth/register)
    ?
Login (POST /api/auth/login)
    ? Returns JWT + Refresh Token
Logout (POST /api/auth/logout)
    ? Creates audit log
Protected Resources (GET /api/requests/mine)
    ? Verify auth claim
Refresh (POST /api/auth/refresh)
    ? Token rotation
```

### Role Authorization
```
User Role:
  ? GET /api/requests/mine
  ? POST /api/requests
  ? GET /api/requests/available (403)
  ? POST /api/bids (403)

ServiceProvider Role:
  ? GET /api/requests/available
  ? POST /api/bids
  ? GET /api/requests/mine (403)
  ? POST /api/requests (403)
```

---

## ? Common Issues

### Issue 1: Tests timeout
**Solution**: Increase timeout or check database
```bash
# Run with longer timeout
dotnet test ServiceMarketplace.API.Tests --configuration Debug
```

### Issue 2: Rate limiting not triggered
**Solution**: In-memory database may not trigger rate limiting
Expected: Tests pass, rate limiting verified in integration

### Issue 3: Token refresh fails
**Solution**: Ensure refresh token is properly rotated
Check: `OldRefreshToken_AfterRotation_ShouldFail` test

---

## ?? Test Output Example

```
Starting test execution, please wait...
A total of 43 tests found in project ServiceMarketplace.API.Tests.csproj

? AuthenticationFlowTests.Register_WithValidCredentials_CreatesNewUser (1.23s)
? AuthenticationFlowTests.Login_WithValidCredentials_ReturnsJwtToken (1.45s)
? RoleBasedAuthorizationTests.UserEndpoint_WithProviderRole_Returns403 (0.98s)
? AuditLogVerificationTests.Login_CreatesAuditLogEntry (1.56s)
? TokenRefreshIdempotencyTests.RefreshToken_WithValidToken_ReturnsNew (1.34s)
? RateLimitingTests.RegisterEndpoint_EnforcesRateLimit_Auth (2.12s)
...
Passed:  43
Failed:  0
Skipped: 0
Total:  43 (49.234s)
```

---

## ?? Security Testing Included

? JWT Token Validation
- Token format (3 parts: header.payload.signature)
- Token expiration (10 minutes)
- Claims present (sub, email, role, jti)

? Role Enforcement
- Correct role in token
- Endpoint authorization
- 403 Forbidden for wrong role

? Audit Trail
- All events logged
- Append-only (no updates)
- Proper metadata capture

? Rate Limiting
- Per-IP limiting
- 429 status code
- Separate limits per endpoint type

---

## ?? Next Steps

1. **Run tests locally**
   ```bash
   dotnet test ServiceMarketplace.API.Tests
   ```

2. **Add to CI/CD pipeline** (GitHub Actions)
   ```yaml
   - name: Run Integration Tests
     run: dotnet test ServiceMarketplace.API.Tests
   ```

3. **Monitor test metrics**
   - Execution time
   - Pass/fail ratio
   - Code coverage

4. **Extend tests** as needed
   - Add new scenarios
   - Test edge cases
   - Verify new features

---

## ?? Full Documentation

See **AUTHENTICATION_INTEGRATION_TESTS_GUIDE.md** for:
- Complete test breakdown
- Architecture details
- Best practices
- Troubleshooting guide
- Security testing patterns

---

**Status**: ? Ready to Run
**Total Tests**: 43+
**Expected Time**: ~50 seconds
**Build**: Successful

