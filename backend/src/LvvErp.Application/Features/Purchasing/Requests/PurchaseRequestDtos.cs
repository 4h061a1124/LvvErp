using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing.Requests;

public sealed record PurchaseRequestSummaryDto(
    Guid Id,
    string Number,
    PurchaseRequestStatus Status,
    DateOnly RequestDate,
    DateOnly? NeededDate,
    Guid DepartmentId,
    string DepartmentName,
    string RequesterName,
    string? Note,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

/// <summary>What the current user may do with the request; the server enforces the same rules.</summary>
public sealed record PurchaseRequestActions(
    bool CanEdit, bool CanDelete, bool CanSubmit, bool CanApprove, bool CanCancel, bool CanClose);

public sealed record PurchaseRequestLineDto(
    Guid Id,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitName,
    decimal Quantity,
    decimal OrderedQuantity, // on orders that are not cancelled
    string? Note);

public sealed record RelatedOrderDto(Guid Id, string Number, PurchaseOrderStatus Status);

public sealed record PurchaseRequestDto(
    Guid Id,
    string Number,
    PurchaseRequestStatus Status,
    DateOnly RequestDate,
    DateOnly? NeededDate,
    Guid DepartmentId,
    string DepartmentName,
    Guid RequesterId,
    string RequesterName,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
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
    IReadOnlyList<PurchaseRequestLineDto> Lines,
    IReadOnlyList<RelatedOrderDto> Orders,
    PurchaseRequestActions Actions);

public sealed class PurchaseRequestListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Number, note or requester name.</summary>
    public string? Search { get; init; }
    public PurchaseRequestStatus? Status { get; init; }
    public Guid? DepartmentId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    /// <summary>Only requests raised by the current user.</summary>
    public bool Mine { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    /// <summary>date (default, newest first) | number; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

public sealed record PurchaseRequestLineRequest(Guid MaterialId, decimal Quantity, string? Note = null);

public sealed record SavePurchaseRequestRequest(
    DateOnly RequestDate,
    DateOnly? NeededDate,
    string? Note,
    IReadOnlyList<PurchaseRequestLineRequest> Lines);

/// <summary>Reject, cancel and close all require a reason.</summary>
public sealed record ReasonRequest(string Reason);

/// <summary>An approved request line that still needs ordering (for building a purchase order).</summary>
public sealed record OrderableRequestLineDto(
    Guid RequestLineId,
    Guid RequestId,
    string RequestNumber,
    string DepartmentName,
    string RequesterName,
    DateOnly? NeededDate,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitName,
    decimal Quantity,
    decimal OrderedQuantity,
    decimal RemainingQuantity,
    string? Note);

public sealed class OrderableLinesQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Request number, material code or name.</summary>
    public string? Search { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? DepartmentId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
