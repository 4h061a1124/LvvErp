using LvvErp.Application.Common.Import;
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
    private readonly LookupMatcher _departments;
    private readonly LookupMatcher _positions;
    private readonly IReadOnlySet<string> _existingCodes;
    private readonly bool _updateExisting;

    public EmployeeImportValidator(
        IEnumerable<LookupDto> departments,
        IEnumerable<LookupDto> positions,
        IReadOnlySet<string> existingCodes,
        bool updateExisting)
    {
        _departments = new LookupMatcher(departments);
        _positions = new LookupMatcher(positions);
        _existingCodes = existingCodes;
        _updateExisting = updateExisting;
    }

    public (IReadOnlyList<ValidatedImportRow> Rows, IReadOnlyList<ImportError> Errors) Validate(
        IReadOnlyList<EmployeeImportRow> rows)
    {
        var valid = new List<ValidatedImportRow>();
        var errors = new List<ImportError>();
        var firstRowByCode = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var rowErrors = new List<ImportError>();
            void Error(string column, string code, string message, string? value) =>
                rowErrors.Add(new ImportError(row.RowNumber, column, code, message, value));

            // Code
            var code = row.Code?.Trim() ?? string.Empty;
            if (code.Length == 0)
                Error(EmployeeImportColumns.Code, ImportErrorCodes.Required, "Employee code is required.", null);
            else if (code.Length > Employee.CodeMaxLength)
                Error(EmployeeImportColumns.Code, ImportErrorCodes.TooLong, $"Max {Employee.CodeMaxLength} characters.", code);
            else if (!EmployeeRules.CodePattern().IsMatch(code))
                Error(EmployeeImportColumns.Code, ImportErrorCodes.InvalidFormat, "Only letters, digits, '.', '_' and '-'.", code);
            else
            {
                code = Employee.NormalizeCode(code);
                if (firstRowByCode.TryGetValue(code, out var firstRow))
                    Error(EmployeeImportColumns.Code, ImportErrorCodes.DuplicateInFile, $"Same code as row {firstRow}.", code);
                else
                    firstRowByCode[code] = row.RowNumber;

                if (!_updateExisting && _existingCodes.Contains(code))
                    Error(EmployeeImportColumns.Code, ImportErrorCodes.AlreadyExists, "Employee code already exists.", code);
            }

            // Full name
            var fullName = row.FullName?.Trim() ?? string.Empty;
            if (fullName.Length == 0)
                Error(EmployeeImportColumns.FullName, ImportErrorCodes.Required, "Full name is required.", null);
            else if (fullName.Length > Employee.FullNameMaxLength)
                Error(EmployeeImportColumns.FullName, ImportErrorCodes.TooLong, $"Max {Employee.FullNameMaxLength} characters.", fullName);

            // Lookups
            var departmentId = Resolve(_departments, row.Department, EmployeeImportColumns.Department, ImportErrorCodes.UnknownDepartment, Error);
            var positionId = Resolve(_positions, row.Position, EmployeeImportColumns.Position, ImportErrorCodes.UnknownPosition, Error);

            // Optional fields
            var email = TextNormalizer.NullIfBlank(row.Email);
            if (email is not null)
            {
                if (email.Length > Employee.EmailMaxLength)
                    Error(EmployeeImportColumns.Email, ImportErrorCodes.TooLong, $"Max {Employee.EmailMaxLength} characters.", email);
                else if (!EmployeeRules.IsValidEmail(email))
                    Error(EmployeeImportColumns.Email, ImportErrorCodes.InvalidFormat, "Email is not valid.", email);
            }

            var phone = TextNormalizer.NullIfBlank(row.Phone);
            if (phone is not null)
            {
                if (phone.Length > Employee.PhoneMaxLength)
                    Error(EmployeeImportColumns.Phone, ImportErrorCodes.TooLong, $"Max {Employee.PhoneMaxLength} characters.", phone);
                else if (!EmployeeRules.PhonePattern().IsMatch(phone))
                    Error(EmployeeImportColumns.Phone, ImportErrorCodes.InvalidFormat, "Phone is not valid.", phone);
            }

            EmployeeStatus? status = null;
            if (TextNormalizer.NullIfBlank(row.Status) is { } statusText)
            {
                if (EmployeeRules.TryParseStatus(statusText, out var parsed))
                    status = parsed;
                else
                    Error(EmployeeImportColumns.Status, ImportErrorCodes.InvalidStatus, "Unknown status.", statusText);
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
        LookupMatcher lookup, string? text, string column, string unknownCode,
        Action<string, string, string, string?> error)
    {
        var key = TextNormalizer.ToMatchKey(text);
        if (key.Length == 0)
        {
            error(column, ImportErrorCodes.Required, $"{column} is required.", null);
            return null;
        }
        if (lookup.TryMatch(text, out var id)) return id;

        error(column, unknownCode, $"{column} not found (match by code or name).", text!.Trim());
        return null;
    }
}
