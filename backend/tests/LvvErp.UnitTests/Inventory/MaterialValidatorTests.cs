using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.UnitTests.Inventory;

public sealed class MaterialValidatorTests
{
    private readonly SaveMaterialRequestValidator _validator = new();

    private static SaveMaterialRequest Valid() =>
        new("BL-M8X30", "Bu lông lục giác", "M8x30, inox 304", Guid.NewGuid(), Guid.NewGuid(), 100, TrackLots: false);

    [Fact]
    public void Valid_request_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("BL M8")]
    [InlineData("bù-lông")]
    public void Rejects_bad_codes(string code) =>
        Assert.False(_validator.Validate(Valid() with { Code = code }).IsValid);

    [Fact]
    public void Rejects_code_longer_than_column() =>
        Assert.False(_validator.Validate(Valid() with { Code = new string('A', Material.CodeMaxLength + 1) }).IsValid);

    [Theory]
    [InlineData(-1)]
    [InlineData(0.0001)]
    public void Rejects_negative_or_too_precise_min_stock(double minStock) =>
        Assert.False(_validator.Validate(Valid() with { MinStock = (decimal)minStock }).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(12.5)]
    [InlineData(0.125)]
    public void Accepts_min_stock_with_up_to_three_decimals(double minStock) =>
        Assert.True(_validator.Validate(Valid() with { MinStock = (decimal)minStock }).IsValid);

    [Fact]
    public void Requires_unit_and_group()
    {
        Assert.False(_validator.Validate(Valid() with { UnitId = Guid.Empty }).IsValid);
        Assert.False(_validator.Validate(Valid() with { GroupId = Guid.Empty }).IsValid);
    }

    [Fact]
    public void Catalog_and_location_codes_follow_the_same_rules()
    {
        Assert.True(new SaveCatalogItemRequestValidator().Validate(new SaveCatalogItemRequest("KG", "Kg")).IsValid);
        Assert.False(new SaveCatalogItemRequestValidator().Validate(new SaveCatalogItemRequest("K G", "Kg")).IsValid);
        Assert.True(new SaveLocationRequestValidator().Validate(new SaveLocationRequest("A-01-03", null)).IsValid);
        Assert.False(new SaveLocationRequestValidator().Validate(new SaveLocationRequest("", null)).IsValid);
    }
}
