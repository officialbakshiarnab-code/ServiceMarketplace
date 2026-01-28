namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Base entity class for entities that only need creation tracking.
/// For entities requiring update tracking, use BaseAuditableEntity instead.
/// </summary>
[Obsolete("Use BaseAuditableEntity for new entities to enable full audit trail")]
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// UTC timestamp when the entity was created.
    /// Note: This is set manually. Consider using BaseAuditableEntity with AuditInterceptor for automatic management.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

