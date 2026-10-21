using FluentValidation;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Catalog;

/// <summary>CRUD for code/name catalogs that have no extra fields (units, material groups).</summary>
public interface ICatalogService<T> where T : CatalogEntity
{
    Task<IReadOnlyList<CatalogItemDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<CatalogItemDto> CreateAsync(SaveCatalogItemRequest request, CancellationToken ct = default);
    Task<CatalogItemDto> UpdateAsync(Guid id, SaveCatalogItemRequest request, CancellationToken ct = default);

    /// <summary>Fails with 409 (InUse) when anything references the entry; deactivate it instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class CatalogService<T>(IAppDbContext db, IValidator<SaveCatalogItemRequest> validator) : ICatalogService<T>
    where T : CatalogEntity, new()
{
    private DbSet<T> Set => db.Set<T>();

    public async Task<IReadOnlyList<CatalogItemDto>> ListAsync(bool activeOnly, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogItemDto(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(ct);

    public async Task<CatalogItemDto> CreateAsync(SaveCatalogItemRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var entity = new T();
        await ApplyAsync(entity, request, ct);
        Set.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<CatalogItemDto> UpdateAsync(Guid id, SaveCatalogItemRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var entity = await FindAsync(id, ct);
        await ApplyAsync(entity, request, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Set.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct); // FK restrict -> 409 InUse in the API exception handler
    }

    private async Task ApplyAsync(T entity, SaveCatalogItemRequest request, CancellationToken ct)
    {
        var code = CatalogEntity.NormalizeCode(request.Code);
        var name = request.Name.Trim();
        if (await Set.AnyAsync(x => x.Code == code && x.Id != entity.Id, ct))
            throw new ConflictException($"Code '{code}' already exists.", ErrorCodes.DuplicateCode);
        if (await Set.AnyAsync(x => x.Name == name && x.Id != entity.Id, ct))
            throw new ConflictException($"Name '{name}' already exists.", ErrorCodes.DuplicateName);

        entity.Code = code;
        entity.Name = name;
        entity.IsActive = request.IsActive;
    }

    private async Task<T> FindAsync(Guid id, CancellationToken ct) =>
        await Set.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For<T>(id);

    private static CatalogItemDto ToDto(T x) => new(x.Id, x.Code, x.Name, x.IsActive);
}
