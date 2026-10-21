using FluentValidation;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Catalog;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<WarehouseDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<WarehouseDto> CreateAsync(SaveWarehouseRequest request, CancellationToken ct = default);
    Task<WarehouseDto> UpdateAsync(Guid id, SaveWarehouseRequest request, CancellationToken ct = default);
    /// <summary>Also deletes its locations; fails with 409 (InUse) if the warehouse or a location was used.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<LocationDto>> ListLocationsAsync(Guid warehouseId, bool activeOnly, CancellationToken ct = default);
    Task<LocationDto> CreateLocationAsync(Guid warehouseId, SaveLocationRequest request, CancellationToken ct = default);
    Task<LocationDto> UpdateLocationAsync(Guid id, SaveLocationRequest request, CancellationToken ct = default);
    Task DeleteLocationAsync(Guid id, CancellationToken ct = default);
}

public sealed class WarehouseService(
    IAppDbContext db,
    IValidator<SaveWarehouseRequest> warehouseValidator,
    IValidator<SaveLocationRequest> locationValidator) : IWarehouseService
{
    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(bool activeOnly, CancellationToken ct = default) =>
        await db.Warehouses.AsNoTracking()
            .Where(w => !activeOnly || w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Note, w.IsActive, w.Locations.Count))
            .ToListAsync(ct);

    public async Task<WarehouseDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Warehouses.AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Note, w.IsActive, w.Locations.Count))
            .SingleOrDefaultAsync(ct)
        ?? throw NotFoundException.For<Warehouse>(id);

    public async Task<WarehouseDto> CreateAsync(SaveWarehouseRequest request, CancellationToken ct = default)
    {
        await warehouseValidator.ValidateAndThrowAsync(request, ct);
        var warehouse = new Warehouse();
        await ApplyAsync(warehouse, request, ct);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return await GetAsync(warehouse.Id, ct);
    }

    public async Task<WarehouseDto> UpdateAsync(Guid id, SaveWarehouseRequest request, CancellationToken ct = default)
    {
        await warehouseValidator.ValidateAndThrowAsync(request, ct);
        var warehouse = await FindWarehouseAsync(id, ct);
        await ApplyAsync(warehouse, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        db.Warehouses.Remove(await FindWarehouseAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LocationDto>> ListLocationsAsync(Guid warehouseId, bool activeOnly, CancellationToken ct = default)
    {
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct))
            throw NotFoundException.For<Warehouse>(warehouseId);

        return await db.Locations.AsNoTracking()
            .Where(l => l.WarehouseId == warehouseId && (!activeOnly || l.IsActive))
            .OrderBy(l => l.Code)
            .Select(l => new LocationDto(l.Id, l.WarehouseId, l.Code, l.Description, l.IsActive))
            .ToListAsync(ct);
    }

    public async Task<LocationDto> CreateLocationAsync(Guid warehouseId, SaveLocationRequest request, CancellationToken ct = default)
    {
        await locationValidator.ValidateAndThrowAsync(request, ct);
        await FindWarehouseAsync(warehouseId, ct);

        var location = new Location { WarehouseId = warehouseId };
        await ApplyAsync(location, request, ct);
        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);
        return ToDto(location);
    }

    public async Task<LocationDto> UpdateLocationAsync(Guid id, SaveLocationRequest request, CancellationToken ct = default)
    {
        await locationValidator.ValidateAndThrowAsync(request, ct);
        var location = await FindLocationAsync(id, ct);
        await ApplyAsync(location, request, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(location);
    }

    public async Task DeleteLocationAsync(Guid id, CancellationToken ct = default)
    {
        db.Locations.Remove(await FindLocationAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Warehouse warehouse, SaveWarehouseRequest request, CancellationToken ct)
    {
        var code = CatalogEntity.NormalizeCode(request.Code);
        var name = request.Name.Trim();
        if (await db.Warehouses.AnyAsync(w => w.Code == code && w.Id != warehouse.Id, ct))
            throw new ConflictException($"Warehouse code '{code}' already exists.", ErrorCodes.DuplicateCode);
        if (await db.Warehouses.AnyAsync(w => w.Name == name && w.Id != warehouse.Id, ct))
            throw new ConflictException($"Warehouse name '{name}' already exists.", ErrorCodes.DuplicateName);

        warehouse.Code = code;
        warehouse.Name = name;
        warehouse.Note = TextNormalizer.NullIfBlank(request.Note);
        warehouse.IsActive = request.IsActive;
    }

    private async Task ApplyAsync(Location location, SaveLocationRequest request, CancellationToken ct)
    {
        var code = CatalogEntity.NormalizeCode(request.Code);
        if (await db.Locations.AnyAsync(l => l.WarehouseId == location.WarehouseId && l.Code == code && l.Id != location.Id, ct))
            throw new ConflictException($"Location '{code}' already exists in this warehouse.", ErrorCodes.DuplicateCode);

        location.Code = code;
        location.Description = TextNormalizer.NullIfBlank(request.Description);
        location.IsActive = request.IsActive;
    }

    private async Task<Warehouse> FindWarehouseAsync(Guid id, CancellationToken ct) =>
        await db.Warehouses.SingleOrDefaultAsync(w => w.Id == id, ct) ?? throw NotFoundException.For<Warehouse>(id);

    private async Task<Location> FindLocationAsync(Guid id, CancellationToken ct) =>
        await db.Locations.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw NotFoundException.For<Location>(id);

    private static LocationDto ToDto(Location l) => new(l.Id, l.WarehouseId, l.Code, l.Description, l.IsActive);
}
