using System.Globalization;
using ClosedXML.Excel;
using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Text;

namespace LvvErp.Infrastructure.Import;

/// <summary>
/// Template layout: data sheet (headers in row 1), "Danh mục" with reference lists used as dropdowns,
/// "Hướng dẫn" with instructions. Reading checks the header row so a wrong file is rejected instead
/// of half-imported.
/// </summary>
public sealed class ExcelImportFile : IExcelImportFile
{
    private const string ListsSheetName = "Danh mục";
    private const string GuideSheetName = "Hướng dẫn";
    private const int FormattedRows = ImportLimits.MaxRows;

    public byte[] CreateTemplate(ExcelTemplate template)
    {
        using var workbook = new XLWorkbook();

        var data = workbook.AddWorksheet(template.DataSheetName);
        for (var i = 0; i < template.Columns.Count; i++)
        {
            var column = template.Columns[i];
            var header = data.Cell(1, i + 1);
            header.Value = column.Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            data.Column(i + 1).Width = column.Width;
            if (column.IsText)
                data.Range(2, i + 1, FormattedRows + 1, i + 1).Style.NumberFormat.Format = "@";
        }
        data.SheetView.FreezeRows(1);

        if (template.Lists.Count > 0)
        {
            var lists = workbook.AddWorksheet(ListsSheetName);
            for (var l = 0; l < template.Lists.Count; l++)
            {
                var list = template.Lists[l];
                lists.Cell(1, l + 1).Value = list.Header;
                lists.Cell(1, l + 1).Style.Font.Bold = true;
                for (var v = 0; v < list.Values.Count; v++) lists.Cell(v + 2, l + 1).Value = list.Values[v];
                lists.Column(l + 1).Width = 30;
            }

            for (var i = 0; i < template.Columns.Count; i++)
            {
                var listIndex = template.Lists.ToList().FindIndex(l => l.Name == template.Columns[i].ListName);
                if (listIndex < 0 || template.Lists[listIndex].Values.Count == 0) continue;

                var validation = data.Range(2, i + 1, FormattedRows + 1, i + 1).CreateDataValidation();
                validation.List(lists.Range(2, listIndex + 1, template.Lists[listIndex].Values.Count + 1, listIndex + 1), true);
                validation.IgnoreBlanks = true;
                // Warning (not Stop) so users may still type a code instead of picking a name.
                validation.ErrorStyle = XLErrorStyle.Warning;
            }
        }

        if (template.Guide.Count > 0)
        {
            var guide = workbook.AddWorksheet(GuideSheetName);
            for (var i = 0; i < template.Guide.Count; i++) guide.Cell(i + 1, 1).Value = template.Guide[i];
            guide.Cell(1, 1).Style.Font.Bold = true;
            guide.Column(1).Width = 110;
        }

        data.SetTabActive();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public IReadOnlyList<ExcelRow> ReadRows(Stream content, IReadOnlyList<ExcelColumn> columns)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception)
        {
            throw FileError("The file is not a valid .xlsx workbook.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.First();
            for (var i = 0; i < columns.Count; i++)
            {
                if (HeaderKey(sheet.Cell(1, i + 1).GetFormattedString()) != HeaderKey(columns[i].Header))
                    throw FileError($"Column {i + 1} header should be '{columns[i].Header.TrimEnd(' ', '*')}'. Please use the template.");
            }

            var rows = new List<ExcelRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var cells = new Dictionary<string, string?>();
                for (var c = 0; c < columns.Count; c++)
                    cells[columns[c].Key] = TextNormalizer.NullIfBlank(CellText(sheet.Cell(r, c + 1)));
                if (cells.Values.All(v => v is null)) continue;
                rows.Add(new ExcelRow(r, cells));
            }
            return rows;
        }
    }

    /// <summary>Culture-independent text: dates as yyyy-MM-dd, numbers with "." decimals.</summary>
    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => DateOnly.FromDateTime(cell.GetDateTime()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => ((decimal)cell.GetDouble()).ToString("0.######", CultureInfo.InvariantCulture),
        _ => cell.GetFormattedString(),
    };

    private static string HeaderKey(string header) => TextNormalizer.ToMatchKey(header.Replace("*", ""));

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
