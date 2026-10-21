using ClosedXML.Excel;
using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;

namespace LvvErp.Infrastructure.Features.Employees;

/// <summary>
/// Import workbook layout. Sheet 1 holds data with headers in row 1; columns are read by position,
/// and the header row is checked so a wrong file is rejected instead of half-imported.
/// </summary>
public sealed class EmployeeExcelFile : IEmployeeExcelFile
{
    private const string DataSheetName = "Nhân viên";
    private const string ListsSheetName = "Danh mục";
    private const string GuideSheetName = "Hướng dẫn";
    private const int ValidationRows = 5000;

    // Order = column order. Header text is Vietnamese (end users); "*" marks required columns.
    private static readonly (string Key, string Header, double Width)[] Columns =
    [
        (ImportColumns.Code, "Mã NV *", 14),
        (ImportColumns.FullName, "Họ tên *", 30),
        (ImportColumns.Department, "Bộ phận *", 24),
        (ImportColumns.Position, "Chức danh *", 22),
        (ImportColumns.Email, "Email", 28),
        (ImportColumns.Phone, "Số điện thoại", 16),
        (ImportColumns.Status, "Trạng thái", 14),
    ];

    public byte[] CreateTemplate(IReadOnlyList<LookupDto> departments, IReadOnlyList<LookupDto> positions)
    {
        using var workbook = new XLWorkbook();

        var data = workbook.AddWorksheet(DataSheetName);
        for (var i = 0; i < Columns.Length; i++)
        {
            var cell = data.Cell(1, i + 1);
            cell.Value = Columns[i].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            data.Column(i + 1).Width = Columns[i].Width;
        }
        data.SheetView.FreezeRows(1);
        // Text format keeps leading zeros in codes and phone numbers.
        data.Range(2, 1, ValidationRows + 1, 1).Style.NumberFormat.Format = "@";
        data.Range(2, 6, ValidationRows + 1, 6).Style.NumberFormat.Format = "@";

        var lists = workbook.AddWorksheet(ListsSheetName);
        WriteList(lists, 1, "Bộ phận", departments.Select(d => d.Name).ToList());
        WriteList(lists, 2, "Chức danh", positions.Select(p => p.Name).ToList());
        WriteList(lists, 3, "Trạng thái", EmployeeRules.StatusLabelsVi.Values.ToList());

        AddDropdown(data, column: 3, lists, listColumn: 1, departments.Count);
        AddDropdown(data, column: 4, lists, listColumn: 2, positions.Count);
        AddDropdown(data, column: 7, lists, listColumn: 3, EmployeeRules.StatusLabelsVi.Count);

        var guide = workbook.AddWorksheet(GuideSheetName);
        string[] notes =
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
        for (var i = 0; i < notes.Length; i++) guide.Cell(i + 1, 1).Value = notes[i];
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Column(1).Width = 110;

        data.SetTabActive();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public IReadOnlyList<EmployeeImportRow> ReadRows(Stream content)
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
            EnsureHeaderMatches(sheet);

            var rows = new List<EmployeeImportRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var values = Enumerable.Range(1, Columns.Length)
                    .Select(c => TextNormalizer.NullIfBlank(sheet.Cell(r, c).GetFormattedString()))
                    .ToArray();
                if (values.All(v => v is null)) continue;

                rows.Add(new EmployeeImportRow(r, values[0], values[1], values[2], values[3], values[4], values[5], values[6]));
            }
            return rows;
        }
    }

    private static void EnsureHeaderMatches(IXLWorksheet sheet)
    {
        for (var i = 0; i < Columns.Length; i++)
        {
            var actual = HeaderKey(sheet.Cell(1, i + 1).GetFormattedString());
            if (actual != HeaderKey(Columns[i].Header))
                throw FileError(
                    $"Column {i + 1} header should be '{Columns[i].Header.TrimEnd(' ', '*')}'. Please use the template.");
        }
    }

    private static string HeaderKey(string header) => TextNormalizer.ToMatchKey(header.Replace("*", ""));

    private static void WriteList(IXLWorksheet sheet, int column, string header, IReadOnlyList<string> values)
    {
        sheet.Cell(1, column).Value = header;
        sheet.Cell(1, column).Style.Font.Bold = true;
        for (var i = 0; i < values.Count; i++) sheet.Cell(i + 2, column).Value = values[i];
        sheet.Column(column).Width = 28;
    }

    private static void AddDropdown(IXLWorksheet data, int column, IXLWorksheet lists, int listColumn, int count)
    {
        if (count == 0) return;
        var validation = data.Range(2, column, ValidationRows + 1, column).CreateDataValidation();
        validation.List(lists.Range(2, listColumn, count + 1, listColumn), true);
        validation.IgnoreBlanks = true;
        // Warning (not Stop) so users may still type a code instead of picking a name.
        validation.ErrorStyle = XLErrorStyle.Warning;
    }

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
