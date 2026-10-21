using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Text;

namespace LvvErp.UnitTests.Common;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Sản xuất", "san xuat")]
    [InlineData("  SẢN   XUẤT  ", "san xuat")]
    [InlineData("Đã nghỉ", "da nghi")]
    [InlineData("Quản lý chất lượng", "quan ly chat luong")]
    [InlineData("Kỹ thuật viên", "ky thuat vien")]
    [InlineData("QC", "qc")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ToMatchKey_strips_case_diacritics_and_extra_spaces(string? input, string expected) =>
        Assert.Equal(expected, TextNormalizer.ToMatchKey(input));
}

public sealed class ImageFormatTests
{
    [Fact]
    public void Detects_jpeg() => Assert.Equal(".jpg", ImageFormat.DetectExtension([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]));

    [Fact]
    public void Detects_png() =>
        Assert.Equal(".png", ImageFormat.DetectExtension([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));

    [Fact]
    public void Detects_webp() =>
        Assert.Equal(".webp", ImageFormat.DetectExtension("RIFF\0\0\0\0WEBPVP8 "u8));

    [Theory]
    [InlineData("GIF89a")]
    [InlineData("<svg xmlns=")]
    [InlineData("MZ executable")]
    [InlineData("")]
    public void Rejects_other_content(string content) =>
        Assert.Null(ImageFormat.DetectExtension(System.Text.Encoding.ASCII.GetBytes(content)));
}
