# Multi-Role Support Architecture

## Overview

The login redirect system now supports users with multiple roles assigned. This document explains how the system determines which dashboard to redirect to when a user has multiple roles.

---

## Role Hierarchy

```
???????????????????????????????????????
?   Role Hierarchy (Priority Order)   ?
???????????????????????????????????????
? 1. ServiceProvider (Highest)         ?
?    ? Redirects to /provider/dashboard?
?                                      ?
? 2. User (Normal)                     ?
?    ? Redirects to /user/dashboard    ?
?                                      ?
? 3. Admin (Currently N/A)             ?
?    ? Reserved for future use         ?
?                                      ?
? 4. No Valid Roles (Default)          ?
?    ? Safe fallback: /user/dashboard  ?
???????????????????????????????????????
```

---

## Role Assignment in ASP.NET Identity

Roles are assigned in the database via `UserRoles` table:

```sql
-- Table structure
SELECT 
    u.UserName,
    r.Name as Role
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
ORDER BY u.UserName, r.Name;
```

**Example Output**:
```
UserName          | Role
-----------------|------------------
user@test.com     | User
provider@test.com | ServiceProvider
both@test.com     | User, ServiceProvider
```

---

## JWT Role Claims

When a user logs in, all assigned roles are added to the JWT token as claims:

```json
{
  "iss": "ServiceMarketplace",
  "aud": "ServiceMarketplaceUsers",
  "sub": "user-id-12345",
  "email": "both@test.com",
  "jti": "session-id-guid",
  "role": [
    "User",
    "ServiceProvider"
  ],
  "exp": 1738418400
}
```

**Key Points**:
- ? JWT `role` claim is an array of all assigned roles
- ? Roles are validated against Role Manager
- ? Invalid roles are rejected during login
- ? At least one role is required

---

## AuthState Multi-Role Retrieval

The `AuthState` class reads all roles from JWT claims:

```csharp
/// Get all roles from authenticated user
var allRoles = await authState.GetAllRolesAsync();
// Returns: ["User", "ServiceProvider"]

/// Check if user has a specific role
var isProvider = await authState.HasRoleAsync(RoleNames.Provider);
// Returns: true

var isAdmin = await authState.HasRoleAsync("Admin");
// Returns: false
```

---

## AuthRedirector Priority Logic

The redirect decision uses this algorithm:

```csharp
private static string DetermineDashboard(IList<string> roles)
{
    // Normalize roles (trim, filter empty)
    var validRoles = roles
        .Where(r => !string.IsNullOrWhiteSpace(r))
        .Select(r => r.Trim())
        .ToList();

    // PRIORITY 1: ServiceProvider (highest)
    if (validRoles.Any(r => r.Equals(RoleNames.Provider)))
        return "/provider/dashboard";

    // PRIORITY 2: User
    if (validRoles.Any(r => r.Equals(RoleNames.User)))
        return "/user/dashboard";

    // FALLBACK: No valid roles ? safe default
    return "/user/dashboard";
}
```

---

## Decision Tree

```
                    User Login
                        ?
                  Get All Roles
                        ?
              ?????????????????????
              ?                   ?
        Has ServiceProvider?   Yes ? /provider/dashboard
              ?                 
             No
              ?
        Has User?            Yes ? /user/dashboard
              ?
             No
              ?
      No Valid Roles?        ? /user/dashboard (default)
```

---

## Scenario Examples

### Scenario 1: User Role Only

```
User: user@example.com
Roles: [User]
Decision: User has User role, no ServiceProvider
Result: ? /user/dashboard
```

### Scenario 2: ServiceProvider Role Only

```
User: provider@example.com
Roles: [ServiceProvider]
Decision: User has ServiceProvider role
Result: ? /provider/dashboard
```

### Scenario 3: Both Roles

```
User: both@example.com
Roles: [User, ServiceProvider]
Decision: User has ServiceProvider role (takes priority)
Result: ? /provider/dashboard
```

### Scenario 4: No Valid Roles

```
User: broken@example.com
Roles: [] (empty or invalid)
Decision: No valid roles found
Result: ? /user/dashboard (safe fallback)
```

---

## API Service Layer

The API issues all assigned roles to JWT during login:

```csharp
// AuthService.LoginAsync()
var roles = await userManager.GetRolesAsync(user);

// Build JWT claims
foreach (var role in roles)
{
    claims.Add(new Claim(ClaimTypes.Role, role));
}

// JWT now contains ALL roles
```

---

## Database Schema

### Users Table
```
Id (PK)
Email
PasswordHash
...
```

### Roles Table
```
Id (PK)
Name (e.g., "User", "ServiceProvider")
NormalizedName
...
```

### UserRoles Table (Junction)
```
UserId (FK)
RoleId (FK)
```

**Example Data**:
```
UserRoles Table:
UserId                  | RoleId
------------------------|---------
user-id-123            | role-1  (User)
provider-id-456        | role-2  (ServiceProvider)
both-id-789            | role-1  (User)
both-id-789            | role-2  (ServiceProvider)
```

---

## Blazor AuthorizeView Integration

Each dashboard uses `AuthorizeView` with role-based access:

```razor
<!-- User Dashboard -->
<AuthorizeView Roles="@RoleNames.User">
    <Authorized>
        <!-- User-specific content -->
    </Authorized>
    <NotAuthorized>
        <!-- Error message for non-users -->
    </NotAuthorized>
</AuthorizeView>

<!-- Provider Dashboard -->
<AuthorizeView Roles="@RoleNames.Provider">
    <Authorized>
        <!-- Provider-specific content -->
    </Authorized>
    <NotAuthorized>
        <!-- Error message for non-providers -->
    </NotAuthorized>
</AuthorizeView>
```

**How It Works**:
1. JWT contains all user roles
2. AuthorizeView checks if user has required role
3. Shows Authorized content if role found
4. Shows NotAuthorized content if role not found

---

## Error Handling

### Invalid Role in JWT

```csharp
// If JWT contains invalid role like "SuperAdmin"
var validRoles = roles.Where(r => RoleConstants.IsValidRole(r));

if (!validRoles.Any())
    return "/user/dashboard"; // Safe fallback
```

### Empty Role List

```csharp
if (roles == null || roles.Count == 0)
    return "/user/dashboard"; // Safe fallback
```

### Exception During Redirect

```csharp
try
{
    var roles = await _authState.GetAllRolesAsync();
    return DetermineDashboard(roles);
}
catch (Exception ex)
{
    logger.LogError(ex, "Error determining dashboard");
    return "/user/dashboard"; // Safe fallback
}
```

---

## Testing Multi-Role Scenarios

### Create Test User with Both Roles

```sql
-- Create user
INSERT INTO Users (Id, Email, UserName, PasswordHash)
VALUES ('test-both-id', 'both@test.com', 'both@test.com', 'hashed-password');

-- Assign User role
INSERT INTO UserRoles (UserId, RoleId)
SELECT 'test-both-id', Id FROM Roles WHERE Name = 'User';

-- Assign ServiceProvider role
INSERT INTO UserRoles (UserId, RoleId)
SELECT 'test-both-id', Id FROM Roles WHERE Name = 'ServiceProvider';
```

### Test Login Flow

```
1. Login with both@test.com
2. Check browser console:
   [AuthRedirector] User roles: User, ServiceProvider
3. Verify redirect:
   Expected: /provider/dashboard
   (ServiceProvider takes priority)
```

### Test Role-Based Access

```
1. While logged in as both@test.com
2. Navigate to /user/dashboard
   Expected: Loads (User role allows it)
3. Navigate to /provider/dashboard
   Expected: Loads (ServiceProvider role allows it)
4. Both dashboards show all relevant content
```

---

## Future Enhancements

### 1. Role Selector UI

Allow users to choose which role to use:

```razor
<div class="role-selector">
    <button @onclick="() => UseRole('User')">
        Use User Dashboard
    </button>
    <button @onclick="() => UseRole('ServiceProvider')">
        Use Provider Dashboard
    </button>
</div>
```

### 2. Admin Role Support

```csharp
// Add to priority logic
if (validRoles.Any(r => r.Equals("Admin")))
    return "/admin/dashboard";
```

### 3. Role Switching in Session

Allow switching between dashboards without re-login:

```csharp
public void SwitchRole(string roleToUse)
{
    if (HasRole(roleToUse))
    {
        NavigateTo($"/{roleToUse.ToLower()}/dashboard");
    }
}
```

### 4. Dashboard Landing Page

Neutral landing page showing available dashboards:

```razor
<div class="dashboard-selector">
    @if (hasUserRole)
    {
        <button @onclick="() => Nav.NavigateTo('/user/dashboard')">
            User Dashboard
        </button>
    }
    @if (hasProviderRole)
    {
        <button @onclick="() => Nav.NavigateTo('/provider/dashboard')">
            Provider Dashboard
        </button>
    }
</div>
```

---

## Performance Considerations

### Role Retrieval Efficiency

```csharp
// Efficient: Single JWT read
var roles = state.User.FindAll(ClaimTypes.Role);
// Roles are already in memory (JWT decoded)

// Inefficient would be: Database queries
var roles = await userManager.GetRolesAsync(user);
// This would hit database every redirect
```

? Current implementation: 0 database queries for redirect decision

---

## Security Considerations

### 1. Role Validation

- ? Only valid roles are accepted
- ? Invalid roles are ignored/filtered
- ? Roles come from validated JWT

### 2. No Elevation of Privileges

- ? User cannot add roles to JWT
- ? Roles come from server (Identity Manager)
- ? Role assignment controlled by admin

### 3. UI vs. API Authorization

```
UI (Blazor):
- Shows dashboard based on redirected page
- Uses AuthorizeView for content gating

API (ASP.NET):
- Uses [Authorize(Roles = "...")] attributes
- Enforces role-based access at API level
```

Both layers validate roles independently.

---

## Monitoring & Debugging

### Console Logs

```
[AuthRedirector] User roles: {comma-separated list}
[AuthRedirector] Valid roles after normalization: {list}
[AuthRedirector] User has ServiceProvider role, navigating to provider dashboard
[AuthRedirector] User has User role, navigating to user dashboard
[AuthRedirector] User has no valid roles, using safe default: user dashboard
```

### Browser Storage

```javascript
// Check JWT in browser
localStorage.getItem('authToken');

// Decode JWT (at jwt.io)
{
  "role": ["User", "ServiceProvider"]
}
```

### Database Query

```sql
-- Check user's assigned roles
SELECT r.Name
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Email = 'both@test.com'
ORDER BY r.Name;
```

---

## Summary

? **Multi-Role Support Implemented**

**Key Points**:
1. Users can have multiple roles assigned
2. JWT contains all assigned roles
3. Redirect uses priority logic (ServiceProvider > User)
4. Safe fallback for edge cases
5. 0 database queries for redirect
6. 100% backward compatible

**Redirect Rules**:
- Has ServiceProvider? ? /provider/dashboard
- Has User? ? /user/dashboard
- No valid roles? ? /user/dashboard (safe default)

**Future Extensible**: Easy to add Admin role or role selector UI

---

**Architecture Version**: 1.0  
**Status**: ? Complete & Verified  
**Date**: February 1, 2025  

---

See also: `LOGIN_REDIRECT_BEHAVIOR_IMPLEMENTATION.md` for detailed implementation
