using System.Linq.Expressions;
using FluentValidation;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> ListAsync(SupplierListQuery query, CancellationToken ct = default);
    Task<SupplierDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken ct = default);
    Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken ct = default);
    /// <summary>Fails with 409 (InUse) once the supplier is on an order; deactivate it instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class SupplierService(IAppDbContext db, IValidator<SaveSupplierRequest> validator) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> ListAsync(SupplierListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, SupplierListQuery.MaxPageSize);

        var suppliers = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            suppliers = suppliers.Where(s =>
                s.Code.ToLower().Contains(term)
                || (s.TaxCode != null && s.TaxCode.Contains(term))
                || AppDbFunctions.Unaccent(s.Name.ToLower()).Contains(AppDbFunctions.Unaccent(term)));
        }
        if (query.IsActive is { } isActive)
            suppliers = suppliers.Where(s => s.IsActive == isActive);

        var total = await suppliers.CountAsync(ct);
        var descending = query.Sort?.StartsWith('-') == true;
        var ordered = query.Sort?.TrimStart('-').ToLowerInvariant() == "name"
            ? (descending ? suppliers.OrderByDescending(s => s.Name) : suppliers.OrderBy(s => s.Name))
            : (descending ? suppliers.OrderByDescending(s => s.Code) : suppliers.OrderBy(s => s.Code));

        var items = await ordered.ThenBy(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToListAsync(ct);
        return new PagedResult<SupplierDto>(items, page, pageSize, total);
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking().Where(s => s.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw NotFoundException.For<Supplier>(id);

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var supplier = new Supplier();
        await ApplyAsync(supplier, request, ct);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetAsync(supplier.Id, ct);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFoundException.For<Supplier>(id);
        await ApplyAsync(supplier, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFoundException.For<Supplier>(id);
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct); // FK restrict -> 409 InUse
    }

    private async Task ApplyAsync(Supplier supplier, SaveSupplierRequest request, CancellationToken ct)
    {
        var code = Supplier.NormalizeCode(request.Code);
        var taxCode = TextNormalizer.NullIfBlank(request.TaxCode);
        if (await db.Suppliers.AnyAsync(s => s.Code == code && s.Id != supplier.Id, ct))
            throw new ConflictException($"Supplier code '{code}' already exists.", ErrorCodes.DuplicateCode);
        if (taxCode is not null && await db.Suppliers.AnyAsync(s => s.TaxCode == taxCode && s.Id != supplier.Id, ct))
            throw new ConflictException($"Tax code '{taxCode}' already belongs to another supplier.", ErrorCodes.DuplicateTaxCode);

        supplier.Code = code;
        supplier.Name = request.Name.Trim();
        supplier.TaxCode = taxCode;
        supplier.Address = TextNormalizer.NullIfBlank(request.Address);
        supplier.ContactName = TextNormalizer.NullIfBlank(request.ContactName);
        supplier.Phone = TextNormalizer.NullIfBlank(request.Phone);
        supplier.Email = TextNormalizer.NullIfBlank(request.Email);
        supplier.PaymentTermDays = request.PaymentTermDays;
        supplier.PaymentTerms = TextNormalizer.NullIfBlank(request.PaymentTerms);
        supplier.IsActive = request.IsActive;
    }

    private static readonly Expression<Func<Supplier, SupplierDto>> ToDto = s => new SupplierDto(
        s.Id, s.Code, s.Name, s.TaxCode, s.Address, s.ContactName, s.Phone, s.Email,
        s.PaymentTermDays, s.PaymentTerms, s.IsActive, s.CreatedAt, s.UpdatedAt);
}
