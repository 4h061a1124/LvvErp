using LvvErp.Application.Common.Import;
using LvvErp.Application.Features.Purchasing.Settings;
using LvvErp.Application.Features.Purchasing.Suppliers;

namespace LvvErp.UnitTests.Purchasing;

public sealed class SupplierValidatorTests
{
    private readonly SaveSupplierRequestValidator _validator = new();

    private static SaveSupplierRequest Valid() => new(
        "NCC-001", "Công ty TNHH Thép Việt", "0101234567", "KCN Bắc Thăng Long, Hà Nội", "Anh Hùng",
        "0912345678", "ban-hang@thepviet.vn", 30, "Chuyển khoản");

    [Fact]
    public void Valid_request_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("0101234567")]
    [InlineData("0101234567-001")]
    [InlineData(" 0101234567 ")]
    [InlineData(null)]
    [InlineData("")]
    public void Accepts_company_and_branch_tax_codes_or_blank(string? taxCode) =>
        Assert.True(_validator.Validate(Valid() with { TaxCode = taxCode }).IsValid);

    [Theory]
    [InlineData("010123456")]
    [InlineData("0101234567-01")]
    [InlineData("01012345AB")]
    public void Rejects_bad_tax_codes(string taxCode)
    {
        var result = _validator.Validate(Valid() with { TaxCode = taxCode });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveSupplierRequest.TaxCode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Rejects_payment_term_days_out_of_range(int days) =>
        Assert.False(_validator.Validate(Valid() with { PaymentTermDays = days }).IsValid);

    [Fact]
    public void Rejects_bad_email_and_code()
    {
        Assert.False(_validator.Validate(Valid() with { Email = "not-an-email" }).IsValid);
        Assert.False(_validator.Validate(Valid() with { Code = "NCC 001" }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000.5)]
    public void Settings_threshold_must_be_positive_whole_vnd(double threshold) =>
        Assert.False(new SavePurchasingSettingsRequestValidator()
            .Validate(new SavePurchasingSettingsRequest((decimal)threshold, Guid.NewGuid())).IsValid);

    [Fact]
    public void Settings_require_a_department() =>
        Assert.False(new SavePurchasingSettingsRequestValidator()
            .Validate(new SavePurchasingSettingsRequest(50_000_000, null)).IsValid);
}

public sealed class SupplierImportValidatorTests
{
    private static SupplierImportRow Row(int n, string? code, string? name = "NCC", string? taxCode = null, string? days = null) =>
        new(n, code, name, taxCode, null, null, null, null, days, null);

    private static SupplierImportValidator Validator(
        IEnumerable<string>? existing = null, Dictionary<string, string>? taxOwners = null, bool update = false) =>
        new((existing ?? []).ToHashSet(), taxOwners ?? [], update);

    [Fact]
    public void Valid_rows_are_normalized()
    {
        var (rows, errors) = Validator().Validate([Row(2, " ncc-01 ", " Thép Việt ", "0101234567", "30")]);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal("NCC-01", row.Code);
        Assert.Equal("Thép Việt", row.Name);
        Assert.Equal(30, row.PaymentTermDays);
        Assert.False(row.IsUpdate);
    }

    [Fact]
    public void Duplicate_codes_and_tax_codes_in_file_are_reported()
    {
        var (_, errors) = Validator().Validate(
        [
            Row(2, "A", taxCode: "0101234567"),
            Row(3, "a", taxCode: "0101234567"),
        ]);

        Assert.Contains(errors, e => e.Row == 3 && e.Column == SupplierImportColumns.Code && e.ErrorCode == ImportErrorCodes.DuplicateInFile);
        Assert.Contains(errors, e => e.Row == 3 && e.Column == SupplierImportColumns.TaxCode && e.ErrorCode == ImportErrorCodes.DuplicateInFile);
    }

    [Fact]
    public void Existing_code_needs_update_mode()
    {
        Assert.Contains(Validator(["A"]).Validate([Row(2, "A")]).Errors, e => e.ErrorCode == ImportErrorCodes.AlreadyExists);

        var (rows, errors) = Validator(["A"], update: true).Validate([Row(2, "A")]);
        Assert.Empty(errors);
        Assert.True(Assert.Single(rows).IsUpdate);
    }

    [Fact]
    public void Tax_code_owned_by_another_supplier_is_rejected_but_own_is_fine()
    {
        var owners = new Dictionary<string, string> { ["0101234567"] = "A" };

        Assert.Contains(Validator(["A"], owners).Validate([Row(2, "B", taxCode: "0101234567")]).Errors,
            e => e.ErrorCode == ImportErrorCodes.DuplicateTaxCode);
        Assert.Empty(Validator(["A"], owners, update: true).Validate([Row(2, "A", taxCode: "0101234567")]).Errors);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("366")]
    [InlineData("1.5")]
    public void Rejects_bad_payment_term_days(string days) =>
        Assert.Contains(Validator().Validate([Row(2, "A", days: days)]).Errors,
            e => e.Column == SupplierImportColumns.PaymentTermDays && e.ErrorCode == ImportErrorCodes.InvalidNumber);

    [Fact]
    public void Required_fields_and_format_errors()
    {
        var (rows, errors) = Validator().Validate([Row(2, null, name: " ", taxCode: "123")]);

        Assert.Empty(rows);
        Assert.Contains(errors, e => e.Column == SupplierImportColumns.Code && e.ErrorCode == ImportErrorCodes.Required);
        Assert.Contains(errors, e => e.Column == SupplierImportColumns.Name && e.ErrorCode == ImportErrorCodes.Required);
        Assert.Contains(errors, e => e.Column == SupplierImportColumns.TaxCode && e.ErrorCode == ImportErrorCodes.InvalidFormat);
    }
}
