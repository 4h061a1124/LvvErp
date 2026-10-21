using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Reports;

/// <param name="Quantity">In the filtered warehouse (or all warehouses).</param>
/// <param name="TotalQuantity">Across all warehouses; what the minimum stock is compared with.</param>
public sealed record StockOnHandDto(
    Guid MaterialId,
    string Code,
    string Name,
    string? Specification,
    string UnitName,
    Guid GroupId,
    string GroupName,
    decimal MinStock,
    decimal Quantity,
    decimal TotalQuantity,
    bool IsBelowMin,
    bool TrackLots,
    string? PhotoUrl);

public sealed class StockOnHandQuery
{
    public const int MaxPageSize = 100;

    public Guid? WarehouseId { get; init; }
    public Guid? GroupId { get; init; }
    /// <summary>Code, name or specification (accent-insensitive).</summary>
    public string? Search { get; init; }
    /// <summary>Only active materials whose total is under their minimum (minimum &gt; 0).</summary>
    public bool BelowMinOnly { get; init; }
    /// <summary>Also list active materials with no stock.</summary>
    public bool IncludeZero { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    /// <summary>code | name | quantity; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

/// <summary>Where the stock of one material sits.</summary>
public sealed record StockOnHandDetailDto(
    Guid WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string? LocationCode,
    Guid? LotId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    decimal Quantity);

public sealed record BelowMinStockDto(
    Guid MaterialId, string Code, string Name, string UnitName, decimal MinStock, decimal Quantity, decimal Shortage);

public sealed class StockCardQuery
{
    public Guid MaterialId { get; init; }
    public Guid? WarehouseId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>Thẻ kho: opening balance, every movement with a running balance, closing balance.</summary>
public sealed record StockCardDto(
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitName,
    Guid? WarehouseId,
    string? WarehouseName,
    DateOnly? From,
    DateOnly? To,
    decimal Opening,
    decimal TotalIn,
    decimal TotalOut,
    decimal Closing,
    bool Truncated,
    IReadOnlyList<StockCardEntryDto> Entries);

/// <param name="OtherWarehouseName">Transfers: the warehouse on the other side.</param>
/// <param name="Recipient">Issues: receiving employee, or department.</param>
public sealed record StockCardEntryDto(
    Guid TransactionId,
    DateOnly Date,
    Guid DocumentId,
    string DocumentNumber,
    StockDocumentType DocumentType,
    StockDocumentReason? Reason,
    bool IsReversal,
    string WarehouseName,
    string? LocationCode,
    string? LotNumber,
    string? Partner,
    string? Recipient,
    string? OtherWarehouseName,
    decimal In,
    decimal Out,
    decimal Balance);
