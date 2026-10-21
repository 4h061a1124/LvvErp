using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Reports;

public interface IStockReportService
{
    Task<PagedResult<StockOnHandDto>> GetOnHandAsync(StockOnHandQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<StockOnHandDetailDto>> GetOnHandDetailAsync(Guid materialId, Guid? warehouseId, CancellationToken ct = default);
    Task<IReadOnlyList<BelowMinStockDto>> GetBelowMinAsync(CancellationToken ct = default);
    Task<StockCardDto> GetStockCardAsync(StockCardQuery query, CancellationToken ct = default);
}

/// <summary>One ledger row before the running balance is added.</summary>
public sealed record StockCardMovement(
    Guid TransactionId, DateOnly Date, Guid DocumentId, string DocumentNumber, StockDocumentType DocumentType,
    StockDocumentReason? Reason, bool IsReversal, string WarehouseName, string? LocationCode, string? LotNumber,
    string? Partner, string? Recipient, string? OtherWarehouseName, decimal Quantity);

public static class StockCard
{
    /// <summary>Adds In/Out columns and the running balance, starting from <paramref name="opening"/>.</summary>
    public static IReadOnlyList<StockCardEntryDto> Build(decimal opening, IEnumerable<StockCardMovement> movements)
    {
        var balance = opening;
        var entries = new List<StockCardEntryDto>();
        foreach (var m in movements)
        {
            balance += m.Quantity;
            entries.Add(new StockCardEntryDto(
                m.TransactionId, m.Date, m.DocumentId, m.DocumentNumber, m.DocumentType, m.Reason, m.IsReversal,
                m.WarehouseName, m.LocationCode, m.LotNumber, m.Partner, m.Recipient, m.OtherWarehouseName,
                In: m.Quantity > 0 ? m.Quantity : 0,
                Out: m.Quantity < 0 ? -m.Quantity : 0,
                Balance: balance));
        }
        return entries;
    }
}

public sealed class StockReportService(IAppDbContext db, IFileStorage fileStorage) : IStockReportService
{
    /// <summary>A stock card longer than this is cut off (narrow the date range).</summary>
    public const int MaxCardEntries = 5000;

    public async Task<PagedResult<StockOnHandDto>> GetOnHandAsync(StockOnHandQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, StockOnHandQuery.MaxPageSize);
        var warehouseId = query.WarehouseId;

        var materials = db.Materials.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            materials = materials.Where(m =>
                m.Code.ToLower().Contains(term)
                || AppDbFunctions.Unaccent(m.Name.ToLower()).Contains(AppDbFunctions.Unaccent(term))
                || (m.Specification != null && AppDbFunctions.Unaccent(m.Specification.ToLower()).Contains(AppDbFunctions.Unaccent(term))));
        }
        if (query.GroupId is { } groupId)
            materials = materials.Where(m => m.GroupId == groupId);

        var rows = materials.Select(m => new
        {
            Material = m,
            Quantity = db.StockBalances.Where(b => b.MaterialId == m.Id && (warehouseId == null || b.WarehouseId == warehouseId))
                .Sum(b => (decimal?)b.Quantity) ?? 0,
            Total = db.StockBalances.Where(b => b.MaterialId == m.Id).Sum(b => (decimal?)b.Quantity) ?? 0,
        });

        if (query.BelowMinOnly)
            rows = rows.Where(r => r.Material.IsActive && r.Material.MinStock > 0 && r.Total < r.Material.MinStock);
        else if (query.IncludeZero)
            rows = rows.Where(r => r.Material.IsActive || r.Quantity > 0);
        else
            rows = rows.Where(r => r.Quantity > 0);

        var total = await rows.CountAsync(ct);
        var descending = query.Sort?.StartsWith('-') == true;
        var ordered = query.Sort?.TrimStart('-').ToLowerInvariant() switch
        {
            "name" => descending ? rows.OrderByDescending(r => r.Material.Name) : rows.OrderBy(r => r.Material.Name),
            "quantity" => descending ? rows.OrderByDescending(r => r.Quantity) : rows.OrderBy(r => r.Quantity),
            _ => descending ? rows.OrderByDescending(r => r.Material.Code) : rows.OrderBy(r => r.Material.Code),
        };

        var items = await ordered.ThenBy(r => r.Material.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Material.Id, r.Material.Code, r.Material.Name, r.Material.Specification, UnitName = r.Material.Unit.Name,
                r.Material.GroupId, GroupName = r.Material.Group.Name, r.Material.MinStock, r.Quantity, r.Total,
                r.Material.TrackLots, r.Material.PhotoPath,
            })
            .ToListAsync(ct);

        return new PagedResult<StockOnHandDto>(
            items.Select(i => new StockOnHandDto(
                i.Id, i.Code, i.Name, i.Specification, i.UnitName, i.GroupId, i.GroupName, i.MinStock, i.Quantity, i.Total,
                IsBelowMin: i.MinStock > 0 && i.Total < i.MinStock, i.TrackLots,
                i.PhotoPath is null ? null : fileStorage.GetPublicUrl(i.PhotoPath))).ToList(),
            page, pageSize, total);
    }

    public async Task<IReadOnlyList<StockOnHandDetailDto>> GetOnHandDetailAsync(Guid materialId, Guid? warehouseId, CancellationToken ct = default)
    {
        await EnsureMaterialAsync(materialId, ct);
        return await db.StockBalances.AsNoTracking()
            .Where(b => b.MaterialId == materialId && b.Quantity > 0 && (warehouseId == null || b.WarehouseId == warehouseId))
            .OrderBy(b => b.Warehouse.Name)
            .ThenBy(b => b.Location!.Code)
            .ThenBy(b => b.Lot == null || b.Lot.ExpiryDate == null)
            .ThenBy(b => b.Lot!.ExpiryDate)
            .Select(b => new StockOnHandDetailDto(
                b.WarehouseId, b.Warehouse.Name, b.LocationId, b.Location != null ? b.Location.Code : null,
                b.LotId, b.Lot != null ? b.Lot.LotNumber : null, b.Lot != null ? b.Lot.ExpiryDate : null, b.Quantity))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BelowMinStockDto>> GetBelowMinAsync(CancellationToken ct = default)
    {
        var rows = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive && m.MinStock > 0)
            .Select(m => new
            {
                m.Id, m.Code, m.Name, UnitName = m.Unit.Name, m.MinStock,
                Quantity = db.StockBalances.Where(b => b.MaterialId == m.Id).Sum(b => (decimal?)b.Quantity) ?? 0,
            })
            .Where(r => r.Quantity < r.MinStock)
            .ToListAsync(ct);

        // Most critical first: the smallest share of the minimum still in stock.
        return rows
            .OrderBy(r => r.Quantity / r.MinStock)
            .ThenBy(r => r.Code)
            .Select(r => new BelowMinStockDto(r.Id, r.Code, r.Name, r.UnitName, r.MinStock, r.Quantity, r.MinStock - r.Quantity))
            .ToList();
    }

    public async Task<StockCardDto> GetStockCardAsync(StockCardQuery query, CancellationToken ct = default)
    {
        var material = await db.Materials.AsNoTracking()
            .Where(m => m.Id == query.MaterialId)
            .Select(m => new { m.Id, m.Code, m.Name, UnitName = m.Unit.Name })
            .SingleOrDefaultAsync(ct) ?? throw NotFoundException.For<Material>(query.MaterialId);
        string? warehouseName = null;
        if (query.WarehouseId is { } wid)
            warehouseName = await db.Warehouses.Where(w => w.Id == wid).Select(w => w.Name).SingleOrDefaultAsync(ct)
                ?? throw NotFoundException.For<Warehouse>(wid);

        var ledger = db.StockTransactions.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.MaterialId == query.MaterialId && (query.WarehouseId == null || t.WarehouseId == query.WarehouseId));

        var opening = query.From is { } from
            ? await ledger.Where(t => t.TransactionDate < from).SumAsync(t => (decimal?)t.Quantity, ct) ?? 0
            : 0;

        var inRange = ledger;
        if (query.From is { } f) inRange = inRange.Where(t => t.TransactionDate >= f);
        if (query.To is { } to) inRange = inRange.Where(t => t.TransactionDate <= to);

        // Within a day: postings in the order they happened (CreatedAt is shared by one posting), then by
        // line, outgoing before incoming (a transfer's two rows). Ids can't be used: UUIDv7s created in the
        // same millisecond are not ordered.
        var movements = await inRange
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.DocumentId)
            .ThenBy(t => t.DocumentLine.LineNo)
            .ThenBy(t => t.Quantity)
            .Take(MaxCardEntries + 1)
            .Select(t => new StockCardMovement(
                t.Id, t.TransactionDate, t.DocumentId, t.Document.Number, t.Document.Type, t.Document.Reason, t.IsReversal,
                t.Warehouse.Name, t.Location != null ? t.Location.Code : null, t.Lot != null ? t.Lot.LotNumber : null,
                t.Document.Partner,
                t.Document.Employee != null ? t.Document.Employee.FullName : t.Document.Department != null ? t.Document.Department.Name : null,
                t.Document.Type != StockDocumentType.Transfer ? null
                    : t.WarehouseId == t.Document.WarehouseId ? t.Document.ToWarehouse!.Name : t.Document.Warehouse.Name,
                t.Quantity))
            .ToListAsync(ct);

        var truncated = movements.Count > MaxCardEntries;
        if (truncated) movements.RemoveAt(movements.Count - 1);

        var entries = StockCard.Build(opening, movements);
        var totalIn = entries.Sum(e => e.In);
        var totalOut = entries.Sum(e => e.Out);
        return new StockCardDto(
            material.Id, material.Code, material.Name, material.UnitName, query.WarehouseId, warehouseName,
            query.From, query.To, opening, totalIn, totalOut, opening + totalIn - totalOut, truncated, entries);
    }

    private async Task EnsureMaterialAsync(Guid materialId, CancellationToken ct)
    {
        if (!await db.Materials.AnyAsync(m => m.Id == materialId, ct))
            throw NotFoundException.For<Material>(materialId);
    }
}
