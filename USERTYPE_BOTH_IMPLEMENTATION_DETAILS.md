# UserType.Both Implementation Details

## Architecture Overview

```
???????????????????????????????????????????????????????????????
?                     USER REGISTRATION                       ?
?                                                             ?
?  Select Role: User | ServiceProvider | Both               ?
???????????????????????????????????????????????????????????????
                     ?
                     ?
???????????????????????????????????????????????????????????????
?                    AUTH SERVICE                             ?
?                                                             ?
?  if (role == "Both")                                        ?
?    - Assign "User" role in database                         ?
?    - Assign "ServiceProvider" role in database             ?
?    - Set UserType = UserType.Both                          ?
???????????????????????????????????????????????????????????????
                     ?
                     ?
???????????????????????????????????????????????????????????????
?              USER ROLES TABLE (Database)                    ?
?                                                             ?
?  UserId  ? RoleId            ?                             ?
?  ?????????????????????????????                             ?
?  user-1  ? user-role-id      ?  ? User can create requests ?
?  user-1  ? provider-role-id  ?  ? User can place bids      ?
???????????????????????????????????????????????????????????????
                     ?
                     ?
???????????????????????????????????????????????????????????????
?                   LOGIN / JWT GENERATION                    ?
?                                                             ?
?  foreach (var role in roles) {                             ?
?    claims.Add(new Claim(ClaimTypes.Role, role));          ?
?  }                                                          ?
?                                                             ?
?  Result: JWT with both roles                               ?
?  { "role": ["User", "ServiceProvider"] }                   ?
???????????????????????????????????????????????????????????????
                     ?
                     ?
???????????????????????????????????????????????????????????????
?                 AUTHORIZATION CHECKS                        ?
?                                                             ?
?  API: [Authorize(Roles = "User")]                           ?
?       ? Check if roles contain "User"                       ?
?       ? ? "User" found ? Allow                             ?
?       ? ? "ServiceProvider" doesn't matter                 ?
?                                                             ?
?  API: [Authorize(Roles = "ServiceProvider")]                ?
?       ? Check if roles contain "ServiceProvider"            ?
?       ? ? "ServiceProvider" found ? Allow                  ?
?       ? ? "User" doesn't matter                            ?
???????????????????????????????????????????????????????????????
                     ?
                     ?
???????????????????????????????????????????????????????????????
?                  NAVIGATION / DASHBOARDS                    ?
?                                                             ?
?  if (roles.contains("ServiceProvider")) {                   ?
?    navigate("/provider/dashboard");                         ?
?  } else if (roles.contains("User")) {                       ?
?    navigate("/user/dashboard");                            ?
?  }                                                          ?
?                                                             ?
?  Result: Both user ? /provider/dashboard (by default)      ?
???????????????????????????????????????????????????????????????
```

---

## Code Flow - Registration

### Step 1: User Selects "Both"

**File**: `Register.razor` (Registration Form)

```html
<select @bind-Value="_model.Role">
    <option value="User">User</option>
    <option value="ServiceProvider">ServiceProvider</option>
    <option value="Both">Both</option>
</select>
```

### Step 2: Form Submitted to API

**File**: `RegisterRequest.cs` (DTO)

```csharp
public class RegisterRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Role { get; set; } = null!;  // "Both" selected
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
}
```

### Step 3: API Registration Endpoint

**File**: `ServiceMarketplace.API/Controllers/AuthController.cs`

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request)
{
    var result = await _authService.RegisterAsync(
        request.Email,
        request.Password,
        request.Role,  // "Both"
        request.FirstName,
        request.LastName,
        request.DateOfBirth
    );
    
    if (!result.Success)
        return BadRequest(result);
    
    return Ok(new { message = "Registration successful" });
}
```

### Step 4: AuthService Processes Registration

**File**: `ServiceMarketplace.Infrastructure/Services/AuthService.cs`

```csharp
public async Task<AuthRegisterResult> RegisterAsync(
    string email, 
    string password, 
    string role,  // "Both"
    string firstName,
    string lastName,
    DateTime dateOfBirth)
{
    // Normalize and validate role
    var normalizedRole = RoleConstants.NormalizeRole(role);
    // normalizedRole = "Both"
    
    // Create user with UserType = UserType.Both
    var user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        DateOfBirth = dateOfBirth,
        UserType = GetUserTypeFromRole("Both"),  // UserType.Both
        CreatedAtUtc = DateTime.UtcNow
    };
    
    // Create user in Identity
    var result = await userManager.CreateAsync(user, password);
    
    // SPECIAL HANDLING FOR "BOTH"
    if (normalizedRole == RoleConstants.Both)
    {
        // Assign BOTH User AND ServiceProvider roles
        await userManager.AddToRoleAsync(user, RoleConstants.User);
        await userManager.AddToRoleAsync(user, RoleConstants.ServiceProvider);
        
        // Optional: Could also assign "Both" role if you want
        // await userManager.AddToRoleAsync(user, RoleConstants.Both);
    }
    else
    {
        // Regular single role assignment
        await userManager.AddToRoleAsync(user, normalizedRole);
    }
    
    return new AuthRegisterResult(true, null, null);
}

private static UserType GetUserTypeFromRole(string normalizedRole)
{
    return normalizedRole switch
    {
        RoleConstants.User => UserType.User,
        RoleConstants.ServiceProvider => UserType.Provider,
        RoleConstants.Both => UserType.Both,
        _ => UserType.User
    };
}
```

### Step 5: Database State After Registration

```sql
-- User Created
SELECT * FROM Users WHERE Email = 'both.user@example.com'
? UserType = 3 (Both)

-- Roles Assigned
SELECT * FROM UserRoles WHERE UserId = 'user-id'
? Row 1: UserId = 'user-id', RoleId = 'user-role-id'
? Row 2: UserId = 'user-id', RoleId = 'provider-role-id'

-- Roles Table (for reference)
SELECT * FROM Roles
? Id = 'user-role-id', Name = 'User'
? Id = 'provider-role-id', Name = 'ServiceProvider'
? Id = 'both-role-id', Name = 'Both' (optional)
```

---

## Code Flow - Login

### Step 1: User Logs In

**API Endpoint**: `POST /api/auth/login`

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    var result = await _authService.LoginAsync(
        request.Email,
        request.Password,
        Request.Headers.UserAgent.ToString(),
        Request.HttpContext.Connection.RemoteIpAddress?.ToString()
    );
    
    if (!result.Success)
        return Unauthorized(new { error = result.Error });
    
    return Ok(result.Payload);
}
```

### Step 2: AuthService Retrieves Roles from Database

```csharp
public async Task<AuthLoginResult> LoginAsync(
    string email, 
    string password, 
    string? userAgent, 
    string? ipAddress)
{
    // Find user by email
    var user = await userManager.FindByEmailAsync(email);
    
    // Validate password
    var validPassword = await userManager.CheckPasswordAsync(user, password);
    
    // Retrieve ALL roles for this user from UserRoles table
    var roles = await userManager.GetRolesAsync(user);
    // roles = ["User", "ServiceProvider"]  ? BOTH roles returned
    
    // ... rest of login logic
}
```

### Step 3: Build JWT Claims with All Roles

```csharp
// Build JWT claims
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, user.Id),
    new(ClaimTypes.Email, user.Email ?? email),
    new(JwtRegisteredClaimNames.Jti, sessionId),
    new(JwtRegisteredClaimNames.Sub, user.Id),
    new(JwtRegisteredClaimNames.Iat, 
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), 
        ClaimValueTypes.Integer64)
};

// Add ALL roles to claims (not just the first one!)
foreach (var role in roles)  // roles = ["User", "ServiceProvider"]
{
    claims.Add(new Claim(ClaimTypes.Role, role));
}
// Result: JWT has TWO role claims
// claims[4] = Claim(Type="role", Value="User")
// claims[5] = Claim(Type="role", Value="ServiceProvider")

// Log dual-role users for debugging
if (roles.Count == 2)
{
    logger.LogInformation("[AuthService] User {Email} has dual roles: {Roles}", 
        email, string.Join(", ", roles));
}

// Sign JWT with 10-minute expiration
var key = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
var expirationTime = DateTime.UtcNow.AddMinutes(10);

var token = new JwtSecurityToken(
    issuer: configuration["Jwt:Issuer"],
    audience: configuration["Jwt:Audience"],
    claims: claims,  // Includes both role claims
    expires: expirationTime,
    signingCredentials: credentials
);

// Return JWT to client
return new AuthLoginResult(
    true, 
    new AuthResultDto
    {
        Token = new JwtSecurityTokenHandler().WriteToken(token),
        ExpiresAt = token.ValidTo
    }, 
    null
);
```

### Step 4: Client Stores JWT

**File**: `TokenAuthenticationStateProvider.cs`

```csharp
public async Task LoginAsync(string token)
{
    // Store JWT in local storage
    await _tokenStorage.SetTokenAsync(token);
    
    // Parse JWT to extract claims
    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);
    
    // Extract roles from JWT
    var roles = jwt.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList();
    // roles = ["User", "ServiceProvider"]
    
    // Create authentication state with roles
    var identity = new ClaimsIdentity(jwt.Claims, "jwt");
    var principal = new ClaimsPrincipal(identity);
    var authState = new AuthenticationState(principal);
    
    // Notify Blazor of new auth state
    NotifyAuthenticationStateChanged(Task.FromResult(authState));
}
```

### Step 5: AuthRedirector Navigates Based on Roles

**File**: `AuthRedirector.cs`

```csharp
public async Task RedirectToDashboardAsync()
{
    var roles = await _authState.GetAllRolesAsync();
    // roles = ["User", "ServiceProvider"]
    
    var navigateTarget = DetermineDashboard(roles);
    // DetermineDashboard checks:
    // 1. Has "ServiceProvider" role? ? /provider/dashboard
    // 2. Has "User" role? ? /user/dashboard
    // 3. Default ? /user/dashboard
    
    // For Both user, returns "/provider/dashboard"
    _nav.NavigateTo(navigateTarget, forceLoad: false);
}

private static string DetermineDashboard(IList<string> roles)
{
    // Check if has ServiceProvider role (includes Both users)
    if (roles.Any(r => string.Equals(r, RoleNames.Provider, 
        StringComparison.Ordinal)))
    {
        return "/provider/dashboard";
    }
    
    // Check if has User role
    if (roles.Any(r => string.Equals(r, RoleNames.User, 
        StringComparison.Ordinal)))
    {
        return "/user/dashboard";
    }
    
    // Default
    return "/user/dashboard";
}
```

---

## Code Flow - Authorization

### When Both User Makes API Request

```
1. Client sends request with JWT:
   
   POST /api/requests HTTP/1.1
   Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
   
2. API receives request, JWT Bearer middleware extracts token
3. Middleware validates JWT signature
4. Middleware extracts all claims from JWT
   ? sub = "user-id"
   ? email = "both@example.com"
   ? role = ["User", "ServiceProvider"]
5. ClaimsPrincipal created with all claims
6. HttpContext.User set to ClaimsPrincipal
7. Controller action executes:

   [Authorize(Roles = "User")]
   [HttpPost]
   public async Task<IActionResult> Create(...)
   
8. ASP.NET Core checks: Does User have "User" role?
   ? User.Claims.Where(c => c.Type == ClaimTypes.Role)
   ? Returns ["User", "ServiceProvider"]
   ? Contains "User"? YES ?
   ? Authorization passes
   
9. Action executes, returns 200 OK

OR

10. Different action:
    
    [Authorize(Roles = "ServiceProvider")]
    [HttpPost]
    public async Task<IActionResult> PlaceBid(...)
    
11. ASP.NET Core checks: Does User have "ServiceProvider" role?
    ? User.Claims.Where(c => c.Type == ClaimTypes.Role)
    ? Returns ["User", "ServiceProvider"]
    ? Contains "ServiceProvider"? YES ?
    ? Authorization passes
    
12. Action executes, returns 200 OK
```

---

## Database Schema

### UserType Enum

```csharp
public enum UserType
{
    User = 1,
    Provider = 2,
    Both = 3,
    Admin = 4
}
```

### Users Table

```sql
CREATE TABLE Users (
    Id NVARCHAR(450) PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    UserType INT NOT NULL DEFAULT 1,  -- 1=User, 2=Provider, 3=Both, 4=Admin
    CreatedAtUtc DATETIME2 NOT NULL,
    -- ... other columns
);
```

### Roles Table

```sql
CREATE TABLE Roles (
    Id NVARCHAR(450) PRIMARY KEY,
    Name NVARCHAR(256) NOT NULL UNIQUE
);

-- Pre-seeded with:
INSERT INTO Roles VALUES ('id1', 'User');
INSERT INTO Roles VALUES ('id2', 'ServiceProvider');
INSERT INTO Roles VALUES ('id3', 'Both');
INSERT INTO Roles VALUES ('id4', 'Admin');
```

### UserRoles Table (Junction)

```sql
CREATE TABLE UserRoles (
    UserId NVARCHAR(450) NOT NULL,
    RoleId NVARCHAR(450) NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

-- For Both user:
INSERT INTO UserRoles VALUES ('user-id', 'user-role-id');     -- User role
INSERT INTO UserRoles VALUES ('user-id', 'provider-role-id');  -- ServiceProvider role
-- Optional: INSERT INTO UserRoles VALUES ('user-id', 'both-role-id');  -- Both role
```

---

## Authorization Policies (Program.cs)

```csharp
builder.Services.AddAuthorization(options =>
{
    // Strictly User role only
    options.AddPolicy("UserOnly", policy =>
        policy.RequireRole(RoleConstants.User));

    // Strictly ServiceProvider role only
    options.AddPolicy("ProviderOnly", policy =>
        policy.RequireRole(RoleConstants.ServiceProvider));

    // User role OR Both role
    options.AddPolicy("UserOrBoth", policy =>
        policy.RequireRole(RoleConstants.User, RoleConstants.Both));
    // Equivalent to: role == "User" OR role == "Both"

    // ServiceProvider role OR Both role
    options.AddPolicy("ProviderOrBoth", policy =>
        policy.RequireRole(RoleConstants.ServiceProvider, RoleConstants.Both));
    // Equivalent to: role == "ServiceProvider" OR role == "Both"

    // Dual-role only
    options.AddPolicy("BothRoleOnly", policy =>
        policy.RequireRole(RoleConstants.Both));
});
```

**Policy Usage**:
```csharp
[Authorize(Policy = "UserOrBoth")]
public async Task<IActionResult> Create(...)

[Authorize(Policy = "ProviderOrBoth")]
public async Task<IActionResult> PlaceBid(...)
```

**Current Implementation** still uses `[Authorize(Roles = "...")]` which works fine since:
```csharp
[Authorize(Roles = "User")]
// Same as requiring ANY of these roles
// Both users have "User" role, so they pass ?
```

---

## Extension Points

### Add More Dual-Role Types

Future support for combinations like:
- User + Admin
- ServiceProvider + Admin
- User + ServiceProvider + Admin

Would follow same pattern:
1. Add role to RoleConstants
2. Add logic to AuthService.RegisterAsync
3. Users automatically get multiple role claims
4. Authorization automatically works

### Add Role Switching UI

Allow users to switch primary role:
```csharp
// UI dropdown: "Switch to User Dashboard" / "Switch to Provider Dashboard"
// Could also prefer one over the other
var preferences = await _preferencesService.GetRolePreferencesAsync(userId);
var primaryRole = preferences.PrimaryRole; // "User" or "ServiceProvider"
NavigateToDashboard(primaryRole);
```

---

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public async Task RegisterAsync_WithBothRole_AssignsBothRoles()
{
    // Arrange
    var email = "both@test.com";
    var role = RoleConstants.Both;
    
    // Act
    var result = await _authService.RegisterAsync(email, "Pass1!", role, ...);
    
    // Assert
    Assert.True(result.Success);
    var user = await _userManager.FindByEmailAsync(email);
    var roles = await _userManager.GetRolesAsync(user);
    Assert.Contains(RoleConstants.User, roles);
    Assert.Contains(RoleConstants.ServiceProvider, roles);
}

[Fact]
public async Task LoginAsync_WithBothRole_IncludesBothRolesInJwt()
{
    // Arrange
    var email = "both@test.com";
    // Pre-create user with both roles
    
    // Act
    var result = await _authService.LoginAsync(email, "Pass1!", null, null);
    
    // Assert
    Assert.True(result.Success);
    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(result.Payload.Token);
    var roles = jwt.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList();
    Assert.Contains(RoleConstants.User, roles);
    Assert.Contains(RoleConstants.ServiceProvider, roles);
}
```

### Integration Tests

```csharp
[Fact]
public async Task CreateRequest_BothUser_Succeeds()
{
    // Register as Both
    await _api.PostAsync("/api/auth/register", new { Role = "Both", ... });
    
    // Login
    var loginResponse = await _api.PostAsync("/api/auth/login", new { ... });
    var token = loginResponse.Value.Token;
    
    // Create request as Both user
    var createResponse = await _api.PostAsync(
        "/api/requests",
        new { Title = "Test", ... },
        new AuthenticationHeaderValue("Bearer", token)
    );
    
    // Assert
    Assert.Equal(200, createResponse.StatusCode);
}

[Fact]
public async Task PlaceBid_BothUser_Succeeds()
{
    // Setup: Create request, register as Both user
    var token = await _loginAsBothUser();
    
    // Place bid
    var bidResponse = await _api.PostAsync(
        "/api/bids",
        new { ServiceRequestId = ..., Amount = 100 },
        new AuthenticationHeaderValue("Bearer", token)
    );
    
    // Assert
    Assert.Equal(200, bidResponse.StatusCode);
}
```

---

## Troubleshooting

### JWT Missing Role Claims

**Symptom**: 403 Forbidden on authorized endpoint  
**Cause**: Roles not in UserRoles table  
**Check**: 
```sql
SELECT * FROM UserRoles WHERE UserId = 'user-id'
```
**Fix**: Manually add missing role:
```sql
INSERT INTO UserRoles (UserId, RoleId)
SELECT 'user-id', Id FROM Roles WHERE Name = 'User'
```

### Navigation to Wrong Dashboard

**Symptom**: Both user navigates to /user/dashboard instead of /provider/dashboard  
**Cause**: AuthRedirector.DetermineDashboard logic  
**Check**: Browser console logs  
**Fix**: Verify JWT contains both roles, restart browser

### Registration Fails

**Symptom**: 400 Bad Request on registration  
**Cause**: "Both" role not yet seeded in database  
**Check**: 
```sql
SELECT * FROM Roles WHERE Name IN ('User', 'ServiceProvider', 'Both')
```
**Fix**: Seed roles via RoleSeedingService:
```csharp
await roleManager.CreateAsync(new IdentityRole("Both"));
```

---

**Implementation Complete**: ?  
**Documentation Complete**: ?  
**Ready for Production**: ?

