using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Employees;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Employees;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct = default);
    Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto> SetPhotoAsync(Guid id, Stream content, CancellationToken ct = default);
    Task RemovePhotoAsync(Guid id, CancellationToken ct = default);
}

public sealed class EmployeeService(
    IAppDbContext db,
    IFileStorage fileStorage,
    IValidator<SaveEmployeeRequest> validator) : IEmployeeService
{
    private const string PhotoFolder = "employees";

    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, EmployeeListQuery.MaxPageSize);

        var employees = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            employees = employees.Where(e =>
                e.Code.ToLower().Contains(term)
                || AppDbFunctions.Unaccent(e.FullName.ToLower()).Contains(AppDbFunctions.Unaccent(term))
                || (e.Email != null && e.Email.ToLower().Contains(term)));
        }
        if (query.DepartmentId is { } departmentId)
            employees = employees.Where(e => e.DepartmentId == departmentId);
        if (query.PositionId is { } positionId)
            employees = employees.Where(e => e.PositionId == positionId);
        if (query.Status is { } status)
            employees = employees.Where(e => e.Status == status);

        var total = await employees.CountAsync(ct);
        var rows = await ApplySort(employees, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToRow)
            .ToListAsync(ct);

        return new PagedResult<EmployeeDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Employees.AsNoTracking().Where(e => e.Id == id).Select(ToRow).SingleOrDefaultAsync(ct)
            ?? throw NotFoundException.For<Employee>(id);
        return ToDto(row);
    }

    public async Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var code = Employee.NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, exceptId: null, ct);
        await EnsureLookupsExistAsync(request, ct);

        var employee = new Employee { Code = code, FullName = request.FullName.Trim() };
        Apply(employee, request);
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        return await GetAsync(employee.Id, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var employee = await FindAsync(id, ct);
        var code = Employee.NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, exceptId: id, ct);
        await EnsureLookupsExistAsync(request, ct);

        employee.Code = code;
        Apply(employee, request);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await FindAsync(id, ct);
        db.Employees.Remove(employee); // soft delete via AuditableEntityInterceptor; photo kept for restore
        await db.SaveChangesAsync(ct);
    }

    public async Task<EmployeeDto> SetPhotoAsync(Guid id, Stream content, CancellationToken ct = default)
    {
        var employee = await FindAsync(id, ct);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0 || buffer.Length > EmployeeRules.MaxPhotoBytes)
            throw PhotoError($"Photo must be between 1 byte and {EmployeeRules.MaxPhotoBytes / 1024 / 1024} MB.");

        var extension = ImageFormat.DetectExtension(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
            ?? throw PhotoError("Photo must be a JPG, PNG or WEBP image.");

        buffer.Position = 0;
        var newPath = await fileStorage.SaveAsync(buffer, PhotoFolder, extension, ct);
        var oldPath = employee.PhotoPath;
        employee.PhotoPath = newPath;
        await db.SaveChangesAsync(ct);

        if (oldPath is not null)
            await fileStorage.DeleteAsync(oldPath, ct);

        return await GetAsync(id, ct);
    }

    public async Task RemovePhotoAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await FindAsync(id, ct);
        if (employee.PhotoPath is not { } path) return;

        employee.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        await fileStorage.DeleteAsync(path, ct);
    }

    private static void Apply(Employee employee, SaveEmployeeRequest request)
    {
        employee.FullName = request.FullName.Trim();
        employee.DepartmentId = request.DepartmentId;
        employee.PositionId = request.PositionId;
        employee.Email = TextNormalizer.NullIfBlank(request.Email);
        employee.Phone = TextNormalizer.NullIfBlank(request.Phone);
        employee.Status = request.Status;
    }

    private async Task<Employee> FindAsync(Guid id, CancellationToken ct) =>
        await db.Employees.SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw NotFoundException.For<Employee>(id);

    private async Task EnsureCodeAvailableAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(e => e.Code == code && e.Id != exceptId, ct))
            throw new ConflictException($"Employee code '{code}' already exists.");
    }

    private async Task EnsureLookupsExistAsync(SaveEmployeeRequest request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
            failures.Add(new(nameof(request.DepartmentId), "Department does not exist."));
        if (!await db.Positions.AnyAsync(p => p.Id == request.PositionId, ct))
            failures.Add(new(nameof(request.PositionId), "Position does not exist."));
        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private static ValidationException PhotoError(string message) => new([new ValidationFailure("file", message)]);

    private static IQueryable<Employee> ApplySort(IQueryable<Employee> query, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        var key = sort?.TrimStart('-').ToLowerInvariant();

        IOrderedQueryable<Employee> ordered = key switch
        {
            "fullname" => descending ? query.OrderByDescending(e => e.FullName) : query.OrderBy(e => e.FullName),
            "department" => descending ? query.OrderByDescending(e => e.Department.Name) : query.OrderBy(e => e.Department.Name),
            "position" => descending ? query.OrderByDescending(e => e.Position.Name) : query.OrderBy(e => e.Position.Name),
            "status" => descending ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            "createdat" => descending ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt),
            _ => descending ? query.OrderByDescending(e => e.Code) : query.OrderBy(e => e.Code),
        };
        return ordered.ThenBy(e => e.Id);
    }

    // Two-stage projection: SQL selects columns, then PhotoPath is turned into a URL in memory.
    private sealed record EmployeeRow(
        Guid Id, string Code, string FullName, Guid DepartmentId, string DepartmentName, Guid PositionId,
        string PositionName, string? Email, string? Phone, string? PhotoPath, EmployeeStatus Status,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    private static readonly System.Linq.Expressions.Expression<Func<Employee, EmployeeRow>> ToRow = e => new EmployeeRow(
        e.Id, e.Code, e.FullName, e.DepartmentId, e.Department.Name, e.PositionId, e.Position.Name,
        e.Email, e.Phone, e.PhotoPath, e.Status, e.CreatedAt, e.UpdatedAt);

    private EmployeeDto ToDto(EmployeeRow r) => new(
        r.Id, r.Code, r.FullName, r.DepartmentId, r.DepartmentName, r.PositionId, r.PositionName,
        r.Email, r.Phone, r.PhotoPath is null ? null : fileStorage.GetPublicUrl(r.PhotoPath),
        r.Status, r.CreatedAt, r.UpdatedAt);
}
