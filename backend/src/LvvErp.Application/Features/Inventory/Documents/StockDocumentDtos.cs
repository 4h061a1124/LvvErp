using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Documents;

public sealed record StockDocumentDto(
    Guid Id,
    string Number,
    StockDocumentType Type,
    StockDocumentStatus Status,
    DateOnly DocumentDate,
    Guid WarehouseId,
    string WarehouseName,
    Guid? ToWarehouseId,
    string? ToWarehouseName,
    StockDocumentReason? Reason,
    string? Partner,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? EmployeeId,
    string? EmployeeName,
    string? Note,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    IReadOnlyList<StockDocumentLineDto> Lines);

public sealed record StockDocumentLineDto(
    Guid Id,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitName,
    bool TrackLots,
    Guid? LocationId,
    string? LocationCode,
    Guid? ToLocationId,
    string? ToLocationCode,
    Guid? LotId,
    string? LotNumber,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    decimal Quantity,
    string? Note);

public sealed record StockDocumentSummaryDto(
    Guid Id,
    string Number,
    StockDocumentType Type,
    StockDocumentStatus Status,
    DateOnly DocumentDate,
    string WarehouseName,
    string? ToWarehouseName,
    StockDocumentReason? Reason,
    string? Partner,
    string? DepartmentName,
    string? EmployeeName,
    int LineCount,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);

/// <summary>Query string of document lists.</summary>
public sealed class StockDocumentListQuery
{
    public const int MaxPageSize = 100;

    public StockDocumentStatus? Status { get; init; }
    /// <summary>Matches either side of a transfer.</summary>
    public Guid? WarehouseId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    /// <summary>Document number, partner, recipient or note.</summary>
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    /// <summary>date | number; prefix "-" for descending. Default: newest first.</summary>
    public string? Sort { get; init; }
}

/// <summary>A line that could not be posted because the stock wasn't there.</summary>
public sealed record StockShortage(int LineNo, string MaterialCode, string? LotNumber, decimal Requested, decimal Available);

/// <summary>Stock that can be issued or transferred, best candidates first (earliest expiry).</summary>
public sealed record AvailableStockDto(
    Guid WarehouseId,
    Guid? LocationId,
    string? LocationCode,
    Guid? LotId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    decimal Quantity);

public sealed record LotDto(Guid Id, string LotNumber, DateOnly? ManufactureDate, DateOnly? ExpiryDate, decimal Quantity);
