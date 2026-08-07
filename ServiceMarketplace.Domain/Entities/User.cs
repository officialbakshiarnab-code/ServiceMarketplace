using System.ComponentModel.DataAnnotations;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = null!;

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? NormalizedEmail { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(20)]
    public string? NormalizedPhoneNumber { get; set; }

    [MaxLength(20)]
    public string? SecondaryPhoneNumber { get; set; }

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = null!;

    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public DateTime DateOfBirth { get; set; }
    public UserType UserType { get; set; } = UserType.Customer;
    public string? GovIdFilePath { get; set; }
    public bool IsKycSubmitted { get; set; }
    public bool IsKycApproved { get; set; }
    public string? KycApprovedByUserId { get; set; }
    public DateTime? KycApprovedOn { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ServiceProviderProfile? ServiceProviderProfile { get; set; }
    public SellerProfile? SellerProfile { get; set; }
}
