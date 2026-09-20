namespace ServiceMarketplace.Domain.Enums;

/// <summary>Internal payment workflow states; these do not assert bank custody or actual money movement.</summary>
public enum PaymentStatus
{
    Pending,
    Held,
    Released,
    Refunded
}
