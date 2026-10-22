using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Domain.Features.Purchasing;

/// <summary>
/// Draft → Submitted → Approved / Rejected. Once approved, the status follows how much has been put
/// on purchase orders (PartiallyOrdered, Ordered); Closed stops further ordering. A request that is
/// not on any order can be cancelled.
/// </summary>
public enum PurchaseRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    PartiallyOrdered = 5,
    Ordered = 6,
    Closed = 7,
    Cancelled = 8,
}

/// <summary>Đề nghị mua hàng, raised by a department for its own needs.</summary>
public class PurchaseRequest : AuditableEntity
{
    public const int NumberMaxLength = 20;
    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 500;
    public const int MaxLines = 200;
    public const string NumberPrefix = "DN";

    /// <summary>e.g. "DN-2026-00001", assigned when the draft is created; never reused.</summary>
    public string Number { get; set; } = string.Empty;
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public DateOnly RequestDate { get; set; }
    /// <summary>Ngày cần hàng.</summary>
    public DateOnly? NeededDate { get; set; }

    /// <summary>The requester's department at the time of the request; its head approves.</summary>
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;

    /// <summary>Mục đích sử dụng.</summary>
    public string? Note { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
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

    public List<PurchaseRequestLine> Lines { get; set; } = [];

    public static string FormatNumber(int year, int sequence) => $"{NumberPrefix}-{year}-{sequence:D5}";

    /// <summary>Statuses whose remaining quantities can be put on a purchase order.</summary>
    public static bool IsOrderable(PurchaseRequestStatus status) =>
        status is PurchaseRequestStatus.Approved or PurchaseRequestStatus.PartiallyOrdered;
}

public class PurchaseRequestLine : Entity
{
    public const int NoteMaxLength = 200;

    public Guid RequestId { get; set; }
    public PurchaseRequest Request { get; set; } = null!;

    public int LineNo { get; set; }

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal Quantity { get; set; }
    public string? Note { get; set; }
}
