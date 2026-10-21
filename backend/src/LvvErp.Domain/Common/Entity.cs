namespace LvvErp.Domain.Common;

/// <summary>Base for all entities. Ids are UUIDv7 (time-ordered, index friendly).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

/// <summary>Entity with audit columns, filled automatically on SaveChanges.</summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Marks an entity as soft-deletable: Remove() is turned into IsDeleted = true
/// and a global query filter hides deleted rows.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
