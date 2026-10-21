using FluentValidation;
using LvvErp.Application.Common.Validation;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Catalog;

public sealed class SaveCatalogItemRequestValidator : AbstractValidator<SaveCatalogItemRequest>
{
    public SaveCatalogItemRequestValidator()
    {
        RuleFor(x => x.Code).Code(CatalogEntity.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatalogEntity.NameMaxLength);
    }
}

public sealed class SaveWarehouseRequestValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).Code(CatalogEntity.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatalogEntity.NameMaxLength);
        RuleFor(x => x.Note).MaximumLength(Warehouse.NoteMaxLength);
    }
}

public sealed class SaveLocationRequestValidator : AbstractValidator<SaveLocationRequest>
{
    public SaveLocationRequestValidator()
    {
        RuleFor(x => x.Code).Code(Location.CodeMaxLength);
        RuleFor(x => x.Description).MaximumLength(Location.DescriptionMaxLength);
    }
}
