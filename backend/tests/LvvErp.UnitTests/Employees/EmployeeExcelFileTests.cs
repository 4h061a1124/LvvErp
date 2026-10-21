using ClosedXML.Excel;
using FluentValidation;
using LvvErp.Application.Common.Models;
using LvvErp.Infrastructure.Features.Employees;

namespace LvvErp.UnitTests.Employees;

public sealed class EmployeeExcelFileTests
{
    private readonly EmployeeExcelFile _excel = new();

    private byte[] Template() => _excel.CreateTemplate(
        [new LookupDto(Guid.NewGuid(), "SX", "Sản xuất"), new LookupDto(Guid.NewGuid(), "KHO", "Kho")],
        [new LookupDto(Guid.NewGuid(), "CN", "Công nhân")]);

    private static byte[] Fill(byte[] template, Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook(new MemoryStream(template));
        fill(workbook.Worksheet(1));
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    [Fact]
    public void Template_has_data_sheet_with_headers_and_lookup_lists()
    {
        using var workbook = new XLWorkbook(new MemoryStream(Template()));

        var data = workbook.Worksheet(1);
        Assert.Equal("Nhân viên", data.Name);
        Assert.Equal("Mã NV *", data.Cell(1, 1).GetString());
        Assert.Equal("Trạng thái", data.Cell(1, 7).GetString());

        var lists = workbook.Worksheet("Danh mục");
        Assert.Equal("Sản xuất", lists.Cell(2, 1).GetString());
        Assert.Equal("Công nhân", lists.Cell(2, 2).GetString());
        Assert.Equal("Đang làm", lists.Cell(2, 3).GetString());
        Assert.NotEmpty(data.DataValidations);
    }

    [Fact]
    public void Template_contains_no_data_rows() =>
        Assert.Empty(_excel.ReadRows(new MemoryStream(Template())));

    [Fact]
    public void Reads_filled_rows_keeping_leading_zeros_and_skipping_blank_rows()
    {
        var file = Fill(Template(), sheet =>
        {
            sheet.Cell(2, 1).Value = "00123";
            sheet.Cell(2, 2).Value = "Nguyễn Văn A";
            sheet.Cell(2, 3).Value = "Sản xuất";
            sheet.Cell(2, 4).Value = "CN";
            sheet.Cell(2, 6).Value = "0901234567";
            // row 3 left blank on purpose
            sheet.Cell(4, 1).Value = "NV002";
            sheet.Cell(4, 2).Value = "Trần Thị B";
            sheet.Cell(4, 7).Value = "Tạm nghỉ";
        });

        var rows = _excel.ReadRows(new MemoryStream(file));

        Assert.Equal(2, rows.Count);
        Assert.Equal((2, "00123", "Sản xuất", "0901234567"), (rows[0].RowNumber, rows[0].Code, rows[0].Department, rows[0].Phone));
        Assert.Null(rows[0].Email);
        Assert.Equal((4, "NV002", "Tạm nghỉ"), (rows[1].RowNumber, rows[1].Code, rows[1].Status));
    }

    [Fact]
    public void Wrong_header_is_rejected()
    {
        var file = Fill(Template(), sheet => sheet.Cell(1, 2).Value = "Tên");

        var ex = Assert.Throws<ValidationException>(() => _excel.ReadRows(new MemoryStream(file)));
        Assert.Contains("Họ tên", ex.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Header_match_ignores_case_accents_and_asterisk()
    {
        var file = Fill(Template(), sheet => sheet.Cell(1, 1).Value = "ma nv");
        Assert.Empty(_excel.ReadRows(new MemoryStream(file)));
    }

    [Fact]
    public void Non_excel_content_is_rejected() =>
        Assert.Throws<ValidationException>(() => _excel.ReadRows(new MemoryStream("a,b,c\n1,2,3"u8.ToArray())));
}
