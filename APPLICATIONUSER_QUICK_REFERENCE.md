# ApplicationUser Quick Reference Card

## Class Definition

```csharp
public class ApplicationUser : IdentityUser
{
    // Required properties
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string PhonePrimary { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
    public int UserType { get; set; }
    
    // Optional properties
    public string? PhoneSecondary { get; set; }
    public string? GovernmentIdImagePath { get; set; }
    
    // Auto-set properties
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // Derived property
    public string FullName => $"{FirstName} {LastName}".Trim();
}
```

---

## Namespace
```csharp
using ServiceMarketplace.Infrastructure.Data;
```

---

## UserType Values
| Value | Meaning | Usage |
|-------|---------|-------|
| 1 | Regular User | Service requester |
| 2 | Service Provider | Offers services |
| 3 | Both | User + Provider |

---

## Property Constraints

| Property | Type | Max Length | Required | Default |
|----------|------|-----------|----------|---------|
| FirstName | string | 100 | Yes | '' |
| LastName | string | 100 | Yes | '' |
| PhonePrimary | string | 20 | Yes | '' |
| PhoneSecondary | string? | 20 | No | NULL |
| DateOfBirth | DateTime | - | Yes | 1/1/0001 |
| GovernmentIdImagePath | string? | 500 | No | NULL |
| UserType | int | - | Yes | 0 |
| CreatedAtUtc | DateTime | - | Yes | GETUTCDATE() |

---

## Common Operations

### Create User
```csharp
var user = new ApplicationUser
{
    Email = "user@example.com",
    UserName = "user@example.com",
    FirstName = "John",
    LastName = "Doe",
    PhonePrimary = "+12025551234",
    DateOfBirth = new DateTime(1990, 1, 15),
    UserType = 1,
    CreatedAtUtc = DateTime.UtcNow
};
await userManager.CreateAsync(user, password);
```

### Get User
```csharp
var user = await userManager.FindByEmailAsync("user@example.com");
var user = await userManager.FindByIdAsync(userId);
```

### Update User
```csharp
user.FirstName = "Jane";
user.LastName = "Smith";
user.PhonePrimary = "+1234567890";
await userManager.UpdateAsync(user);
```

### Display Full Name
```csharp
string name = user.FullName; // "John Doe"
```

### Get User Age
```csharp
int age = DateTime.Today.Year - user.DateOfBirth.Year;
if (user.DateOfBirth > DateTime.Today.AddYears(-age)) age--;
```

### Check User Type
```csharp
bool isProvider = user.UserType == 2 || user.UserType == 3;
bool isUser = user.UserType == 1 || user.UserType == 3;
bool isBoth = user.UserType == 3;
```

### Filter by UserType
```csharp
var providers = await context.Users
    .Where(u => u.UserType == 2 || u.UserType == 3)
    .ToListAsync();
```

### Search by Name
```csharp
var results = await context.Users
    .Where(u => u.FirstName.Contains(searchTerm) 
             || u.LastName.Contains(searchTerm))
    .ToListAsync();
```

---

## Database Migration

**Migration**: AddApplicationUserProperties  
**Timestamp**: 20260204173220  
**Status**: Applied

### Apply Migration
```bash
dotnet ef database update -p ServiceMarketplace.Infrastructure -s ServiceMarketplace.API
```

### Rollback Migration
```bash
dotnet ef database update <previous-migration> -p ServiceMarketplace.Infrastructure -s ServiceMarketplace.API
```

---

## Inherited from IdentityUser

```csharp
// Email properties
public string Email { get; set; }
public string NormalizedEmail { get; set; }
public bool EmailConfirmed { get; set; }

// Phone properties
public string PhoneNumber { get; set; }
public bool PhoneNumberConfirmed { get; set; }

// Password/Security
public string PasswordHash { get; set; }
public string SecurityStamp { get; set; }

// Auth properties
public string ConcurrencyStamp { get; set; }
public bool TwoFactorEnabled { get; set; }

// Lockout properties
public DateTimeOffset? LockoutEnd { get; set; }
public bool LockoutEnabled { get; set; }
public int AccessFailedCount { get; set; }

// Identity properties
public string Id { get; set; }
public string UserName { get; set; }
public string NormalizedUserName { get; set; }
```

---

## Validation Examples

### Validate Phone Number
```csharp
private bool IsValidPhone(string phone)
{
    return Regex.IsMatch(
        phone.Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", ""),
        @"^(\+\d{1,3}[- ]?)?\d{10}$"
    );
}
```

### Validate Date of Birth
```csharp
private bool IsValidDOB(DateTime dob)
{
    if (dob >= DateTime.Today) return false;
    
    var age = DateTime.Today.Year - dob.Year;
    if (dob > DateTime.Today.AddYears(-age)) age--;
    
    return age >= 13 && age <= 150;
}
```

### Validate User Type
```csharp
private bool IsValidUserType(int type)
{
    return type >= 1 && type <= 3;
}
```

---

## Extension Methods

```csharp
public static class ApplicationUserExtensions
{
    public static bool IsServiceProvider(this ApplicationUser user)
        => user.UserType == 2 || user.UserType == 3;
    
    public static bool IsRegularUser(this ApplicationUser user)
        => user.UserType == 1 || user.UserType == 3;
    
    public static bool IsBothRoles(this ApplicationUser user)
        => user.UserType == 3;
    
    public static int GetAge(this ApplicationUser user)
    {
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth > DateTime.Today.AddYears(-age)) age--;
        return age;
    }
}

// Usage
if (user.IsServiceProvider()) { ... }
int age = user.GetAge();
```

---

## DTO Example

```csharp
public class UserProfileDto
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public string PhonePrimary { get; set; }
    public string PhoneSecondary { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string GovernmentIdImagePath { get; set; }
    public int UserType { get; set; }
    public int Age { get; set; }
    
    public static UserProfileDto FromApplicationUser(ApplicationUser user)
    {
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth > DateTime.Today.AddYears(-age)) age--;
        
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            PhonePrimary = user.PhonePrimary,
            PhoneSecondary = user.PhoneSecondary,
            DateOfBirth = user.DateOfBirth,
            GovernmentIdImagePath = user.GovernmentIdImagePath,
            UserType = user.UserType,
            Age = age
        };
    }
}
```

---

## SQL Verification

### Check Columns
```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
AND COLUMN_NAME IN ('FirstName', 'LastName', 'PhonePrimary', 
                     'PhoneSecondary', 'DateOfBirth', 
                     'GovernmentIdImagePath', 'UserType', 'CreatedAtUtc')
ORDER BY ORDINAL_POSITION;
```

### Check Index
```sql
SELECT NAME, TYPE_DESC
FROM sys.indexes
WHERE OBJECT_ID = OBJECT_ID('Users')
AND NAME = 'IX_Users_UserType';
```

### Count by UserType
```sql
SELECT UserType, COUNT(*) as Count
FROM Users
GROUP BY UserType;
```

---

## Common Errors & Fixes

### ? "FirstName is required"
```csharp
// ? Wrong
var user = new ApplicationUser { Email = "user@example.com" };

// ? Correct
var user = new ApplicationUser 
{ 
    Email = "user@example.com",
    FirstName = "John",
    LastName = "Doe",
    PhonePrimary = "+1234567890",
    DateOfBirth = new DateTime(1990, 1, 15)
};
```

### ? "PhoneSecondary exceeds max length"
```csharp
// ? Wrong
user.PhoneSecondary = "+1202555123456789012345"; // > 20 chars

// ? Correct
user.PhoneSecondary = "+12025551234"; // <= 20 chars
```

### ? "DateOfBirth must be past date"
```csharp
// ? Wrong
user.DateOfBirth = DateTime.Now; // Future date

// ? Correct
user.DateOfBirth = new DateTime(1990, 1, 15); // Past date
```

### ? "Invalid UserType"
```csharp
// ? Wrong
user.UserType = 99; // Not 1, 2, or 3

// ? Correct
user.UserType = 1; // Valid: 1 = User, 2 = Provider, 3 = Both
```

---

## Files Reference

| File | Purpose |
|------|---------|
| ApplicationUser.cs | Class definition |
| AppDbContext.cs | Entity configuration |
| AddApplicationUserProperties.cs | Migration |
| USER_TABLE_MODIFICATIONS_COMPLETE.md | Full docs |
| APPLICATIONUSER_USAGE_GUIDE.md | Code examples |
| USERS_TABLE_MODIFICATION_SUMMARY.md | Summary |

---

## Status

? **Implementation**: Complete  
? **Migration**: Applied  
? **Build**: Successful  
? **Documentation**: Complete  
? **Production Ready**: YES  

