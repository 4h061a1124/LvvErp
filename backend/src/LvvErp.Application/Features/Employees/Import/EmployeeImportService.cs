using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Employees;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Employees.Import;

public interface IEmployeeImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// All-or-nothing: if any row has an error nothing is saved and the errors are returned.
    /// With <paramref name="updateExisting"/>, rows whose code already exists update that employee;
    /// blank optional cells (email, phone, status) then keep the current value.
    /// </summary>
    Task<EmployeeImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default);
}

public sealed class EmployeeImportService(
    IAppDbContext db,
    ILookupService lookups,
    IEmployeeExcelFile excelFile) : IEmployeeImportService
{
    public const int MaxRows = 5000;

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct = default) =>
        excelFile.CreateTemplate(await lookups.GetDepartmentsAsync(ct), await lookups.GetPositionsAsync(ct));

    public async Task<EmployeeImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default)
    {
        var rows = excelFile.ReadRows(content);
        if (rows.Count == 0)
            throw FileError("The file has no data rows.");
        if (rows.Count > MaxRows)
            throw FileError($"The file has {rows.Count} rows; the maximum is {MaxRows}.");

        var codes = rows
            .Select(r => r.Code?.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => Employee.NormalizeCode(c!))
            .Distinct()
            .ToList();
        var existing = await db.Employees.Where(e => codes.Contains(e.Code)).ToDictionaryAsync(e => e.Code, ct);

        var validator = new EmployeeImportValidator(
            await lookups.GetDepartmentsAsync(ct),
            await lookups.GetPositionsAsync(ct),
            existing.Keys.ToHashSet(),
            updateExisting);
        var (validRows, errors) = validator.Validate(rows);

        if (errors.Count > 0)
            return new EmployeeImportResult(false, rows.Count, 0, 0, errors);

        foreach (var row in validRows)
        {
            if (row.IsUpdate)
            {
                var employee = existing[row.Code];
                employee.FullName = row.FullName;
                employee.DepartmentId = row.DepartmentId;
                employee.PositionId = row.PositionId;
                employee.Email = row.Email ?? employee.Email;
                employee.Phone = row.Phone ?? employee.Phone;
                employee.Status = row.Status ?? employee.Status;
            }
            else
            {
                db.Employees.Add(new Employee
                {
                    Code = row.Code,
                    FullName = row.FullName,
                    DepartmentId = row.DepartmentId,
                    PositionId = row.PositionId,
                    Email = row.Email,
                    Phone = row.Phone,
                    Status = row.Status ?? EmployeeStatus.Active,
                });
            }
        }

        // A single SaveChanges runs in one transaction: either every row is stored or none.
        await db.SaveChangesAsync(ct);

        var updated = validRows.Count(r => r.IsUpdate);
        return new EmployeeImportResult(true, rows.Count, validRows.Count - updated, updated, []);
    }

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
