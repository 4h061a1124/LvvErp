using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing.Orders;

public sealed record PurchaseOrderSummaryDto(
    Guid Id,
    string Number,
    PurchaseOrderStatus Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid SupplierId,
    string SupplierName,
    string WarehouseName,
    VatRate VatRate,
    decimal Total,
    int LineCount,
    PurchaseOrderApprovalLevel? ApprovalLevel,
    bool IsOverdue, // expected date has passed and goods are still due
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

public sealed record PurchaseOrderActions(
    bool CanEdit, bool CanDelete, bool CanSubmit, bool CanApprove, bool CanCancel, bool CanClose);

public sealed record OrderLineSourceDto(Guid RequestLineId, Guid RequestId, string RequestNumber, int RequestLineNo, decimal Quantity);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Specification,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    string? Note,
    string? OverOrderReason,
    decimal ReceivedQuantity, // on approved receipts
    decimal ReceiptTolerancePercent,
    bool TrackLots,
    IReadOnlyList<OrderLineSourceDto> Sources);

public sealed record OrderReceiptDto(Guid Id, string Number, StockDocumentStatus Status, DateOnly DocumentDate);

public sealed record OrderSupplierDto(
    Guid Id, string Code, string Name, string? TaxCode, string? Address, string? ContactName, string? Phone, string? Email);

public sealed record PurchaseOrderDto(
    Guid Id,
    string Number,
    PurchaseOrderStatus Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    OrderSupplierDto Supplier,
    Guid WarehouseId,
    string WarehouseName,
    int PaymentTermDays,
    VatRate VatRate,
    string? Note,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    PurchaseOrderApprovalLevel? ApprovalLevel,
    bool IsOverdue,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? SubmittedAt,
    string? SubmittedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? RejectedAt,
    string? RejectedBy,
    string? RejectReason,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? CloseReason,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    IReadOnlyList<OrderReceiptDto> Receipts,
    PurchaseOrderActions Actions);

public sealed class PurchaseOrderListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Order number, supplier code or name.</summary>
    public string? Search { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
    public Guid? SupplierId { get; init; }
    public bool OverdueOnly { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    /// <summary>date (default, newest first) | number | total | expected; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

/// <param name="SourceLineIds">Approved purchase request lines of the same material, covered in this order.</param>
/// <param name="OverOrderReason">Required when Quantity exceeds what the sources still need.</param>
public sealed record PurchaseOrderLineRequest(
    Guid MaterialId,
    decimal Quantity,
    decimal UnitPrice,
    IReadOnlyList<Guid> SourceLineIds,
    string? Note = null,
    string? OverOrderReason = null);

public sealed record SavePurchaseOrderRequest(
    Guid SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid WarehouseId,
    int PaymentTermDays,
    VatRate VatRate,
    string? Note,
    IReadOnlyList<PurchaseOrderLineRequest> Lines);

/// <summary>Latest approved price of a material, preferring the given supplier.</summary>
public sealed record LastPriceDto(
    Guid MaterialId,
    decimal UnitPrice,
    Guid SupplierId,
    string SupplierName,
    bool SameSupplier,
    string OrderNumber,
    DateOnly OrderDate);

public sealed record PriceHistoryDto(
    Guid OrderId,
    string OrderNumber,
    DateOnly OrderDate,
    PurchaseOrderStatus Status,
    Guid SupplierId,
    string SupplierName,
    decimal Quantity,
    decimal UnitPrice,
    VatRate VatRate);

public sealed class PriceHistoryQuery
{
    public const int MaxPageSize = 100;

    public Guid MaterialId { get; init; }
    public Guid? SupplierId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
