using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Domain.Features.Inventory;

public enum StockDocumentType
{
    /// <summary>Phiếu nhập kho.</summary>
    Receipt = 1,
    /// <summary>Phiếu xuất kho.</summary>
    Issue = 2,
    /// <summary>Phiếu chuyển kho (also moves between locations of one warehouse).</summary>
    Transfer = 3,
}

/// <summary>Draft → Approved → (Admin only) Cancelled. Only Approved touches stock.</summary>
public enum StockDocumentStatus
{
    Draft = 1,
    Approved = 2,
    Cancelled = 3,
}

public enum StockDocumentReason
{
    // Receipts
    Purchase = 1,
    ProductionReturn = 2,
    OpeningBalance = 3,
    // Issues
    Production = 4,
    Consumption = 5,
    // Both
    Other = 9,
}

/// <summary>Chứng từ kho: phiếu nhập, xuất hoặc chuyển. Approved/cancelled documents are immutable.</summary>
public class StockDocument : AuditableEntity
{
    public const int NumberMaxLength = 20;
    public const int PartnerMaxLength = 200;
    public const int NoteMaxLength = 500;
    public const int MaxLines = 500;

    /// <summary>e.g. "PN-2026-00001", assigned when the draft is created; never reused.</summary>
    public string Number { get; set; } = string.Empty;
    public StockDocumentType Type { get; set; }
    public StockDocumentStatus Status { get; set; } = StockDocumentStatus.Draft;
    public DateOnly DocumentDate { get; set; }

    /// <summary>Receipt: destination. Issue/transfer: source.</summary>
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Transfer only: destination.</summary>
    public Guid? ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }

    /// <summary>Null for transfers.</summary>
    public StockDocumentReason? Reason { get; set; }

    /// <summary>Receipt: supplier / source as free text (no supplier catalog yet).</summary>
    public string? Partner { get; set; }

    /// <summary>Issue: receiving department and person.</summary>
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public List<StockDocumentLine> Lines { get; set; } = [];

    public static string NumberPrefix(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "PN",
        StockDocumentType.Issue => "PX",
        StockDocumentType.Transfer => "PC",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string FormatNumber(StockDocumentType type, int year, int sequence) =>
        $"{NumberPrefix(type)}-{year}-{sequence:D5}";

    public static bool IsReasonAllowed(StockDocumentType type, StockDocumentReason? reason) => type switch
    {
        StockDocumentType.Receipt => reason is StockDocumentReason.Purchase or StockDocumentReason.ProductionReturn
            or StockDocumentReason.OpeningBalance or StockDocumentReason.Other,
        StockDocumentType.Issue => reason is StockDocumentReason.Production or StockDocumentReason.Consumption
            or StockDocumentReason.Other,
        StockDocumentType.Transfer => reason is null,
        _ => false,
    };
}

public class StockDocumentLine : Entity
{
    public const int NoteMaxLength = 200;

    public Guid DocumentId { get; set; }
    public StockDocument Document { get; set; } = null!;

    /// <summary>1-based position, used in error messages ("dòng 3").</summary>
    public int LineNo { get; set; }

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    /// <summary>Receipt: destination location. Issue/transfer: source location.</summary>
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    /// <summary>Transfer only: destination location.</summary>
    public Guid? ToLocationId { get; set; }
    public Location? ToLocation { get; set; }

    /// <summary>
    /// Issue/transfer: the lot taken. Receipt: filled on approval, when the lot named by
    /// <see cref="LotNumber"/> is found or created.
    /// </summary>
    public Guid? LotId { get; set; }
    public Lot? Lot { get; set; }

    /// <summary>Receipt drafts only: the lot as typed, with its dates.</summary>
    public string? LotNumber { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public decimal Quantity { get; set; }
    public string? Note { get; set; }
}
