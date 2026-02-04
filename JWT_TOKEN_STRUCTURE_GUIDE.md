# JWT Token Structure - Visual Reference Guide

## Quick Reference

```
JWT Token Structure:
[Header].[Payload].[Signature]

Example:
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJlbWFpbCI6InVzZXJAZXhhbXBsZS5jb20iLCJqdGkiOiJmNDdhYzEwYi01OGNjLTQzNzItYTU2Ny0wZTAyYjJjM2Q0NzkiLCJzdWIiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJpYXQiOjE3Mzg1OTg3MDAicm9sZSI6IlVzZXIiLCJpc3MiOiJodHRwczovL2xvY2FsaG9zdDo3MTQ3IiwiYXVkIjoiaHR0cHM6Ly9sb2NhbGhvc3Q6NzI0MSIsImV4cCI6MTczODU5OTMwMH0.QnHLu3qDKf_tKSB8L45Lb8h_Fw2sXBB6ZQ9wlVsCb_E
```

---

## Header (Part 1)

**Encoded**:
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
```

**Decoded**:
```json
{
  "alg": "HS256",           // Algorithm: HMAC SHA-256
  "typ": "JWT"              // Type: JSON Web Token
}
```

---

## Payload (Part 2) - Claims

**Encoded**:
```
eyJuYW1laWQiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJlbWFpbCI6InVzZXJAZXhhbXBsZS5jb20iLCJqdGkiOiJmNDdhYzEwYi01OGNjLTQzNzItYTU2Ny0wZTAyYjJjM2Q0NzkiLCJzdWIiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJpYXQiOjE3Mzg1OTg3MDAsInJvbGUiOiJVc2VyIiwiaXNzIjoiaHR0cHM6Ly9sb2NhbGhvc3Q6NzE0NyIsImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0OjcyNDEiLCJleHAiOjE3Mzg1OTkzMDB9
```

**Decoded**:
```json
{
  "nameid": "550e8400-e29b-41d4-a716-446655440000",     // ? ClaimTypes.NameIdentifier (user ID)
  "email": "user@example.com",                          // ? ClaimTypes.Email
  "jti": "f47ac10b-58cc-4372-a567-0e02b2c3d479",       // ? JwtRegisteredClaimNames.Jti (session ID)
  "sub": "550e8400-e29b-41d4-a716-446655440000",       // ? JwtRegisteredClaimNames.Sub (user ID)
  "iat": 1738598700,                                    // ? JwtRegisteredClaimNames.Iat (issued at, Unix timestamp)
  "role": "User",                                       // ? ClaimTypes.Role (authorization role)
  "iss": "https://localhost:7147",                      // ? Issuer (API URL)
  "aud": "https://localhost:7241",                      // ? Audience (UI URL)
  "exp": 1738599300                                     // ? Expiration (Unix timestamp, 10 min from iat)
}
```

---

## Claims Breakdown

| # | Claim | Type | Source | Purpose |
|---|-------|------|--------|---------|
| 1 | **nameid** | `ClaimTypes.NameIdentifier` | user.Id | Primary user identifier |
| 2 | **email** | `ClaimTypes.Email` | user.Email | User's email address |
| 3 | **jti** | `JwtRegisteredClaimNames.Jti` | Guid.NewGuid() | Unique session ID per login |
| 4 | **sub** | `JwtRegisteredClaimNames.Sub` | user.Id | Backup user identifier (OAuth standard) |
| 5 | **iat** | `JwtRegisteredClaimNames.Iat` | DateTimeOffset.UtcNow.ToUnixTimeSeconds() | Token issued at time (Unix timestamp) |
| 6 | **role** | `ClaimTypes.Role` | userManager.GetRolesAsync() | Authorization role (User, ServiceProvider, Admin) |
| 7 | **iss** | (Standard) | config["Jwt:Issuer"] | Token issuer (API URL) |
| 8 | **aud** | (Standard) | config["Jwt:Audience"] | Token audience (UI URL) |
| 9 | **exp** | (Standard) | DateTime.UtcNow.AddMinutes(10) | Token expiration (10 minutes) |

---

## Signature (Part 3)

**Encoded**:
```
QnHLu3qDKf_tKSB8L45Lb8h_Fw2sXBB6ZQ9wlVsCb_E
```

**Creation**:
```
HMACSHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  secret_key
)
```

**Verification**:
- ? Signature created using `SecurityAlgorithms.HmacSha256`
- ? Signed with key from `config["Jwt:Key"]`
- ? Server validates signature on each request

---

## Full Token Lifetime

### Timeline

```
T+0 seconds:
  - User submits email/password
  - AuthService.LoginAsync() called
  - Claims assembled
  - JWT created with:
    - iat = current Unix timestamp
    - exp = iat + 600 seconds (10 minutes)
  - Token signed with secret key
  - Token returned to client as string
  - Client stores in localStorage

T+0 to T+600 seconds (next 10 minutes):
  - Client includes token in Authorization header
  - Server validates:
    - ? Signature matches secret key
    - ? Issuer matches config
    - ? Audience matches config
    - ? Expiration time has not passed
    - ? Required claims present
  - All checks pass ? Request accepted (200 OK)

T+600 seconds (10 minutes):
  - TokenAuthenticationStateProvider timer fires
  - Client detects: DateTime.UtcNow > exp timestamp
  - Client notifies server: POST /api/auth/token-expired
  - Server records SessionExpired audit log
  - Client logs user out
  - Token cleared from localStorage

T+600+ seconds:
  - Token in localStorage is no longer valid
  - Server rejects with 401 Unauthorized
  - ValidateLifetime check fails
  - User must log in again to get new token
```

---

## Timestamp Conversion

### Unix Timestamp Format

The JWT uses Unix timestamps (seconds since epoch):

```
iat: 1738598700  ?  2025-02-03 18:45:00 UTC
exp: 1738599300  ?  2025-02-03 18:55:00 UTC
```

### Verification in Code

```csharp
// JWT creation
var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();     // Current time as Unix timestamp
var exp = iat + 600;                                      // Add 600 seconds (10 minutes)

// Token validation
var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
if (DateTime.UtcNow > expirationTime)
{
    // Token expired
}
```

---

## How Tokens Are Used

### Step 1: Login Returns Token

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}

HTTP/1.1 200 OK
Content-Type: application/json

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-02-03T18:55:00Z"
}
```

### Step 2: Client Stores Token

```javascript
// Blazor C#
await tokenStorage.SaveTokenAsync(response.Token);

// Or JavaScript
localStorage.setItem('token', response.token);
```

### Step 3: Client Includes Token in Requests

```http
GET /api/requests/mine
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Step 4: Server Validates Token

```csharp
// Automatic (via JwtBearerDefaults middleware)
// 1. Extract "Bearer {token}" from Authorization header
// 2. Decode token
// 3. Verify signature
// 4. Check claims: iss, aud, exp
// 5. If valid: Create ClaimsPrincipal with all claims
// 6. If invalid: Return 401 Unauthorized
```

### Step 5: Controller Accesses Claims

```csharp
[Authorize(Roles = "User")]
[HttpPost("api/requests")]
public async Task<IActionResult> CreateRequest(...)
{
    // JWT was automatically validated by middleware
    // Claims are now available via User property
    
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);  // From "nameid" claim
    var role = User.FindFirstValue(ClaimTypes.Role);              // From "role" claim
    var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Jti); // From "jti" claim
    
    // Use claims in business logic
    var result = await _service.CreateAsync(dto, userId);
    return Ok(new { RequestId = result.Id });
}
```

---

## Role Authorization Example

### Token with User Role
```json
{
  "role": "User",
  "nameid": "user-123"
}
```

**Accessing endpoints**:
```
? POST /api/requests              [Authorize(Roles = "User")] ? ALLOWED
? GET /api/requests/mine          [Authorize(Roles = "User")] ? ALLOWED
? POST /api/bids                  [Authorize(Roles = "ServiceProvider")] ? 403 FORBIDDEN
? GET /api/requests/open          [Authorize(Roles = "ServiceProvider")] ? 403 FORBIDDEN
```

### Token with ServiceProvider Role
```json
{
  "role": "ServiceProvider",
  "nameid": "provider-456"
}
```

**Accessing endpoints**:
```
? POST /api/requests              [Authorize(Roles = "User")] ? 403 FORBIDDEN
? GET /api/requests/mine          [Authorize(Roles = "User")] ? 403 FORBIDDEN
? POST /api/bids                  [Authorize(Roles = "ServiceProvider")] ? ALLOWED
? GET /api/requests/open          [Authorize(Roles = "ServiceProvider")] ? ALLOWED
```

---

## Multiple Roles Example (If Applicable)

If a user had multiple roles (e.g., User AND Admin):

```json
{
  "role": ["User", "Admin"],
  "nameid": "admin-789"
}
```

**Authorization check**:
```csharp
[Authorize(Roles = "Admin")]  // ? Allowed (user has Admin role)
[Authorize(Roles = "User")]   // ? Allowed (user has User role)
[Authorize(Roles = "ServiceProvider")]  // ? Not allowed (user doesn't have this role)
```

Current implementation: Each user has exactly ONE role per registration.

---

## Decoding Tools

### Online Tool
- **jwt.io**: Paste token to decode and verify signature

### Command Line
```bash
# Decode using jq
echo "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c" | cut -d'.' -f2 | base64 -d | jq
```

### PowerShell
```powershell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
$payload = $token.Split('.')[1] + "=" * (4 - ($token.Split('.')[1].Length % 4))
$decoded = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload))
$decoded | ConvertFrom-Json | Format-Table
```

### C#
```csharp
var handler = new JwtSecurityTokenHandler();
var token = handler.ReadJwtToken(tokenString);

foreach (var claim in token.Claims)
{
    Console.WriteLine($"{claim.Type}: {claim.Value}");
}

Console.WriteLine($"Expires: {token.ValidTo}");
```

---

## Security Notes

? **Signature**: HMAC-SHA256 prevents token tampering
? **Expiration**: 10-minute timeout limits exposure of compromised tokens
? **SessionId (jti)**: Unique per login enables audit tracking and logout
? **Issuer/Audience**: Prevents token reuse on wrong services
? **Roles**: Fine-grained access control per endpoint
? **Storage**: Client stores in localStorage (vulnerable to XSS, but standard for SPAs)

---

## Summary

| Item | Value | Status |
|------|-------|--------|
| Algorithm | HMAC-SHA256 | ? Secure |
| Lifetime | 10 minutes | ? Reasonable |
| Required Claims | sub, email, role, exp, iss, aud | ? All present |
| SessionId | Unique per login (jti) | ? Enabled |
| Authorization | Role-based (ClaimTypes.Role) | ? Correct |
| Validation | Signature, expiration, issuer, audience | ? Comprehensive |

---

**JWT Implementation**: ? **COMPLETE AND CORRECT**
