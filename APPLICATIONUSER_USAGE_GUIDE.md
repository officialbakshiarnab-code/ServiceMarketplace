# ApplicationUser - Usage Guide & Code Examples

## Quick Reference

### Accessing User Properties

```csharp
// From dependency injection
var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

// Get user by email
var user = await userManager.FindByEmailAsync("user@example.com");

// Access new properties
string firstName = user.FirstName;
string lastName = user.LastName;
string fullName = user.FullName; // Derived property
string phonePrimary = user.PhonePrimary;
string? phoneSecondary = user.PhoneSecondary;
DateTime dateOfBirth = user.DateOfBirth;
string? idImagePath = user.GovernmentIdImagePath;
int userType = user.UserType;
DateTime createdAt = user.CreatedAtUtc;
```

---

## Common Tasks

### 1. Creating a New User

```csharp
public async Task<IdentityResult> RegisterUserAsync(
    string email, 
    string password,
    string firstName,
    string lastName,
    string phonePrimary,
    DateTime dateOfBirth,
    int userType = 1)
{
    var user = new ApplicationUser
    {
        Email = email,
        UserName = email,
        FirstName = firstName,
        LastName = lastName,
        PhonePrimary = phonePrimary,
        DateOfBirth = dateOfBirth,
        UserType = userType,
        CreatedAtUtc = DateTime.UtcNow
    };

    return await _userManager.CreateAsync(user, password);
}
```

### 2. Updating User Profile

```csharp
public async Task<IdentityResult> UpdateUserProfileAsync(
    string userId,
    string firstName,
    string lastName,
    string phonePrimary,
    string? phoneSecondary = null,
    string? governmentIdPath = null)
{
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found" });

    user.FirstName = firstName;
    user.LastName = lastName;
    user.PhonePrimary = phonePrimary;
    user.PhoneSecondary = phoneSecondary;
    user.GovernmentIdImagePath = governmentIdPath;

    return await _userManager.UpdateAsync(user);
}
```

### 3. Getting User's Full Name

```csharp
public string GetUserDisplayName(ApplicationUser user)
{
    return user.FullName; // Uses the derived property
    // Alternative: $"{user.FirstName} {user.LastName}"
}
```

### 4. Verifying User Age

```csharp
public bool IsAdult(ApplicationUser user)
{
    var age = DateTime.Today.Year - user.DateOfBirth.Year;
    if (user.DateOfBirth > DateTime.Today.AddYears(-age))
        age--;
    return age >= 18;
}

// Or using more precise calculation
public double GetExactAge(ApplicationUser user)
{
    return (DateTime.Now - user.DateOfBirth).TotalDays / 365.25;
}
```

### 5. Filtering Users by Type

```csharp
public class UserTypeConstants
{
    public const int User = 1;
    public const int ServiceProvider = 2;
    public const int Both = 3;
}

// Get all service providers
var providers = await _context.Users
    .Where(u => u.UserType == UserTypeConstants.ServiceProvider)
    .ToListAsync();

// Get all regular users
var regularUsers = await _context.Users
    .Where(u => u.UserType == UserTypeConstants.User)
    .ToListAsync();

// Get multi-role users
var multiRoleUsers = await _context.Users
    .Where(u => u.UserType == UserTypeConstants.Both)
    .ToListAsync();
```

### 6. Searching Users

```csharp
// Search by full name
public async Task<List<ApplicationUser>> SearchByFullNameAsync(string searchTerm)
{
    var term = searchTerm.ToLower();
    return await _context.Users
        .Where(u => 
            u.FirstName.Contains(searchTerm) || 
            u.LastName.Contains(searchTerm))
        .ToListAsync();
}

// Search by phone number
public async Task<ApplicationUser?> FindByPhoneAsync(string phone)
{
    return await _context.Users
        .FirstOrDefaultAsync(u => 
            u.PhonePrimary == phone || 
            u.PhoneSecondary == phone);
}

// Find adults who registered recently
public async Task<List<ApplicationUser>> FindRecentAdultsAsync(int days = 30)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-days);
    return await _context.Users
        .Where(u => 
            u.CreatedAtUtc >= cutoffDate &&
            u.DateOfBirth.AddYears(18) <= DateTime.Today)
        .ToListAsync();
}
```

### 7. Creating DTOs from ApplicationUser

```csharp
// User Profile DTO
public class UserProfileDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PhonePrimary { get; set; } = null!;
    public string? PhoneSecondary { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? GovernmentIdImagePath { get; set; }
    public int UserType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int Age { get; set; }

    public static UserProfileDto FromApplicationUser(ApplicationUser user)
    {
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth > DateTime.Today.AddYears(-age))
            age--;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            PhonePrimary = user.PhonePrimary,
            PhoneSecondary = user.PhoneSecondary,
            DateOfBirth = user.DateOfBirth,
            GovernmentIdImagePath = user.GovernmentIdImagePath,
            UserType = user.UserType,
            CreatedAtUtc = user.CreatedAtUtc,
            Age = age
        };
    }
}

// User Registration Request DTO
public class RegisterUserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required]
    [Phone]
    public string PhonePrimary { get; set; } = null!;

    [Phone]
    public string? PhoneSecondary { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    public string? GovernmentIdImagePath { get; set; }

    [Required]
    public int UserType { get; set; } = 1;
}

// Usage
public async Task<IdentityResult> RegisterAsync(RegisterUserDto dto)
{
    var user = new ApplicationUser
    {
        Email = dto.Email,
        UserName = dto.Email,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        PhonePrimary = dto.PhonePrimary,
        PhoneSecondary = dto.PhoneSecondary,
        DateOfBirth = dto.DateOfBirth,
        GovernmentIdImagePath = dto.GovernmentIdImagePath,
        UserType = dto.UserType,
        CreatedAtUtc = DateTime.UtcNow
    };

    return await _userManager.CreateAsync(user, dto.Password);
}
```

### 8. Validation Examples

```csharp
public class ApplicationUserValidator
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationUserValidator(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    // Validate phone format
    public bool IsValidPhoneNumber(string phone)
    {
        // Allow formats: +1234567890, 1234567890, (123)456-7890
        var phonePattern = @"^(\+\d{1,3}[- ]?)?\d{10}$";
        return Regex.IsMatch(phone.Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", ""), phonePattern);
    }

    // Validate date of birth
    public bool IsValidDateOfBirth(DateTime dateOfBirth)
    {
        if (dateOfBirth >= DateTime.Today)
            return false; // Future date not allowed

        var age = DateTime.Today.Year - dateOfBirth.Year;
        if (dateOfBirth > DateTime.Today.AddYears(-age))
            age--;

        return age >= 13 && age <= 150; // Reasonable age range
    }

    // Validate user type
    public bool IsValidUserType(int userType)
    {
        return userType >= 1 && userType <= 3;
    }

    // Validate all user properties
    public List<string> ValidateUser(ApplicationUser user)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(user.FirstName))
            errors.Add("FirstName is required");

        if (string.IsNullOrWhiteSpace(user.LastName))
            errors.Add("LastName is required");

        if (!IsValidPhoneNumber(user.PhonePrimary))
            errors.Add("PhonePrimary has invalid format");

        if (!string.IsNullOrEmpty(user.PhoneSecondary) && !IsValidPhoneNumber(user.PhoneSecondary))
            errors.Add("PhoneSecondary has invalid format");

        if (!IsValidDateOfBirth(user.DateOfBirth))
            errors.Add("DateOfBirth is invalid (must be past date, reasonable age)");

        if (!IsValidUserType(user.UserType))
            errors.Add("UserType is invalid (must be 1, 2, or 3)");

        if (!string.IsNullOrEmpty(user.GovernmentIdImagePath) && user.GovernmentIdImagePath.Length > 500)
            errors.Add("GovernmentIdImagePath exceeds maximum length");

        return errors;
    }
}
```

### 9. Service Provider Check

```csharp
public static class ApplicationUserExtensions
{
    public bool IsServiceProvider(this ApplicationUser user)
    {
        return user.UserType == 2 || user.UserType == 3;
    }

    public bool IsRegularUser(this ApplicationUser user)
    {
        return user.UserType == 1 || user.UserType == 3;
    }

    public bool IsBothRoles(this ApplicationUser user)
    {
        return user.UserType == 3;
    }

    public int GetAge(this ApplicationUser user)
    {
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth > DateTime.Today.AddYears(-age))
            age--;
        return age;
    }
}

// Usage
var user = await _userManager.FindByEmailAsync("user@example.com");
if (user.IsServiceProvider())
{
    // Show provider dashboard
}
```

### 10. Reporting Queries

```csharp
// User statistics
public async Task<object> GetUserStatisticsAsync()
{
    var totalUsers = await _context.Users.CountAsync();
    var serviceProviders = await _context.Users
        .CountAsync(u => u.UserType == 2 || u.UserType == 3);
    var regularUsers = await _context.Users
        .CountAsync(u => u.UserType == 1 || u.UserType == 3);
    var multiRoleUsers = await _context.Users
        .CountAsync(u => u.UserType == 3);
    
    var avgAge = await _context.Users
        .AsEnumerable()
        .Where(u => u.DateOfBirth < DateTime.Today)
        .Average(u => (DateTime.Today - u.DateOfBirth).TotalDays / 365.25);

    var recentSignups = await _context.Users
        .Where(u => u.CreatedAtUtc >= DateTime.UtcNow.AddDays(-30))
        .CountAsync();

    return new
    {
        TotalUsers = totalUsers,
        ServiceProviders = serviceProviders,
        RegularUsers = regularUsers,
        MultiRoleUsers = multiRoleUsers,
        AverageAge = Math.Round(avgAge, 1),
        RecentSignups = recentSignups
    };
}
```

---

## Best Practices

### ? DO

```csharp
// ? Use the derived FullName property
var displayName = user.FullName;

// ? Validate user data before creating/updating
var errors = userValidator.ValidateUser(newUser);
if (errors.Any()) return BadRequest(errors);

// ? Use UserType filtering with constants
var providers = await _context.Users
    .Where(u => u.UserType == UserTypeConstants.ServiceProvider)
    .ToListAsync();

// ? Store phone numbers in consistent format
user.PhonePrimary = NormalizePhoneNumber(phoneInput);

// ? Use DateTime.UtcNow for CreatedAtUtc
user.CreatedAtUtc = DateTime.UtcNow;

// ? Validate age requirements before allowing sensitive operations
if (!user.IsAdult()) 
    return Forbid("User must be 18 or older");
```

### ? DON'T

```csharp
// ? Don't use UserType without validation
user.UserType = userInput; // What if it's 999?

// ? Don't store inconsistently formatted phone numbers
user.PhonePrimary = "+1 (202) 555-1234"; // Use normalized format

// ? Don't assume DateOfBirth is always reasonable
if (user.DateOfBirth == default) // This will be 1/1/0001

// ? Don't concatenate FirstName + LastName
var name = user.FirstName + " " + user.LastName; // Use FullName instead

// ? Don't forget to handle null PhoneSecondary
string phone = user.PhoneSecondary; // This is nullable!
if (string.IsNullOrEmpty(user.PhoneSecondary)) { ... }
```

---

## Migration Notes

### For Existing Users
If you have existing users without these new required fields, you'll need to:

1. **Add migration to backfill data**:
   ```csharp
   migrationBuilder.Sql(@"
       UPDATE Users 
       SET FirstName = SUBSTRING(UserName, 1, CHARINDEX('@', UserName) - 1),
           LastName = 'User',
           PhonePrimary = ISNULL(PhoneNumber, '0000000000'),
           DateOfBirth = CAST('1990-01-01' AS datetime2),
           UserType = 1,
           CreatedAtUtc = GETUTCDATE()
       WHERE FirstName IS NULL
   ");
   ```

2. **Or populate manually in code**:
   ```csharp
   var usersToUpdate = await _context.Users
       .Where(u => string.IsNullOrEmpty(u.FirstName))
       .ToListAsync();

   foreach (var user in usersToUpdate)
   {
       user.FirstName = user.UserName?.Split('@')[0] ?? "User";
       user.LastName = "User";
       user.PhonePrimary = user.PhoneNumber ?? "0000000000";
       user.DateOfBirth = new DateTime(1990, 1, 1);
       user.UserType = 1;
   }

   await _context.SaveChangesAsync();
   ```

---

## Summary

The `ApplicationUser` class extends `IdentityUser` with 8 new properties:
- **FirstName** & **LastName** - User identification
- **PhonePrimary** & **PhoneSecondary** - Contact information
- **DateOfBirth** - Age verification
- **GovernmentIdImagePath** - ID verification storage
- **UserType** - User classification (INT: 1=User, 2=Provider, 3=Both)
- **CreatedAtUtc** - Account creation tracking
- **FullName** - Derived property for convenience

All changes are backward compatible and ready for production use.

