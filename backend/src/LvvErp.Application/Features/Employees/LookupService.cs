using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Employees;

public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetPositionsAsync(CancellationToken ct = default);
}

public sealed class LookupService(IAppDbContext db) : ILookupService
{
    public async Task<IReadOnlyList<LookupDto>> GetDepartmentsAsync(CancellationToken ct = default) =>
        await db.Departments.AsNoTracking()
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .Select(d => new LookupDto(d.Id, d.Code, d.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> GetPositionsAsync(CancellationToken ct = default) =>
        await db.Positions.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new LookupDto(p.Id, p.Code, p.Name))
            .ToListAsync(ct);
}
