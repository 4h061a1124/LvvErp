using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Application.Features.Employees.Import;

/// <summary>
/// Pure validation of import rows (no database access) so every rule is unit-testable.
/// Department and position cells match by code or by name, ignoring case and diacritics.
/// </summary>
public sealed class EmployeeImportValidator
{
    private readonly Dictionary<string, Guid> _departments;
    private readonly Dictionary<string, Guid> _positions;
    private readonly IReadOnlySet<string> _existingCodes;
    private readonly bool _updateExisting;

    public EmployeeImportValidator(
        IEnumerable<LookupDto> departments,
        IEnumerable<LookupDto> positions,
        IReadOnlySet<string> existingCodes,
        bool updateExisting)
    {
        _departments = BuildLookup(departments);
        _positions = BuildLookup(positions);
        _existingCodes = existingCodes;
        _updateExisting = updateExisting;
    }

    public (IReadOnlyList<ValidatedImportRow> Rows, IReadOnlyList<EmployeeImportError> Errors) Validate(
        IReadOnlyList<EmployeeImportRow> rows)
    {
        var valid = new List<ValidatedImportRow>();
        var errors = new List<EmployeeImportError>();
        var firstRowByCode = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var rowErrors = new List<EmployeeImportError>();
            void Error(string column, string code, string message, string? value) =>
                rowErrors.Add(new EmployeeImportError(row.RowNumber, column, code, message, value));

            // Code
            var code = row.Code?.Trim() ?? string.Empty;
            if (code.Length == 0)
                Error(ImportColumns.Code, ImportErrorCodes.Required, "Employee code is required.", null);
            else if (code.Length > Employee.CodeMaxLength)
                Error(ImportColumns.Code, ImportErrorCodes.TooLong, $"Max {Employee.CodeMaxLength} characters.", code);
            else if (!EmployeeRules.CodePattern().IsMatch(code))
                Error(ImportColumns.Code, ImportErrorCodes.InvalidFormat, "Only letters, digits, '.', '_' and '-'.", code);
            else
            {
                code = Employee.NormalizeCode(code);
                if (firstRowByCode.TryGetValue(code, out var firstRow))
                    Error(ImportColumns.Code, ImportErrorCodes.DuplicateInFile, $"Same code as row {firstRow}.", code);
                else
                    firstRowByCode[code] = row.RowNumber;

                if (!_updateExisting && _existingCodes.Contains(code))
                    Error(ImportColumns.Code, ImportErrorCodes.AlreadyExists, "Employee code already exists.", code);
            }

            // Full name
            var fullName = row.FullName?.Trim() ?? string.Empty;
            if (fullName.Length == 0)
                Error(ImportColumns.FullName, ImportErrorCodes.Required, "Full name is required.", null);
            else if (fullName.Length > Employee.FullNameMaxLength)
                Error(ImportColumns.FullName, ImportErrorCodes.TooLong, $"Max {Employee.FullNameMaxLength} characters.", fullName);

            // Lookups
            var departmentId = Resolve(_departments, row.Department, ImportColumns.Department, ImportErrorCodes.UnknownDepartment, Error);
            var positionId = Resolve(_positions, row.Position, ImportColumns.Position, ImportErrorCodes.UnknownPosition, Error);

            // Optional fields
            var email = TextNormalizer.NullIfBlank(row.Email);
            if (email is not null)
            {
                if (email.Length > Employee.EmailMaxLength)
                    Error(ImportColumns.Email, ImportErrorCodes.TooLong, $"Max {Employee.EmailMaxLength} characters.", email);
                else if (!EmployeeRules.IsValidEmail(email))
                    Error(ImportColumns.Email, ImportErrorCodes.InvalidFormat, "Email is not valid.", email);
            }

            var phone = TextNormalizer.NullIfBlank(row.Phone);
            if (phone is not null)
            {
                if (phone.Length > Employee.PhoneMaxLength)
                    Error(ImportColumns.Phone, ImportErrorCodes.TooLong, $"Max {Employee.PhoneMaxLength} characters.", phone);
                else if (!EmployeeRules.PhonePattern().IsMatch(phone))
                    Error(ImportColumns.Phone, ImportErrorCodes.InvalidFormat, "Phone is not valid.", phone);
            }

            EmployeeStatus? status = null;
            if (TextNormalizer.NullIfBlank(row.Status) is { } statusText)
            {
                if (EmployeeRules.TryParseStatus(statusText, out var parsed))
                    status = parsed;
                else
                    Error(ImportColumns.Status, ImportErrorCodes.InvalidStatus, "Unknown status.", statusText);
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            valid.Add(new ValidatedImportRow(
                row.RowNumber, code, fullName, departmentId!.Value, positionId!.Value, email, phone, status,
                IsUpdate: _existingCodes.Contains(code)));
        }

        return (valid, errors);
    }

    private static Guid? Resolve(
        Dictionary<string, Guid> lookup, string? text, string column, string unknownCode,
        Action<string, string, string, string?> error)
    {
        var key = TextNormalizer.ToMatchKey(text);
        if (key.Length == 0)
        {
            error(column, ImportErrorCodes.Required, $"{column} is required.", null);
            return null;
        }
        if (lookup.TryGetValue(key, out var id)) return id;

        error(column, unknownCode, $"{column} not found (match by code or name).", text!.Trim());
        return null;
    }

    private static Dictionary<string, Guid> BuildLookup(IEnumerable<LookupDto> items)
    {
        var lookup = new Dictionary<string, Guid>();
        foreach (var item in items)
        {
            lookup.TryAdd(TextNormalizer.ToMatchKey(item.Code), item.Id);
            lookup.TryAdd(TextNormalizer.ToMatchKey(item.Name), item.Id);
        }
        return lookup;
    }
}
