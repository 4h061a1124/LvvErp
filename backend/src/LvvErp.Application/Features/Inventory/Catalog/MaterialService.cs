using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Catalog;

public interface IMaterialService
{
    Task<PagedResult<MaterialDto>> ListAsync(MaterialListQuery query, CancellationToken ct = default);
    Task<MaterialDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<MaterialDto> CreateAsync(SaveMaterialRequest request, CancellationToken ct = default);
    Task<MaterialDto> UpdateAsync(Guid id, SaveMaterialRequest request, CancellationToken ct = default);
    /// <summary>Fails with 409 (InUse) once the material has lots or stock movements; deactivate it instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<MaterialDto> SetPhotoAsync(Guid id, Stream content, CancellationToken ct = default);
    Task RemovePhotoAsync(Guid id, CancellationToken ct = default);
}

public sealed class MaterialService(
    IAppDbContext db,
    IFileStorage fileStorage,
    PhotoUploader photoUploader,
    IValidator<SaveMaterialRequest> validator) : IMaterialService
{
    private const string PhotoFolder = "materials";

    public async Task<PagedResult<MaterialDto>> ListAsync(MaterialListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaterialListQuery.MaxPageSize);

        var materials = db.Materials.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            materials = materials.Where(m =>
                m.Code.ToLower().Contains(term)
                || AppDbFunctions.Unaccent(m.Name.ToLower()).Contains(AppDbFunctions.Unaccent(term))
                || (m.Specification != null
                    && AppDbFunctions.Unaccent(m.Specification.ToLower()).Contains(AppDbFunctions.Unaccent(term))));
        }
        if (query.GroupId is { } groupId)
            materials = materials.Where(m => m.GroupId == groupId);
        if (query.IsActive is { } isActive)
            materials = materials.Where(m => m.IsActive == isActive);

        var total = await materials.CountAsync(ct);
        var rows = await ApplySort(materials, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToRow)
            .ToListAsync(ct);

        return new PagedResult<MaterialDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<MaterialDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Materials.AsNoTracking().Where(m => m.Id == id).Select(ToRow).SingleOrDefaultAsync(ct)
            ?? throw NotFoundException.For<Material>(id);
        return ToDto(row);
    }

    public async Task<MaterialDto> CreateAsync(SaveMaterialRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var material = new Material();
        await ApplyAsync(material, request, ct);
        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);
        return await GetAsync(material.Id, ct);
    }

    public async Task<MaterialDto> UpdateAsync(Guid id, SaveMaterialRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var material = await FindAsync(id, ct);
        await ApplyAsync(material, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        db.Materials.Remove(material);
        await db.SaveChangesAsync(ct); // FK restrict -> 409 InUse
        await photoUploader.DeleteAsync(material.PhotoPath, ct);
    }

    public async Task<MaterialDto> SetPhotoAsync(Guid id, Stream content, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        var newPath = await photoUploader.SaveAsync(content, PhotoFolder, ct);
        var oldPath = material.PhotoPath;
        material.PhotoPath = newPath;
        await db.SaveChangesAsync(ct);
        await photoUploader.DeleteAsync(oldPath, ct);
        return await GetAsync(id, ct);
    }

    public async Task RemovePhotoAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material.PhotoPath is not { } path) return;

        material.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        await photoUploader.DeleteAsync(path, ct);
    }

    private async Task ApplyAsync(Material material, SaveMaterialRequest request, CancellationToken ct)
    {
        var code = Material.NormalizeCode(request.Code);
        if (await db.Materials.AnyAsync(m => m.Code == code && m.Id != material.Id, ct))
            throw new ConflictException($"Material code '{code}' already exists.", ErrorCodes.DuplicateCode);

        // Inactive units/groups can't be picked for a new value, but an existing link may stay.
        var failures = new List<ValidationFailure>();
        if (request.UnitId != material.UnitId && !await db.Units.AnyAsync(u => u.Id == request.UnitId && u.IsActive, ct))
            failures.Add(new(nameof(request.UnitId), "Unit does not exist or is inactive."));
        if (request.GroupId != material.GroupId && !await db.MaterialGroups.AnyAsync(g => g.Id == request.GroupId && g.IsActive, ct))
            failures.Add(new(nameof(request.GroupId), "Material group does not exist or is inactive."));
        if (failures.Count > 0)
            throw new ValidationException(failures);

        // Lines and ledger rows of a lot-tracked material all carry a lot (and vice versa).
        if (request.TrackLots != material.TrackLots && await db.StockDocumentLines.AnyAsync(l => l.MaterialId == material.Id, ct))
            throw new ConflictException("Lot tracking can't be changed once the material is used in stock documents.",
                ErrorCodes.TrackLotsLocked);

        material.Code = code;
        material.Name = request.Name.Trim();
        material.Specification = TextNormalizer.NullIfBlank(request.Specification);
        material.UnitId = request.UnitId;
        material.GroupId = request.GroupId;
        material.MinStock = request.MinStock;
        material.TrackLots = request.TrackLots;
        material.IsActive = request.IsActive;
    }

    private async Task<Material> FindAsync(Guid id, CancellationToken ct) =>
        await db.Materials.SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw NotFoundException.For<Material>(id);

    private static IQueryable<Material> ApplySort(IQueryable<Material> query, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        var key = sort?.TrimStart('-').ToLowerInvariant();

        IOrderedQueryable<Material> ordered = key switch
        {
            "name" => descending ? query.OrderByDescending(m => m.Name) : query.OrderBy(m => m.Name),
            "group" => descending ? query.OrderByDescending(m => m.Group.Name) : query.OrderBy(m => m.Group.Name),
            "createdat" => descending ? query.OrderByDescending(m => m.CreatedAt) : query.OrderBy(m => m.CreatedAt),
            _ => descending ? query.OrderByDescending(m => m.Code) : query.OrderBy(m => m.Code),
        };
        return ordered.ThenBy(m => m.Id);
    }

    // Two-stage projection: SQL selects columns, then PhotoPath is turned into a URL in memory.
    private sealed record MaterialRow(
        Guid Id, string Code, string Name, string? Specification, Guid UnitId, string UnitName, Guid GroupId,
        string GroupName, decimal MinStock, bool TrackLots, string? PhotoPath, bool IsActive,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    private static readonly Expression<Func<Material, MaterialRow>> ToRow = m => new MaterialRow(
        m.Id, m.Code, m.Name, m.Specification, m.UnitId, m.Unit.Name, m.GroupId, m.Group.Name,
        m.MinStock, m.TrackLots, m.PhotoPath, m.IsActive, m.CreatedAt, m.UpdatedAt);

    private MaterialDto ToDto(MaterialRow r) => new(
        r.Id, r.Code, r.Name, r.Specification, r.UnitId, r.UnitName, r.GroupId, r.GroupName, r.MinStock, r.TrackLots,
        r.PhotoPath is null ? null : fileStorage.GetPublicUrl(r.PhotoPath), r.IsActive, r.CreatedAt, r.UpdatedAt);
}
