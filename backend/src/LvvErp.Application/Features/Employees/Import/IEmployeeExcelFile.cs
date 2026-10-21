using LvvErp.Application.Common.Models;

namespace LvvErp.Application.Features.Employees.Import;

/// <summary>Reads and writes the employee import workbook (implemented with ClosedXML in Infrastructure).</summary>
public interface IEmployeeExcelFile
{
    byte[] CreateTemplate(IReadOnlyList<LookupDto> departments, IReadOnlyList<LookupDto> positions);

    /// <summary>Returns non-empty data rows. Throws a ValidationException if the file is not a valid template.</summary>
    IReadOnlyList<EmployeeImportRow> ReadRows(Stream content);
}
