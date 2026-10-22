using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Inventory;

/// <summary>Vật tư.</summary>
public class Material : AuditableEntity
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 200;
    public const int SpecificationMaxLength = 500;
    public const decimal MaxReceiptTolerancePercent = 100m;

    /// <summary>Stored upper-case; also the content of the material's QR label.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Quy cách, e.g. "M8x30, inox 304".</summary>
    public string? Specification { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public Guid GroupId { get; set; }
    public MaterialGroup Group { get; set; } = null!;

    /// <summary>Warning threshold, compared with the total quantity across all warehouses.</summary>
    public decimal MinStock { get; set; }

    /// <summary>When true every stock movement of this material must name a lot.</summary>
    public bool TrackLots { get; set; }

    /// <summary>
    /// How much more than ordered may be received against a purchase order line, in percent of the
    /// ordered quantity (e.g. steel sold by weight). 0 = no more than ordered.
    /// </summary>
    public decimal ReceiptTolerancePercent { get; set; }

    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

/// <summary>Lô hàng of a lot-tracked material. Created when stock is received.</summary>
public class Lot : AuditableEntity
{
    public const int LotNumberMaxLength = 50;

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    /// <summary>Unique within the material, stored as typed (trimmed).</summary>
    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
