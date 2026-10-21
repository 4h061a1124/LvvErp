using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Import;
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
    Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default);
}

public sealed class EmployeeImportService(
    IAppDbContext db,
    ILookupService lookups,
    IExcelImportFile excelFile) : IEmployeeImportService
{
    public const int MaxRows = ImportLimits.MaxRows;

    private static readonly string[] Guide =
    [
        "Hướng dẫn nhập danh sách nhân viên",
        "",
        "• Nhập dữ liệu ở sheet \"Nhân viên\", bắt đầu từ dòng 2. Không đổi thứ tự hoặc tên cột.",
        "• Cột có dấu * là bắt buộc.",
        "• Bộ phận, Chức danh: chọn từ danh sách hoặc gõ mã/tên (không phân biệt hoa thường, có dấu hay không).",
        "• Trạng thái: Đang làm / Tạm nghỉ / Đã nghỉ. Để trống = Đang làm.",
        "• Mã NV trong file không được trùng nhau.",
        "• Nếu có bất kỳ dòng nào lỗi, toàn bộ file sẽ không được lưu.",
        "• Khi bật \"Cập nhật nếu trùng mã NV\": dòng có mã đã tồn tại sẽ cập nhật nhân viên đó;",
        "  ô Email, Số điện thoại, Trạng thái để trống sẽ giữ nguyên giá trị hiện tại.",
    ];

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct = default) =>
        excelFile.CreateTemplate(new ExcelTemplate(
            "Nhân viên",
            EmployeeImportColumns.All,
            [
                new(EmployeeImportColumns.Department, "Bộ phận", (await lookups.GetDepartmentsAsync(ct)).Select(d => d.Name).ToList()),
                new(EmployeeImportColumns.Position, "Chức danh", (await lookups.GetPositionsAsync(ct)).Select(p => p.Name).ToList()),
                new(EmployeeImportColumns.Status, "Trạng thái", EmployeeRules.StatusLabelsVi.Values.ToList()),
            ],
            Guide));

    public async Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default)
    {
        var rows = excelFile.ReadRows(content, EmployeeImportColumns.All).Select(EmployeeImportRow.From).ToList();
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
            return new ImportResult(false, rows.Count, 0, 0, errors);

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
        return new ImportResult(true, rows.Count, validRows.Count - updated, updated, []);
    }

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
