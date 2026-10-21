using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.UnitTests.Inventory;

public sealed class StockPostingTests
{
    private static readonly Guid WarehouseA = Guid.NewGuid();
    private static readonly Guid WarehouseB = Guid.NewGuid();
    private static readonly Guid Material = Guid.NewGuid();
    private static readonly Guid Shelf1 = Guid.NewGuid();
    private static readonly Guid Shelf2 = Guid.NewGuid();
    private static readonly Guid Lot = Guid.NewGuid();

    private static StockDocument Document(StockDocumentType type, params StockDocumentLine[] lines) => new()
    {
        Type = type,
        WarehouseId = WarehouseA,
        ToWarehouseId = type == StockDocumentType.Transfer ? WarehouseB : null,
        Lines = [.. lines],
    };

    private static StockDocumentLine Line(int no, decimal qty, Guid? location = null, Guid? toLocation = null, Guid? lot = null) =>
        new() { LineNo = no, MaterialId = Material, Quantity = qty, LocationId = location, ToLocationId = toLocation, LotId = lot };

    [Fact]
    public void Receipt_adds_stock_at_the_destination()
    {
        var movement = Assert.Single(StockPosting.ForApproval(Document(StockDocumentType.Receipt, Line(1, 10, Shelf1, lot: Lot))));

        Assert.Equal(new StockKey(Material, WarehouseA, Shelf1, Lot), movement.Key);
        Assert.Equal(10, movement.Quantity);
    }

    [Fact]
    public void Issue_removes_stock_from_the_source()
    {
        var movement = Assert.Single(StockPosting.ForApproval(Document(StockDocumentType.Issue, Line(1, 4, Shelf1))));

        Assert.Equal(new StockKey(Material, WarehouseA, Shelf1, null), movement.Key);
        Assert.Equal(-4, movement.Quantity);
    }

    [Fact]
    public void Transfer_moves_the_same_lot_from_source_to_destination()
    {
        var movements = StockPosting.ForApproval(Document(StockDocumentType.Transfer, Line(1, 3, Shelf1, Shelf2, Lot)));

        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, m => m.Key == new StockKey(Material, WarehouseA, Shelf1, Lot) && m.Quantity == -3);
        Assert.Contains(movements, m => m.Key == new StockKey(Material, WarehouseB, Shelf2, Lot) && m.Quantity == 3);
        Assert.Equal(0, movements.Sum(m => m.Quantity));
    }

    [Fact]
    public void Cancellation_is_the_exact_opposite_of_approval_and_skips_earlier_reversals()
    {
        var line = Line(2, 5, Shelf1);
        var posted = new[]
        {
            new StockTransaction { DocumentLineId = line.Id, MaterialId = Material, WarehouseId = WarehouseA, LocationId = Shelf1, Quantity = -5 },
            new StockTransaction { DocumentLineId = line.Id, MaterialId = Material, WarehouseId = WarehouseA, Quantity = 1, IsReversal = true },
        };

        var movement = Assert.Single(StockPosting.ForCancellation(posted, new Dictionary<Guid, int> { [line.Id] = 2 }));

        Assert.Equal((2, 5m, (Guid?)Shelf1), (movement.LineNo, movement.Quantity, movement.Key.LocationId));
    }

    [Fact]
    public void Lock_order_is_by_key_whatever_the_input_order()
    {
        var keys = Enumerable.Range(0, 20)
            .Select(_ => new StockMovement(Guid.NewGuid(), 1, new StockKey(Guid.NewGuid(), Guid.NewGuid(), null, null), -1))
            .ToList();

        var forward = StockPosting.InLockOrder(keys).Select(m => m.Key);
        var backward = StockPosting.InLockOrder(Enumerable.Reverse(keys)).Select(m => m.Key);

        Assert.Equal(forward, backward);
        Assert.Equal(forward.Order(), forward);
    }

    [Fact]
    public void Document_numbers_have_prefix_year_and_five_digits()
    {
        Assert.Equal("PN-2026-00001", StockDocument.FormatNumber(StockDocumentType.Receipt, 2026, 1));
        Assert.Equal("PX-2026-01234", StockDocument.FormatNumber(StockDocumentType.Issue, 2026, 1234));
        Assert.Equal("PC-2027-00042", StockDocument.FormatNumber(StockDocumentType.Transfer, 2027, 42));
    }

    [Theory]
    [InlineData(StockDocumentType.Receipt, StockDocumentReason.Purchase, true)]
    [InlineData(StockDocumentType.Receipt, StockDocumentReason.OpeningBalance, true)]
    [InlineData(StockDocumentType.Receipt, StockDocumentReason.Production, false)]
    [InlineData(StockDocumentType.Issue, StockDocumentReason.Production, true)]
    [InlineData(StockDocumentType.Issue, StockDocumentReason.Purchase, false)]
    [InlineData(StockDocumentType.Transfer, null, true)]
    [InlineData(StockDocumentType.Transfer, StockDocumentReason.Other, false)]
    public void Reasons_depend_on_document_type(StockDocumentType type, StockDocumentReason? reason, bool allowed) =>
        Assert.Equal(allowed, StockDocument.IsReasonAllowed(type, reason));
}

public sealed class StockDocumentDraftValidatorTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // 2026-10-21 23:30 in Vietnam is still 2026-10-21 16:30 UTC.
    private readonly StockDocumentDraftValidator _validator =
        new(new BusinessClock(new FixedTime(new DateTimeOffset(2026, 10, 21, 16, 30, 0, TimeSpan.Zero))));

    private static readonly DateOnly Today = new(2026, 10, 21);

    private static StockDocumentDraft Receipt(params StockLineDraft[] lines) => new(
        StockDocumentType.Receipt, Today, Guid.NewGuid(), null, StockDocumentReason.Purchase, null, null, null, null,
        lines.Length > 0 ? lines : [Line()]);

    private static StockLineDraft Line(decimal qty = 1, Guid? location = null, Guid? toLocation = null,
        DateOnly? mfg = null, DateOnly? exp = null) =>
        new(Guid.NewGuid(), location, toLocation, null, null, mfg, exp, qty, null);

    [Fact]
    public void Valid_receipt_passes() => Assert.True(_validator.Validate(Receipt()).IsValid);

    [Fact]
    public void Today_is_computed_in_vietnam_time()
    {
        Assert.True(_validator.Validate(Receipt() with { DocumentDate = Today }).IsValid);
        Assert.False(_validator.Validate(Receipt() with { DocumentDate = Today.AddDays(1) }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0005)]
    public void Rejects_bad_quantities(double qty) =>
        Assert.False(_validator.Validate(Receipt(Line((decimal)qty))).IsValid);

    [Fact]
    public void Requires_at_least_one_line() =>
        Assert.False(_validator.Validate(Receipt() with { Lines = [] }).IsValid);

    [Fact]
    public void Expiry_must_not_precede_manufacture() =>
        Assert.False(_validator.Validate(Receipt(Line(mfg: Today, exp: Today.AddDays(-1)))).IsValid);

    [Fact]
    public void Reason_must_match_type() =>
        Assert.False(_validator.Validate(Receipt() with { Reason = StockDocumentReason.Production }).IsValid);

    [Fact]
    public void Transfer_needs_a_destination_that_differs_from_the_source()
    {
        var warehouse = Guid.NewGuid();
        var shelf1 = Guid.NewGuid();
        var transfer = new StockDocumentDraft(StockDocumentType.Transfer, Today, warehouse, warehouse, null, null, null, null, null,
            [Line(location: shelf1, toLocation: shelf1)]);

        Assert.False(_validator.Validate(transfer).IsValid);
        Assert.True(_validator.Validate(transfer with { Lines = [Line(location: shelf1, toLocation: Guid.NewGuid())] }).IsValid);
        Assert.False(_validator.Validate(transfer with { ToWarehouseId = null }).IsValid);
    }
}
