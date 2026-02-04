# UserType Enum - Quick Reference

## Values

```csharp
UserType.User = 1          // Create requests, accept bids
UserType.Provider = 2      // Browse requests, place bids
UserType.Both = 3          // Has both capabilities
```

## Extension Methods

```csharp
// Type checking
user.UserType.IsUser()            // ? bool
user.UserType.IsProvider()        // ? bool
user.UserType.IsBothRoles()       // ? bool

// Get role strings (for JWT/Authorization)
user.UserType.GetRoles()          // ? string[]
user.UserType.GetPrimaryRole()    // ? string

// Convert string roles to enum
UserTypeExtensions.FromRole("User")                    // ? UserType?
UserTypeExtensions.FromRoles(new[] {"User", "Provider"})  // ? UserType

// Validation
UserTypeExtensions.IsValid(1)    // ? true (valid: 1, 2, 3)
```

## Usage Examples

### Check User Capabilities

```csharp
if (user.UserType.IsProvider())
{
    // Can place bids
}

if (user.UserType.IsUser())
{
    // Can create requests
}

if (user.UserType == UserType.Both)
{
    // Has both roles
}
```

### Database Queries

```csharp
// Get all providers
var providers = context.Users
    .Where(u => u.UserType.IsProvider())
    .ToList();

// Get specific type
var users = context.Users
    .Where(u => u.UserType == UserType.User)
    .ToList();
```

### Convert Between Types

```csharp
// String ? Enum (from registration form)
var type = UserTypeExtensions.FromRole("User");

// Enum ? String (for JWT)
var roles = user.UserType.GetRoles();
var primaryRole = user.UserType.GetPrimaryRole();
```

## Important Notes

? **JWT still uses strings**: Role claims in JWT are always strings ("User", "ServiceProvider")  
? **ASP.NET Identity uses strings**: `[Authorize(Roles = "User")]` still expects strings  
? **Blazor uses strings**: `<AuthorizeView Roles="@RoleNames.User">` still expects strings  
? **Database uses enum**: Stored as INT (1, 2, 3) for efficiency  

## Migration Status

- ? Migration Created: `20260204173932_UpdateUserTypeToEnum`
- ? Applied to Database: ServiceMarketplaceDB
- ? Build Successful: 0 Errors, 0 Warnings
- ? Backward Compatible: 100%

## Files

- `ServiceMarketplace.Domain/Enums/UserType.cs` - Enum definition
- `ServiceMarketplace.Infrastructure/Data/ApplicationUser.cs` - Uses enum
- `ServiceMarketplace.Infrastructure/Data/Extensions/UserTypeExtensions.cs` - Helper methods

---

**Status**: ? Production Ready

