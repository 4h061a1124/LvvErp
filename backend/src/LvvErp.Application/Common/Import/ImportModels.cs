namespace LvvErp.Application.Common.Import;

/// <summary>
/// One problem in an imported file. <see cref="Column"/> is a field key (Code, Name, ...) and
/// <see cref="ErrorCode"/> one of <see cref="ImportErrorCodes"/>, so the frontend can show a
/// translated message; <see cref="Message"/> is an English fallback.
/// </summary>
public sealed record ImportError(int Row, string Column, string ErrorCode, string Message, string? Value = null);

/// <summary>Result of an all-or-nothing catalog import.</summary>
public sealed record ImportResult(bool Succeeded, int TotalRows, int Created, int Updated, IReadOnlyList<ImportError> Errors);

public static class ImportErrorCodes
{
    public const string Required = "Required";
    public const string TooLong = "TooLong";
    public const string InvalidFormat = "InvalidFormat";
    public const string InvalidNumber = "InvalidNumber";
    public const string InvalidDate = "InvalidDate";
    public const string DuplicateInFile = "DuplicateInFile";
    public const string AlreadyExists = "AlreadyExists";
    public const string DuplicateTaxCode = "DuplicateTaxCode";

    // Lookups that didn't match by code or name
    public const string UnknownDepartment = "UnknownDepartment";
    public const string UnknownPosition = "UnknownPosition";
    public const string UnknownUnit = "UnknownUnit";
    public const string UnknownGroup = "UnknownGroup";
    public const string UnknownMaterial = "UnknownMaterial";
    public const string UnknownWarehouse = "UnknownWarehouse";
    public const string UnknownLocation = "UnknownLocation";

    public const string InvalidStatus = "InvalidStatus";
    public const string LotRequired = "LotRequired";
    public const string LotNotAllowed = "LotNotAllowed";
    /// <summary>Lot dates differ from another row or from the existing lot.</summary>
    public const string LotDatesMismatch = "LotDatesMismatch";
    public const string TrackLotsLocked = "TrackLotsLocked";
}

public static class ImportLimits
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 10 * 1024 * 1024;
}

/// <summary>A column of an import template. "*" in <see cref="Header"/> marks it required for users.</summary>
/// <param name="IsText">Format the column as text so codes and phone numbers keep leading zeros.</param>
/// <param name="ListName">Name of an <see cref="ExcelList"/> offered as a dropdown (typing other values is still allowed).</param>
public sealed record ExcelColumn(string Key, string Header, double Width = 18, bool IsText = false, string? ListName = null);

/// <summary>Reference values written to the "Danh mục" sheet.</summary>
public sealed record ExcelList(string Name, string Header, IReadOnlyList<string> Values);

public sealed record ExcelTemplate(
    string DataSheetName,
    IReadOnlyList<ExcelColumn> Columns,
    IReadOnlyList<ExcelList> Lists,
    IReadOnlyList<string> Guide);

/// <summary>A non-empty data row: cell text by column key. Dates come as yyyy-MM-dd, numbers in invariant culture.</summary>
public sealed record ExcelRow(int RowNumber, IReadOnlyDictionary<string, string?> Cells)
{
    public string? this[string key] => Cells.GetValueOrDefault(key);
}
