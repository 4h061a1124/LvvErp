using FluentValidation;
using LvvErp.Application.Common.Validation;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Catalog;

public sealed class SaveMaterialRequestValidator : AbstractValidator<SaveMaterialRequest>
{
    /// <summary>decimal(18,3) column.</summary>
    public const decimal MaxQuantity = 999_999_999_999_999m;

    public SaveMaterialRequestValidator()
    {
        RuleFor(x => x.Code).Code(Material.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Material.NameMaxLength);
        RuleFor(x => x.Specification).MaximumLength(Material.SpecificationMaxLength);
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.MinStock).InclusiveBetween(0, MaxQuantity).PrecisionScale(18, 3, ignoreTrailingZeros: true);
        RuleFor(x => x.ReceiptTolerancePercent)
            .InclusiveBetween(0, Material.MaxReceiptTolerancePercent)
            .PrecisionScale(5, 2, ignoreTrailingZeros: true);
    }
}
