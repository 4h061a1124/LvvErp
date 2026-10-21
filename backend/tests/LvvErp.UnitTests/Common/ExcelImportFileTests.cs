using ClosedXML.Excel;
using FluentValidation;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Infrastructure.Import;

namespace LvvErp.UnitTests.Common;

public sealed class ExcelImportFileTests
{
    private readonly ExcelImportFile _excel = new();

    private static readonly ExcelColumn[] Columns =
    [
        new("Code", "Mã *", IsText: true),
        new("Name", "Tên *"),
        new("Unit", "ĐVT", ListName: "Unit"),
        new("Quantity", "Số lượng"),
        new("Expiry", "Hạn dùng"),
    ];

    private byte[] Template() => _excel.CreateTemplate(new ExcelTemplate(
        "Dữ liệu", Columns, [new ExcelList("Unit", "Đơn vị tính", ["Cái", "Kg"])], ["Hướng dẫn", "• dòng 2"]));

    private static byte[] Fill(byte[] template, Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook(new MemoryStream(template));
        fill(workbook.Worksheet(1));
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    [Fact]
    public void Template_has_headers_lists_dropdowns_and_guide()
    {
        using var workbook = new XLWorkbook(new MemoryStream(Template()));

        var data = workbook.Worksheet(1);
        Assert.Equal("Dữ liệu", data.Name);
        Assert.Equal("Mã *", data.Cell(1, 1).GetString());
        Assert.Equal("Kg", workbook.Worksheet("Danh mục").Cell(3, 1).GetString());
        Assert.Equal("Hướng dẫn", workbook.Worksheet("Hướng dẫn").Cell(1, 1).GetString());
        Assert.NotEmpty(data.DataValidations);
    }

    [Fact]
    public void Employee_template_keeps_its_layout()
    {
        using var workbook = new XLWorkbook(new MemoryStream(_excel.CreateTemplate(
            new ExcelTemplate("Nhân viên", EmployeeImportColumns.All, [], []))));

        Assert.Equal(["Mã NV *", "Họ tên *", "Bộ phận *", "Chức danh *", "Email", "Số điện thoại", "Trạng thái"],
            Enumerable.Range(1, 7).Select(c => workbook.Worksheet(1).Cell(1, c).GetString()));
    }

    [Fact]
    public void Template_contains_no_data_rows() =>
        Assert.Empty(_excel.ReadRows(new MemoryStream(Template()), Columns));

    [Fact]
    public void Reads_rows_by_key_keeping_leading_zeros_and_skipping_blank_rows()
    {
        var file = Fill(Template(), sheet =>
        {
            sheet.Cell(2, 1).Value = "00123";
            sheet.Cell(2, 2).Value = "Bu lông";
            sheet.Cell(2, 3).Value = "Cái";
            sheet.Cell(4, 1).Value = "NV002";
            sheet.Cell(4, 2).Value = "  Sơn  ";
        });

        var rows = _excel.ReadRows(new MemoryStream(file), Columns);

        Assert.Equal(2, rows.Count);
        Assert.Equal((2, "00123", "Cái"), (rows[0].RowNumber, rows[0]["Code"], rows[0]["Unit"]));
        Assert.Null(rows[0]["Quantity"]);
        Assert.Equal((4, "Sơn"), (rows[1].RowNumber, rows[1]["Name"]));
    }

    [Fact]
    public void Number_and_date_cells_are_read_independently_of_culture()
    {
        var file = Fill(Template(), sheet =>
        {
            sheet.Cell(2, 1).Value = "A";
            sheet.Cell(2, 4).Value = 12.5;
            sheet.Cell(2, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(2, 5).Value = new DateTime(2027, 3, 31);
            sheet.Cell(2, 5).Style.DateFormat.Format = "dd/MM/yyyy";
        });

        var row = Assert.Single(_excel.ReadRows(new MemoryStream(file), Columns));

        Assert.Equal(("12.5", "2027-03-31"), (row["Quantity"], row["Expiry"]));
    }

    [Fact]
    public void Wrong_header_is_rejected()
    {
        var file = Fill(Template(), sheet => sheet.Cell(1, 2).Value = "Mô tả");

        var ex = Assert.Throws<ValidationException>(() => _excel.ReadRows(new MemoryStream(file), Columns));
        Assert.Contains("Tên", ex.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Header_match_ignores_case_accents_and_asterisk()
    {
        var file = Fill(Template(), sheet => sheet.Cell(1, 1).Value = "ma");
        Assert.Empty(_excel.ReadRows(new MemoryStream(file), Columns));
    }

    [Fact]
    public void Non_excel_content_is_rejected() =>
        Assert.Throws<ValidationException>(() => _excel.ReadRows(new MemoryStream("a,b,c\n1,2,3"u8.ToArray()), Columns));
}

public sealed class ImportParsingTests
{
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData(" 1 000 ", 1000)]
    [InlineData("0.125", 0.125)]
    public void Parses_numbers_with_dot_or_comma(string text, double expected)
    {
        Assert.True(ImportParsing.TryParseDecimal(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("1.234,5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_ambiguous_or_non_numbers(string? text) =>
        Assert.False(ImportParsing.TryParseDecimal(text, out _));

    [Theory]
    [InlineData("2027-03-31")]
    [InlineData("31/03/2027")]
    [InlineData("31/3/2027")]
    [InlineData("31-03-2027")]
    public void Parses_iso_and_day_first_dates(string text)
    {
        Assert.True(ImportParsing.TryParseDate(text, out var date));
        Assert.Equal(new DateOnly(2027, 3, 31), date);
    }

    [Theory]
    [InlineData("03/31/2027")] // month-first is not accepted
    [InlineData("hôm nay")]
    public void Rejects_other_dates(string text) => Assert.False(ImportParsing.TryParseDate(text, out _));

    [Theory]
    [InlineData("Có", true)]
    [InlineData("co", true)]
    [InlineData("x", true)]
    [InlineData("Không", false)]
    [InlineData("0", false)]
    public void Parses_yes_no(string text, bool expected)
    {
        Assert.True(ImportParsing.TryParseBool(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Rejects_unknown_yes_no() => Assert.False(ImportParsing.TryParseBool("có lẽ", out _));
}
