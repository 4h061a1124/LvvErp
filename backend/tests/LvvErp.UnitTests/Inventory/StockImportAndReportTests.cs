using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Inventory.Import;
using LvvErp.Application.Features.Inventory.Reports;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.UnitTests.Inventory;

public sealed class StockCardTests
{
    private static StockCardMovement Move(int day, decimal qty, bool reversal = false) => new(
        Guid.NewGuid(), new DateOnly(2026, 10, day), Guid.NewGuid(), "PN-2026-00001", StockDocumentType.Receipt,
        null, reversal, "Kho chính", null, null, null, null, null, qty);

    [Fact]
    public void Running_balance_starts_from_opening_and_splits_in_and_out()
    {
        var entries = StockCard.Build(5, [Move(1, 10), Move(2, -3), Move(3, -12), Move(4, 3, reversal: true)]);

        Assert.Equal([15m, 12m, 0m, 3m], entries.Select(e => e.Balance));
        Assert.Equal([10m, 0m, 0m, 3m], entries.Select(e => e.In));
        Assert.Equal([0m, 3m, 12m, 0m], entries.Select(e => e.Out));
        Assert.True(entries[3].IsReversal);
    }

    [Fact]
    public void Empty_range_has_no_entries() => Assert.Empty(StockCard.Build(7, []));
}

public sealed class MaterialImportValidatorTests
{
    private static readonly LookupDto Kg = new(Guid.NewGuid(), "KG", "Kg");
    private static readonly LookupDto Piece = new(Guid.NewGuid(), "CAI", "Cái");
    private static readonly LookupDto RawMaterial = new(Guid.NewGuid(), "NVL", "Nguyên vật liệu");

    private static MaterialImportValidator Create(bool update = false, Dictionary<string, ExistingMaterial>? existing = null) =>
        new([Kg, Piece], [RawMaterial], existing ?? [], update);

    private static MaterialImportRow Row(int row = 2, string? code = "THEP-01", string? name = "Thép tấm", string? spec = null,
        string? unit = "kg", string? group = "nguyen vat lieu", string? min = null, string? lots = null) =>
        new(row, code, name, spec, unit, group, min, lots);

    [Fact]
    public void Valid_row_matches_lookups_by_code_or_name_and_upper_cases_code()
    {
        var (rows, errors) = Create().Validate([Row(code: "thep-01", unit: "cai", min: "12,5", lots: "Có")]);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal(("THEP-01", Piece.Id, RawMaterial.Id, (decimal?)12.5m, (bool?)true, false),
            (row.Code, row.UnitId, row.GroupId, row.MinStock, row.TrackLots, row.IsUpdate));
    }

    [Fact]
    public void Blank_optional_cells_stay_null()
    {
        var row = Assert.Single(Create().Validate([Row()]).Rows);
        Assert.Equal(((decimal?)null, (bool?)null, (string?)null), (row.MinStock, row.TrackLots, row.Specification));
    }

    [Fact]
    public void Reports_every_bad_cell()
    {
        var (rows, errors) = Create().Validate([Row(code: "THÉP 01", name: "", unit: "Thùng phi", group: "XYZ", min: "-1", lots: "có lẽ")]);

        Assert.Empty(rows);
        Assert.Equal(
            [ImportErrorCodes.InvalidFormat, ImportErrorCodes.Required, ImportErrorCodes.UnknownUnit,
             ImportErrorCodes.UnknownGroup, ImportErrorCodes.InvalidNumber, ImportErrorCodes.InvalidFormat],
            errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public void Duplicates_and_existing_codes()
    {
        var existing = new Dictionary<string, ExistingMaterial> { ["THEP-01"] = new(TrackLots: false, TrackLotsLocked: false) };

        var (_, errors) = Create(existing: existing).Validate([Row(), Row(row: 3, code: "SON-01"), Row(row: 4, code: "son-01")]);
        Assert.Equal([(2, ImportErrorCodes.AlreadyExists), (4, ImportErrorCodes.DuplicateInFile)], errors.Select(e => (e.Row, e.ErrorCode)));

        var (rows, updateErrors) = Create(update: true, existing).Validate([Row()]);
        Assert.Empty(updateErrors);
        Assert.True(Assert.Single(rows).IsUpdate);
    }

    [Fact]
    public void Lot_tracking_of_a_used_material_cannot_change()
    {
        var existing = new Dictionary<string, ExistingMaterial> { ["THEP-01"] = new(TrackLots: false, TrackLotsLocked: true) };

        Assert.Equal(ImportErrorCodes.TrackLotsLocked,
            Assert.Single(Create(update: true, existing).Validate([Row(lots: "Có")]).Errors).ErrorCode);
        Assert.Empty(Create(update: true, existing).Validate([Row(lots: "Không")]).Errors);
        Assert.Empty(Create(update: true, existing).Validate([Row()]).Errors);
    }
}

public sealed class OpeningBalanceValidatorTests
{
    private static readonly MaterialRef Steel = new(Guid.NewGuid(), TrackLots: false);
    private static readonly MaterialRef Paint = new(Guid.NewGuid(), TrackLots: true);
    private static readonly LookupDto Main = new(Guid.NewGuid(), "KC", "Kho chính");
    private static readonly LookupDto Spare = new(Guid.NewGuid(), "KP", "Kho phụ");
    private static readonly Guid ShelfA = Guid.NewGuid();

    private static OpeningBalanceValidator Create(Dictionary<(Guid, string), LotDates>? lots = null) => new(
        new Dictionary<string, MaterialRef> { ["THEP"] = Steel, ["SON"] = Paint },
        [Main, Spare],
        new Dictionary<(Guid, string), Guid> { [(Main.Id, "A-01")] = ShelfA },
        lots ?? []);

    private static OpeningBalanceRow Row(int row = 2, string? material = "thep", string? warehouse = "kho chinh",
        string? location = null, string? lot = null, string? mfg = null, string? exp = null, string? qty = "10") =>
        new(row, material, warehouse, location, lot, mfg, exp, qty);

    [Fact]
    public void Valid_rows_resolve_material_warehouse_location_and_dates()
    {
        var (lines, errors) = Create().Validate(
        [
            Row(location: "a-01", qty: "12,5"),
            Row(row: 3, material: "SON", warehouse: "KP", lot: "L1", mfg: "01/01/2026", exp: "2027-12-31"),
        ]);

        Assert.Empty(errors);
        Assert.Equal((Steel.Id, Main.Id, (Guid?)ShelfA, 12.5m), (lines[0].MaterialId, lines[0].WarehouseId, lines[0].LocationId, lines[0].Quantity));
        Assert.Equal(("L1", (DateOnly?)new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2027, 12, 31)),
            (lines[1].LotNumber, lines[1].ManufactureDate, lines[1].ExpiryDate));
    }

    [Fact]
    public void Unknown_references_and_bad_values_are_reported()
    {
        var (_, errors) = Create().Validate(
        [
            Row(material: "KHONGCO"),
            Row(row: 3, warehouse: "Kho xa"),
            Row(row: 4, location: "Z-99"),
            Row(row: 5, warehouse: "KP", qty: "0"),
            Row(row: 6, location: "A-01", qty: "1,5.0"),
        ]);

        Assert.Equal(
            [ImportErrorCodes.UnknownMaterial, ImportErrorCodes.UnknownWarehouse, ImportErrorCodes.UnknownLocation,
             ImportErrorCodes.InvalidNumber, ImportErrorCodes.InvalidNumber],
            errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public void Lot_rules_follow_the_material()
    {
        var (_, errors) = Create().Validate(
        [
            Row(material: "SON"),
            Row(row: 3, material: "THEP", lot: "L1"),
            Row(row: 4, material: "SON", lot: "L2", mfg: "31/12/2026", exp: "01/01/2026"),
            Row(row: 5, material: "SON", lot: "L3", exp: "31/02/2027"),
        ]);

        Assert.Equal(
            [(2, ImportErrorCodes.LotRequired), (3, ImportErrorCodes.LotNotAllowed), (4, ImportErrorCodes.InvalidDate), (5, ImportErrorCodes.InvalidDate)],
            errors.Select(e => (e.Row, e.ErrorCode)));
    }

    [Fact]
    public void Same_lot_must_have_the_same_dates_in_the_file_and_in_the_system()
    {
        var existing = new Dictionary<(Guid, string), LotDates> { [(Paint.Id, "OLD")] = new(null, new DateOnly(2027, 1, 1)) };

        var (_, errors) = Create(existing).Validate(
        [
            Row(material: "SON", lot: "L1", exp: "01/06/2027"),
            Row(row: 3, material: "SON", warehouse: "KP", lot: "L1", exp: "02/06/2027"),
            Row(row: 4, material: "SON", lot: "OLD", exp: "02/01/2027"),
            Row(row: 5, material: "SON", warehouse: "KP", lot: "OLD"),
        ]);

        Assert.Equal([3, 4], errors.Select(e => e.Row));
        Assert.All(errors, e => Assert.Equal(ImportErrorCodes.LotDatesMismatch, e.ErrorCode));
    }

    [Fact]
    public void Same_stock_key_twice_is_a_duplicate()
    {
        var (_, errors) = Create().Validate([Row(location: "A-01"), Row(row: 3, location: "a-01"), Row(row: 4)]);

        var error = Assert.Single(errors);
        Assert.Equal((3, ImportErrorCodes.DuplicateInFile), (error.Row, error.ErrorCode));
    }
}
