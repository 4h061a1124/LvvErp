namespace LvvErp.Application.Common.Import;

/// <summary>Builds import templates and reads filled-in files (ClosedXML in Infrastructure).</summary>
public interface IExcelImportFile
{
    byte[] CreateTemplate(ExcelTemplate template);

    /// <summary>
    /// Reads the first sheet. Columns are read by position after checking that the header row matches
    /// <paramref name="columns"/> (ignoring case, accents and "*"). Throws a ValidationException
    /// (field "file") when the file is not a workbook or the header doesn't match.
    /// </summary>
    IReadOnlyList<ExcelRow> ReadRows(Stream content, IReadOnlyList<ExcelColumn> columns);
}
