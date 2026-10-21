using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Documents;

/// <summary>Read side used while filling in documents.</summary>
public interface IStockQueryService
{
    /// <summary>Stock of one material in one warehouse, earliest expiry first (FEFO), then oldest lot.</summary>
    Task<IReadOnlyList<AvailableStockDto>> GetAvailableAsync(Guid materialId, Guid warehouseId, CancellationToken ct = default);

    /// <summary>All lots of a material with their total quantity across warehouses.</summary>
    Task<IReadOnlyList<LotDto>> GetLotsAsync(Guid materialId, CancellationToken ct = default);
}

public sealed class StockQueryService(IAppDbContext db) : IStockQueryService
{
    public async Task<IReadOnlyList<AvailableStockDto>> GetAvailableAsync(Guid materialId, Guid warehouseId, CancellationToken ct = default)
    {
        await EnsureMaterialAsync(materialId, ct);
        return await db.StockBalances.AsNoTracking()
            .Where(b => b.MaterialId == materialId && b.WarehouseId == warehouseId && b.Quantity > 0)
            .OrderBy(b => b.Lot == null || b.Lot.ExpiryDate == null)
            .ThenBy(b => b.Lot!.ExpiryDate)
            .ThenBy(b => b.Lot!.CreatedAt)
            .ThenBy(b => b.Location!.Code)
            .Select(b => new AvailableStockDto(
                b.WarehouseId, b.LocationId, b.Location != null ? b.Location.Code : null,
                b.LotId, b.Lot != null ? b.Lot.LotNumber : null, b.Lot != null ? b.Lot.ExpiryDate : null, b.Quantity))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LotDto>> GetLotsAsync(Guid materialId, CancellationToken ct = default)
    {
        await EnsureMaterialAsync(materialId, ct);
        return await db.Lots.AsNoTracking()
            .Where(l => l.MaterialId == materialId)
            .OrderBy(l => l.ExpiryDate == null)
            .ThenBy(l => l.ExpiryDate)
            .ThenBy(l => l.CreatedAt)
            .Select(l => new LotDto(l.Id, l.LotNumber, l.ManufactureDate, l.ExpiryDate,
                db.StockBalances.Where(b => b.LotId == l.Id).Sum(b => (decimal?)b.Quantity) ?? 0))
            .ToListAsync(ct);
    }

    private async Task EnsureMaterialAsync(Guid materialId, CancellationToken ct)
    {
        if (!await db.Materials.AnyAsync(m => m.Id == materialId, ct))
            throw NotFoundException.For<Material>(materialId);
    }
}
