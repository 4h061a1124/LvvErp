using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Inventory;

/// <summary>
/// Simple coded catalog entry. Entries that have been used can't be deleted (FK restrict),
/// they are deactivated instead and then hidden from pickers.
/// </summary>
public abstract class CatalogEntity : AuditableEntity
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;

    /// <summary>Stored upper-case (see <see cref="NormalizeCode"/>).</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

/// <summary>Đơn vị tính.</summary>
public class Unit : CatalogEntity;

/// <summary>Nhóm vật tư.</summary>
public class MaterialGroup : CatalogEntity;

/// <summary>Kho.</summary>
public class Warehouse : CatalogEntity
{
    public const int NoteMaxLength = 500;

    public string? Note { get; set; }
    public List<Location> Locations { get; set; } = [];
}

/// <summary>Vị trí trong kho (kệ/ô). Optional: a warehouse may have none.</summary>
public class Location : AuditableEntity
{
    public const int CodeMaxLength = 30;
    public const int DescriptionMaxLength = 200;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Unique within its warehouse, stored upper-case, e.g. "A-01-03".</summary>
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
