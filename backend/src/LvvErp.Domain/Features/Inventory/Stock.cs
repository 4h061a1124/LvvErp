using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Inventory;

/// <summary>Where a quantity of stock sits. Location and lot are optional.</summary>
public readonly record struct StockKey(Guid MaterialId, Guid WarehouseId, Guid? LocationId, Guid? LotId) : IComparable<StockKey>
{
    /// <summary>Total order used to touch balance rows in a fixed sequence (avoids deadlocks).</summary>
    public int CompareTo(StockKey other)
    {
        var c = MaterialId.CompareTo(other.MaterialId);
        if (c == 0) c = WarehouseId.CompareTo(other.WarehouseId);
        if (c == 0) c = (LocationId ?? Guid.Empty).CompareTo(other.LocationId ?? Guid.Empty);
        if (c == 0) c = (LotId ?? Guid.Empty).CompareTo(other.LotId ?? Guid.Empty);
        return c;
    }
}

/// <summary>
/// Sổ kho: one row per stock movement, append-only. Approving a document adds its rows;
/// cancelling adds reversing rows (<see cref="IsReversal"/>) and never deletes anything.
/// </summary>
public class StockTransaction : Entity
{
    public Guid DocumentId { get; set; }
    public StockDocument Document { get; set; } = null!;
    public Guid DocumentLineId { get; set; }
    public StockDocumentLine DocumentLine { get; set; } = null!;

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public Guid? LotId { get; set; }
    public Lot? Lot { get; set; }

    /// <summary>Signed: positive = in, negative = out.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Document date for normal rows, cancellation date for reversals.</summary>
    public DateOnly TransactionDate { get; set; }
    public bool IsReversal { get; set; }

    /// <summary>Same value for every row of one posting (approve or cancel); orders postings within a day.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public StockKey Key => new(MaterialId, WarehouseId, LocationId, LotId);
}

/// <summary>
/// Tồn hiện tại per <see cref="StockKey"/>, kept in step with <see cref="StockTransaction"/> in the
/// same database transaction. Written only through atomic SQL in the stock ledger.
/// </summary>
public class StockBalance : Entity
{
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public Guid? LotId { get; set; }
    public Lot? Lot { get; set; }

    /// <summary>Never negative (database check constraint).</summary>
    public decimal Quantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Last number used per document prefix and year.</summary>
public class DocumentSequence
{
    public string Prefix { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastNumber { get; set; }
}
