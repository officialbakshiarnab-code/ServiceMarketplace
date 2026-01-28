namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Base entity with full audit trail support.
/// CreatedAt and UpdatedAt are automatically populated by EF Core interceptor.
/// </summary>
public abstract class BaseAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// UTC timestamp when entity was first created.
    /// Automatically set by AuditInterceptor on insert.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// UTC timestamp when entity was last modified.
    /// Automatically set by AuditInterceptor on update.
    /// Null if entity has never been updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
