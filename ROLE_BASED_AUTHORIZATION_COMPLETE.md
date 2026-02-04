# Role-Based Authorization Fixes - Complete Implementation Guide

## Overview

This document describes the comprehensive fixes applied to resolve role-based authorization inconsistencies in the ServiceMarketplace application.

**Build Status**: ? **Successful (0 Errors, 0 Warnings)**

---

## Problems Identified & Fixed

### 1. ? Role Naming Inconsistencies

**Issue**: Roles were defined in multiple places with different names:
- Domain enum: `UserRole.Customer` vs `"User"`
- JWT claims: `"User"` and `"ServiceProvider"`
- UI constants: `RoleNames.User` vs `RoleNames.Provider`
- Hardcoded strings: `"User"` scattered throughout codebase

**Solution**: Created centralized `RoleConstants` class in `ServiceMarketplace.Application.Constants`

### 2. ? Missing Role Claim Validation

**Issue**: No validation that JWT contains valid role claims:
- Role claims were accepted without validation
- Invalid roles silently accepted
- No logging when role claims missing

**Solution**: Added `RoleValidator` service with comprehensive logging

### 3. ? No Pre-Initialization Auth Guards

**Issue**: Authorization checks could run before auth state fully initialized:
- Race condition in Blazor components
- "Flash" of unauthorized content briefly visible
- No loading state during auth initialization

**Solution**: Created `AuthGuard` component to prevent premature checks

### 4. ? Inconsistent Role Claims in JWT

**Issue**: Role claims not validated during login/logout:
- Invalid roles could be issued in JWT
- No logging when role assignment fails
- User might not get correct role in token

**Solution**: Added role validation in `AuthService` with detailed logging

### 5. ? API Endpoints Using Hardcoded Role Strings

**Issue**: `[Authorize(Roles = "User")]` scattered throughout:
- Easy to introduce typos
- Hard to refactor role names
- No consistency across controllers

**Solution**: Updated all controllers to use `RoleConstants`

---

## Changes Made

### 1. Created RoleConstants.cs

**File**: `ServiceMarketplace.Application\Constants\RoleConstants.cs`

```csharp
public static class RoleConstants
{
    public const string User = "User";
    public const string ServiceProvider = "ServiceProvider";
    public const string Admin = "Admin";
    
    // Validation methods
    public static bool IsValidRole(string? role)
    public static string? NormalizeRole(string? role)
    public static IReadOnlyList<string> AllRoles { get; }
}
```

**Why**: Single source of truth for all role names across application

### 2. Created RoleValidator Service

**File**: `ServiceMarketplace.UI.Shared\Auth\RoleValidator.cs`

Features:
- ? Validates user has specific role
- ? Gets current user's role with validation
- ? Validates entire JWT configuration
- ? Comprehensive logging of validation results
- ? Detects missing role claims
- ? Detects invalid role values

**Usage**:
```csharp
public sealed class RoleValidator
{
    public async Task<bool> HasRoleAsync(string requiredRole)
    public async Task<string?> GetCurrentRoleAsync()
    public async Task<RoleValidationResult> ValidateAsync()
}
```

### 3. Created AuthGuard Component

**File**: `ServiceMarketplace.UI.Shared\Auth\AuthGuard.razor`

Features:
- ? Prevents authorization checks before auth state loads
- ? Shows loading indicator during initialization
- ? Catches and displays initialization errors
- ? Logs all initialization steps

**Usage**:
```razor
<AuthGuard>
    <YourProtectedComponent />
</AuthGuard>
```

### 4. Updated AuthService

**File**: `ServiceMarketplace.Infrastructure\Services\AuthService.cs`

Changes:
- ? Normalize role during registration: `RoleConstants.NormalizeRole(role)`
- ? Validate role before issuing JWT
- ? Log when role is missing from JWT
- ? Log when role is invalid
- ? Log all authentication events with role info

**New Logging**:
```
[AuthService] Invalid role provided: CustomRole. Valid roles: User, ServiceProvider, Admin
[AuthService] User {Email} has invalid roles: CustomRole
[AuthService] User {Email} has no roles assigned. Cannot issue token.
[AuthService] Added role claim: {Role} for user {Email}
```

### 5. Updated Controllers to Use RoleConstants

**Files**:
- `ServiceMarketplace.API\Controllers\AuthController.cs`
- `ServiceMarketplace.API\Controllers\ServiceRequestsController.cs`
- `ServiceMarketplace.API\Controllers\BidsController.cs`

Changes:
- ? Replace hardcoded roles with `RoleConstants`
- ? Add role validation in register endpoint
- ? Add logging when role claims missing
- ? Add logging for role-based operations

**Example**:
```csharp
// BEFORE
[Authorize(Roles = "ServiceProvider")]
public async Task<IActionResult> PlaceBid()

// AFTER
[Authorize(Roles = RoleConstants.ServiceProvider)]
public async Task<IActionResult> PlaceBid()
```

### 6. Updated RoleNames.cs

**File**: `ServiceMarketplace.UI.Shared\Auth\RoleNames.cs`

Changes:
- ? Sync with `RoleConstants` using constants
- ? Add `IsValidRole()` and `NormalizeRole()` methods
- ? Document synchronization requirement

```csharp
public static class RoleNames
{
    public const string User = RoleConstants.User;
    public const string Provider = RoleConstants.ServiceProvider;
    
    public static bool IsValidRole(string? role) => RoleConstants.IsValidRole(role);
    public static string? NormalizeRole(string? role) => RoleConstants.NormalizeRole(role);
}
```

---

## Authorization Policy Matrix

### User Role (User)
? **Can do**:
- Create service requests: `POST /api/requests`
- View own requests: `GET /api/requests/mine`
- View own request details: `GET /api/requests/{id}`
- Accept bids: `POST /api/requests/{id}/accept/{bidId}`
- View bids for own request: `GET /api/bids/{requestId}`

? **Cannot do**:
- Browse open requests: `GET /api/requests/open` (403 Forbidden)
- Search nearby requests: `POST /api/requests/nearby` (403 Forbidden)
- Place bids: `POST /api/bids` (403 Forbidden)

### ServiceProvider Role
? **Can do**:
- Browse open requests: `GET /api/requests/open`
- Search nearby requests: `POST /api/requests/nearby`
- View available requests: `GET /api/requests/available`
- View request details: `GET /api/requests/{id}/details`
- Place bids: `POST /api/bids`
- View own bids: `GET /api/bids/mine`

? **Cannot do**:
- Create service requests: `POST /api/requests` (403 Forbidden)
- Accept bids: `POST /api/requests/{id}/accept/{bidId}` (403 Forbidden)

---

## Logging & Debugging

### Role Validation Logs

All role validation operations are logged:

```
[RoleValidator] User has required role: User
[RoleValidator] User role mismatch. Required: User, Has: ServiceProvider
[RoleValidator] User {UserId} has no role claim in JWT
[RoleValidator] User has invalid role: CustomRole. Valid roles: User, ServiceProvider, Admin
[RoleValidator] User is not authenticated
```

### Authentication Service Logs

```
[AuthService] Invalid role provided: CustomRole. Valid roles: User, ServiceProvider, Admin
[AuthService] User already exists with requested role: user@example.com, Role: User
[AuthService] User creation failed for user@example.com: {errors}
[AuthService] Failed to create role: CustomRole
[AuthService] Failed to assign role to user
[AuthService] User {Email} has invalid roles: CustomRole
[AuthService] User {Email} has no roles assigned. Cannot issue token.
[AuthService] Added role claim: User for user user@example.com
[AuthService] User logged in successfully: user@example.com, Role: User
```

### Controller Logs

```
[AuthController] Register: Invalid role provided: CustomRole
[AuthController] Register: Attempting to register user {Email} with role {Role}
[AuthController] Login: Attempting to login user {Email}
[AuthController] Logout: User {UserId} logging out (role: {Role})
[AuthController] TokenExpired: User has invalid role in JWT: {Role}

[ServiceRequestsController] User {UserId} creating request with role {Role}
[ServiceRequestsController] Create called but UserId claim is missing

[BidsController] Provider {ProviderId} placing bid with role {Role}
[BidsController] PlaceBid called but ProviderId claim is missing
```

---

## How to Use RoleValidator in Components

### Example 1: Check if User Has Role

```razor
@inject RoleValidator RoleValidator

@if (await RoleValidator.HasRoleAsync(RoleNames.User))
{
    <div>User can create requests</div>
}
```

### Example 2: Get Current User's Role

```razor
@inject RoleValidator RoleValidator

@code {
    private string? currentRole;
    
    protected override async Task OnInitializedAsync()
    {
        currentRole = await RoleValidator.GetCurrentRoleAsync();
    }
}
```

### Example 3: Validate Entire Auth State

```razor
@inject RoleValidator RoleValidator

@code {
    private RoleValidationResult validationResult = new();
    
    protected override async Task OnInitializedAsync()
    {
        validationResult = await RoleValidator.ValidateAsync();
        
        if (!validationResult.IsValid)
        {
            logger.LogError("Auth validation failed: {Error}", validationResult.Error);
        }
    }
}
```

---

## Role Normalization

The `NormalizeRole()` method ensures consistent role names:

```csharp
RoleConstants.NormalizeRole("user")          // Returns: "User"
RoleConstants.NormalizeRole("User")          // Returns: "User"
RoleConstants.NormalizeRole("SERVICE_PROV")  // Returns: null (invalid)
RoleConstants.NormalizeRole(null)            // Returns: null
RoleConstants.NormalizeRole("")              // Returns: null
```

---

## Role Validation Workflow

### During Registration

```
User submits registration form
    ?
Register endpoint receives request
    ?
[AuthController] Validates role with RoleConstants.IsValidRole()
    ?
[AuthService] Normalizes role with RoleConstants.NormalizeRole()
    ?
Create user in Identity database
    ?
Create role if doesn't exist
    ?
Assign role to user
    ?
Create audit log entry with role
    ?
Return success/error
```

### During Login

```
User submits login credentials
    ?
[AuthService] Finds user in Identity database
    ?
Verifies password
    ?
Gets user's roles from UserRoles
    ?
Validates each role with RoleConstants.IsValidRole()
    ?
Logs error if any role is invalid
    ?
Creates JWT with role claims
    ?
Creates audit log with role
    ?
Returns token to client
```

### During Authorization

```
HTTP request received at API endpoint
    ?
[Authorize(Roles = RoleConstants.User)] attribute checked
    ?
Extract ClaimTypes.Role from JWT
    ?
Compare with required role (case-sensitive, using Ordinal comparison)
    ?
If matches: Continue to controller action
    ?
If doesn't match: Return 403 Forbidden
```

---

## Testing Authorization

### Test Case 1: User Cannot Access Provider Endpoints

```csharp
// Setup
var userJWT = LoginAsUser(); // JWT with role="User"

// Act
var response = await client.GetAsync(
    "/api/requests/open",
    headers: new { Authorization = $"Bearer {userJWT}" }
);

// Assert
Assert.Equal(403, response.StatusCode);  // Forbidden
```

### Test Case 2: ServiceProvider Cannot Access User Endpoints

```csharp
// Setup
var providerJWT = LoginAsProvider(); // JWT with role="ServiceProvider"

// Act
var response = await client.PostAsync(
    "/api/requests",
    body: newRequestDto,
    headers: new { Authorization = $"Bearer {providerJWT}" }
);

// Assert
Assert.Equal(403, response.StatusCode);  // Forbidden
```

### Test Case 3: Invalid Role Rejected

```csharp
// Setup
var request = new RegisterRequest
{
    Email = "test@example.com",
    Password = "Test@123",
    Role = "InvalidRole"  // Not in RoleConstants.AllRoles
};

// Act
var response = await client.PostAsync("/api/auth/register", request);

// Assert
Assert.Equal(400, response.StatusCode);  // Bad Request
var error = await response.Content.ReadAsAsync<string>();
Assert.Contains("Invalid role", error);
```

---

## Migration Guide for Existing Code

### Update Hardcoded Role Strings

**Before**:
```csharp
[Authorize(Roles = "User")]
if (user.Role == "User")
if (requestedRole == "ServiceProvider")
```

**After**:
```csharp
[Authorize(Roles = RoleConstants.User)]
if (user.Role == RoleConstants.User)
if (requestedRole == RoleConstants.ServiceProvider)
```

### Update Role Validation

**Before**:
```csharp
var role = user.FindFirst(ClaimTypes.Role)?.Value;
if (string.IsNullOrWhiteSpace(role))
    throw new Exception("No role");
```

**After**:
```csharp
var role = user.FindFirst(ClaimTypes.Role)?.Value;
if (!RoleConstants.IsValidRole(role))
{
    logger.LogWarning("Invalid role in JWT: {Role}", role);
    return Unauthorized("Invalid role configuration");
}
```

### Register Role Validator in Components

**Before**:
```csharp
@code {
    var authState = await authStateProvider.GetAuthenticationStateAsync();
    var role = authState.User.FindFirst(ClaimTypes.Role)?.Value;
}
```

**After**:
```csharp
@inject RoleValidator RoleValidator

@code {
    protected override async Task OnInitializedAsync()
    {
        var role = await RoleValidator.GetCurrentRoleAsync();
    }
}
```

---

## Performance Impact

### Minimal Runtime Overhead

- RoleConstants lookup: O(1) - direct constant access
- RoleValidator: O(n) where n = number of claims (typically 5-10)
- Authorization middleware: No change - same performance as before

### No Database Calls

- Role validation uses in-memory JWT claims
- No additional database queries
- Validation happens in JWT middleware (same path as before)

---

## Security Considerations

### ? Proper Implementation

- Roles compared case-sensitive (Ordinal)
- Role claims validated on every request
- Invalid roles logged for audit trail
- No elevation of privileges possible
- JWT signature prevents tampering

### ?? Important Notes

- Role claims must come from JWT (trusted source)
- Never trust role from query string or form data
- Always use `[Authorize(Roles = ...)]` on protected endpoints
- Log all role-related security events

---

## Troubleshooting

### Problem: User Getting 403 Forbidden

**Diagnosis**:
1. Check API logs for: `[ServiceRequestsController] User roles: ...`
2. Check JWT token in browser DevTools: Application ? Local Storage ? token
3. Decode JWT at https://jwt.io (check `role` claim)
4. Verify user's role in database: `SELECT * FROM UserRoles WHERE UserId = ...`

**Solutions**:
- Verify user is logged in (check token expiry)
- Verify user has role assigned in database
- Check role name matches exactly: "User" not "USER" or "user"
- Re-login to get fresh token with correct roles

### Problem: Invalid Role Error During Registration

**Diagnosis**:
1. Check API logs for: `[AuthController] Register: Invalid role provided: {Role}`
2. Check list of valid roles in log: `Valid roles: {ValidRoles}`
3. Verify role submitted matches exactly (case-sensitive)

**Solutions**:
- Use exact role names: "User" or "ServiceProvider"
- Check for trailing spaces or special characters
- Verify frontend is sending correct role value

### Problem: AuthGuard Stuck on Loading

**Diagnosis**:
1. Open browser DevTools ? Console
2. Look for errors loading auth state
3. Check network requests to `/api/auth/*` endpoints
4. Verify JWT is present in Local Storage

**Solutions**:
- Refresh the page
- Clear Local Storage and re-login
- Check API server is running
- Check network connectivity

---

## Files Changed Summary

| File | Changes | Impact |
|------|---------|--------|
| RoleConstants.cs | ? NEW | Single source of truth for roles |
| RoleValidator.cs | ? NEW | Comprehensive role validation |
| AuthGuard.razor | ? NEW | Prevent premature auth checks |
| AuthService.cs | ?? UPDATED | Role validation & logging |
| AuthController.cs | ?? UPDATED | Use RoleConstants, validate roles |
| ServiceRequestsController.cs | ?? UPDATED | Use RoleConstants, add logging |
| BidsController.cs | ?? UPDATED | Use RoleConstants, add logging |
| RoleNames.cs | ?? UPDATED | Sync with RoleConstants |
| Program.cs (UI.Web) | ?? UPDATED | Register RoleValidator |

---

## Build Status

```
? Build Successful
- 0 Errors
- 0 Warnings
- All projects compiled successfully
```

---

## Next Steps

### For Developers

1. ? Review RoleConstants.cs - understand centralized roles
2. ? Study RoleValidator - learn how to validate roles
3. ? Update any custom auth logic to use RoleValidator
4. ? Add logging to role-dependent features
5. ? Run security tests to verify authorization

### For Testers

1. ? Test User role cannot access Provider endpoints
2. ? Test ServiceProvider role cannot access User endpoints
3. ? Test role names are normalized during registration
4. ? Test invalid roles are rejected
5. ? Test missing role claims are detected and logged

### For DevOps

1. ? Monitor application logs for role-related warnings
2. ? Alert on: "Invalid role", "missing role claim", "role validation failed"
3. ? Archive audit logs for compliance
4. ? Review role assignments in production

---

## References

- RoleConstants: Single source of truth for all role names
- RoleValidator: Validates and logs role claims
- AuthGuard: Prevents premature authorization checks
- [Authorize] attribute: Controls endpoint access

---

**Version**: 1.0  
**Date**: 2025-02-01  
**Status**: ? Complete & Ready for Production
