using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Common.Validation;
using LvvErp.Application.Features.Employees;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Suppliers;

public static class SupplierImportColumns
{
    public const string Code = "Code";
    public const string Name = "Name";
    public const string TaxCode = "TaxCode";
    public const string Address = "Address";
    public const string ContactName = "ContactName";
    public const string Phone = "Phone";
    public const string Email = "Email";
    public const string PaymentTermDays = "PaymentTermDays";
    public const string PaymentTerms = "PaymentTerms";

    public static readonly IReadOnlyList<ExcelColumn> All =
    [
        new(Code, "Mã NCC *", 14, IsText: true),
        new(Name, "Tên nhà cung cấp *", 36),
        new(TaxCode, "Mã số thuế", 16, IsText: true),
        new(Address, "Địa chỉ", 40),
        new(ContactName, "Người liên hệ", 22),
        new(Phone, "Điện thoại", 16, IsText: true),
        new(Email, "Email", 26),
        new(PaymentTermDays, "Số ngày công nợ", 14),
        new(PaymentTerms, "Điều khoản thanh toán", 30),
    ];
}

public sealed record SupplierImportRow(
    int RowNumber, string? Code, string? Name, string? TaxCode, string? Address, string? ContactName,
    string? Phone, string? Email, string? PaymentTermDays, string? PaymentTerms)
{
    public static SupplierImportRow From(ExcelRow r) => new(
        r.RowNumber, r[SupplierImportColumns.Code], r[SupplierImportColumns.Name], r[SupplierImportColumns.TaxCode],
        r[SupplierImportColumns.Address], r[SupplierImportColumns.ContactName], r[SupplierImportColumns.Phone],
        r[SupplierImportColumns.Email], r[SupplierImportColumns.PaymentTermDays], r[SupplierImportColumns.PaymentTerms]);
}

/// <summary>Optional fields left blank stay null: keep the current value on update.</summary>
public sealed record ValidatedSupplierRow(
    int RowNumber, string Code, string Name, string? TaxCode, string? Address, string? ContactName,
    string? Phone, string? Email, int? PaymentTermDays, string? PaymentTerms, bool IsUpdate);

/// <summary>Pure row validation for the supplier import (no database access).</summary>
/// <param name="existingCodes">Upper-case codes already in the system.</param>
/// <param name="taxCodeOwners">Tax code → code of the supplier that has it.</param>
public sealed class SupplierImportValidator(
    IReadOnlySet<string> existingCodes,
    IReadOnlyDictionary<string, string> taxCodeOwners,
    bool updateExisting)
{
    public (IReadOnlyList<ValidatedSupplierRow> Rows, IReadOnlyList<ImportError> Errors) Validate(IReadOnlyList<SupplierImportRow> rows)
    {
        var valid = new List<ValidatedSupplierRow>();
        var errors = new List<ImportError>();
        var firstRowByCode = new Dictionary<string, int>();
        var firstRowByTaxCode = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var rowErrors = new List<ImportError>();
            void Error(string column, string code, string message, string? value = null) =>
                rowErrors.Add(new ImportError(row.RowNumber, column, code, message, value));

            var code = row.Code?.Trim() ?? string.Empty;
            if (code.Length == 0)
                Error(SupplierImportColumns.Code, ImportErrorCodes.Required, "Supplier code is required.");
            else if (code.Length > Supplier.CodeMaxLength)
                Error(SupplierImportColumns.Code, ImportErrorCodes.TooLong, $"Max {Supplier.CodeMaxLength} characters.", code);
            else if (!ValidationRules.CodePattern().IsMatch(code))
                Error(SupplierImportColumns.Code, ImportErrorCodes.InvalidFormat, "Only letters, digits, '.', '_' and '-'.", code);
            else
            {
                code = Supplier.NormalizeCode(code);
                if (firstRowByCode.TryGetValue(code, out var firstRow))
                    Error(SupplierImportColumns.Code, ImportErrorCodes.DuplicateInFile, $"Same code as row {firstRow}.", code);
                else
                    firstRowByCode[code] = row.RowNumber;
                if (!updateExisting && existingCodes.Contains(code))
                    Error(SupplierImportColumns.Code, ImportErrorCodes.AlreadyExists, "Supplier code already exists.", code);
            }

            var name = row.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
                Error(SupplierImportColumns.Name, ImportErrorCodes.Required, "Name is required.");
            else if (name.Length > Supplier.NameMaxLength)
                Error(SupplierImportColumns.Name, ImportErrorCodes.TooLong, $"Max {Supplier.NameMaxLength} characters.", name);

            var taxCode = TextNormalizer.NullIfBlank(row.TaxCode);
            if (taxCode is not null)
            {
                if (!Supplier.TaxCodePattern().IsMatch(taxCode))
                    Error(SupplierImportColumns.TaxCode, ImportErrorCodes.InvalidFormat, "10 digits, or 10 digits-3 digits.", taxCode);
                else if (firstRowByTaxCode.TryGetValue(taxCode, out var firstRow))
                    Error(SupplierImportColumns.TaxCode, ImportErrorCodes.DuplicateInFile, $"Same tax code as row {firstRow}.", taxCode);
                else
                {
                    firstRowByTaxCode[taxCode] = row.RowNumber;
                    if (taxCodeOwners.TryGetValue(taxCode, out var owner) && owner != code)
                        Error(SupplierImportColumns.TaxCode, ImportErrorCodes.DuplicateTaxCode, $"Tax code belongs to supplier {owner}.", taxCode);
                }
            }

            string? Optional(string? value, int max, string column)
            {
                var text = TextNormalizer.NullIfBlank(value);
                if (text?.Length > max) Error(column, ImportErrorCodes.TooLong, $"Max {max} characters.", text);
                return text;
            }
            var address = Optional(row.Address, Supplier.AddressMaxLength, SupplierImportColumns.Address);
            var contact = Optional(row.ContactName, Supplier.ContactNameMaxLength, SupplierImportColumns.ContactName);
            var terms = Optional(row.PaymentTerms, Supplier.PaymentTermsMaxLength, SupplierImportColumns.PaymentTerms);

            var phone = TextNormalizer.NullIfBlank(row.Phone);
            if (phone is not null && (phone.Length > Supplier.PhoneMaxLength || !EmployeeRules.PhonePattern().IsMatch(phone)))
                Error(SupplierImportColumns.Phone, ImportErrorCodes.InvalidFormat, "Phone is not valid.", phone);

            var email = TextNormalizer.NullIfBlank(row.Email);
            if (email is not null && (email.Length > Supplier.EmailMaxLength || !EmployeeRules.IsValidEmail(email)))
                Error(SupplierImportColumns.Email, ImportErrorCodes.InvalidFormat, "Email is not valid.", email);

            int? days = null;
            if (TextNormalizer.NullIfBlank(row.PaymentTermDays) is { } daysText)
            {
                if (ImportParsing.TryParseDecimal(daysText, out var parsed) && parsed == decimal.Truncate(parsed)
                    && parsed >= 0 && parsed <= Supplier.MaxPaymentTermDays)
                    days = (int)parsed;
                else
                    Error(SupplierImportColumns.PaymentTermDays, ImportErrorCodes.InvalidNumber, $"Whole number 0–{Supplier.MaxPaymentTermDays}.", daysText);
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }
            valid.Add(new ValidatedSupplierRow(row.RowNumber, code, name, taxCode, address, contact, phone, email, days, terms,
                IsUpdate: existingCodes.Contains(code)));
        }

        return (valid, errors);
    }
}

public interface ISupplierImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);
    Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default);
}

public sealed class SupplierImportService(IAppDbContext db, IExcelImportFile excelFile) : ISupplierImportService
{
    private static readonly string[] Guide =
    [
        "Hướng dẫn nhập danh mục nhà cung cấp",
        "",
        "• Nhập dữ liệu ở sheet \"Nhà cung cấp\", bắt đầu từ dòng 2. Không đổi thứ tự hoặc tên cột.",
        "• Cột có dấu * là bắt buộc. Mã NCC chỉ gồm chữ không dấu, số và . _ -",
        "• Mã số thuế: 10 chữ số, hoặc 10 chữ số-3 chữ số với chi nhánh. Mỗi MST chỉ thuộc một nhà cung cấp.",
        "• Số ngày công nợ: số nguyên từ 0 đến 365. Để trống = 0.",
        "• Nếu có bất kỳ dòng nào lỗi, toàn bộ file sẽ không được lưu.",
        "• Khi bật \"Cập nhật nếu trùng mã\": các ô không bắt buộc để trống sẽ giữ nguyên giá trị hiện tại.",
    ];

    public Task<byte[]> CreateTemplateAsync(CancellationToken ct = default) =>
        Task.FromResult(excelFile.CreateTemplate(new ExcelTemplate("Nhà cung cấp", SupplierImportColumns.All, [], Guide)));

    public async Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default)
    {
        var rows = excelFile.ReadRows(content, SupplierImportColumns.All).Select(SupplierImportRow.From).ToList();
        if (rows.Count == 0) throw FileError("The file has no data rows.");
        if (rows.Count > ImportLimits.MaxRows) throw FileError($"The file has {rows.Count} rows; the maximum is {ImportLimits.MaxRows}.");

        var codes = rows.Select(r => r.Code?.Trim()).OfType<string>().Where(c => c.Length > 0).Select(Supplier.NormalizeCode).Distinct().ToList();
        var taxCodes = rows.Select(r => r.TaxCode?.Trim()).OfType<string>().Where(c => c.Length > 0).Distinct().ToList();
        var existing = await db.Suppliers.Where(s => codes.Contains(s.Code)).ToDictionaryAsync(s => s.Code, ct);
        var taxCodeOwners = await db.Suppliers.AsNoTracking().Where(s => s.TaxCode != null && taxCodes.Contains(s.TaxCode))
            .ToDictionaryAsync(s => s.TaxCode!, s => s.Code, ct);

        var (validRows, errors) = new SupplierImportValidator(existing.Keys.ToHashSet(), taxCodeOwners, updateExisting).Validate(rows);
        if (errors.Count > 0)
            return new ImportResult(false, rows.Count, 0, 0, errors);

        foreach (var row in validRows)
        {
            if (row.IsUpdate)
            {
                var s = existing[row.Code];
                s.Name = row.Name;
                s.TaxCode = row.TaxCode ?? s.TaxCode;
                s.Address = row.Address ?? s.Address;
                s.ContactName = row.ContactName ?? s.ContactName;
                s.Phone = row.Phone ?? s.Phone;
                s.Email = row.Email ?? s.Email;
                s.PaymentTermDays = row.PaymentTermDays ?? s.PaymentTermDays;
                s.PaymentTerms = row.PaymentTerms ?? s.PaymentTerms;
            }
            else
            {
                db.Suppliers.Add(new Supplier
                {
                    Code = row.Code,
                    Name = row.Name,
                    TaxCode = row.TaxCode,
                    Address = row.Address,
                    ContactName = row.ContactName,
                    Phone = row.Phone,
                    Email = row.Email,
                    PaymentTermDays = row.PaymentTermDays ?? 0,
                    PaymentTerms = row.PaymentTerms,
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var updated = validRows.Count(r => r.IsUpdate);
        return new ImportResult(true, rows.Count, validRows.Count - updated, updated, []);
    }

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
