using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Domain.Features.Purchasing;

/// <summary>
/// Draft → Submitted → Approved / Rejected (editable again, back to Draft). Receiving moves an
/// approved order to PartiallyReceived / Received; Closed ends it early. Cancelled only before
/// anything is received.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    PartiallyReceived = 5,
    Received = 6,
    Closed = 7,
    Cancelled = 8,
}

/// <summary>Thuế suất GTGT. <see cref="Exempt"/> = không chịu thuế (KCT).</summary>
public enum VatRate
{
    Exempt = 1,
    Zero = 2,
    Five = 3,
    Eight = 4,
    Ten = 5,
}

/// <summary>Who must approve a submitted order, fixed at submission from its total and the settings.</summary>
public enum PurchaseOrderApprovalLevel
{
    /// <summary>Head of the purchasing department (a Director may also approve).</summary>
    PurchasingHead = 1,
    Director = 2,
}

/// <summary>Đơn đặt hàng gửi nhà cung cấp, built from approved purchase request lines.</summary>
public class PurchaseOrder : AuditableEntity
{
    public const int NumberMaxLength = 20;
    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 500;
    public const int MaxLines = 200;
    public const string NumberPrefix = "DH";

    public string Number { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateOnly OrderDate { get; set; }
    /// <summary>Ngày giao dự kiến.</summary>
    public DateOnly? ExpectedDate { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    /// <summary>Kho nhận hàng.</summary>
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int PaymentTermDays { get; set; }
    public VatRate VatRate { get; set; } = VatRate.Ten;
    public string? Note { get; set; }

    /// <summary>Sum of line amounts (VND, whole đồng).</summary>
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    public PurchaseOrderApprovalLevel? ApprovalLevel { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? CloseReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];

    public static string FormatNumber(int year, int sequence) => $"{NumberPrefix}-{year}-{sequence:D5}";

    /// <summary>Header and lines can be edited (a rejected order goes back to Draft).</summary>
    public static bool IsEditable(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Rejected;

    /// <summary>Statuses whose lines still count against the purchase requests they came from.</summary>
    public static bool HoldsRequestQuantities(PurchaseOrderStatus status) => status != PurchaseOrderStatus.Cancelled;
}

public class PurchaseOrderLine : Entity
{
    public const int NoteMaxLength = 200;

    public Guid OrderId { get; set; }
    public PurchaseOrder Order { get; set; } = null!;

    public int LineNo { get; set; }

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal Quantity { get; set; }
    /// <summary>Đơn giá before VAT, up to 2 decimals.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Quantity × UnitPrice rounded to whole đồng.</summary>
    public decimal Amount { get; set; }
    public string? Note { get; set; }

    /// <summary>Required when the line orders more than its requests still need; shown to the approver.</summary>
    public string? OverOrderReason { get; set; }

    public List<PurchaseOrderLineSource> Sources { get; set; } = [];
}

/// <summary>How much of a purchase request line an order line covers (the line's origin).</summary>
public class PurchaseOrderLineSource : Entity
{
    public Guid OrderLineId { get; set; }
    public PurchaseOrderLine OrderLine { get; set; } = null!;

    public Guid RequestLineId { get; set; }
    public PurchaseRequestLine RequestLine { get; set; } = null!;

    /// <summary>Part of the request line covered; any excess of the order line is over-ordering.</summary>
    public decimal Quantity { get; set; }
}
