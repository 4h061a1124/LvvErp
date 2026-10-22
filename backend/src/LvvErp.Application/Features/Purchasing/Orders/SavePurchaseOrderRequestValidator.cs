using FluentValidation;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing.Orders;

public sealed class SavePurchaseOrderRequestValidator : AbstractValidator<SavePurchaseOrderRequest>
{
    public SavePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.OrderDate).NotEmpty();
        RuleFor(x => x.ExpectedDate)
            .GreaterThanOrEqualTo(x => x.OrderDate).WithMessage("Expected date cannot be before the order date.")
            .When(x => x.ExpectedDate is not null);
        RuleFor(x => x.PaymentTermDays).InclusiveBetween(0, Supplier.MaxPaymentTermDays);
        RuleFor(x => x.VatRate).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(PurchaseOrder.NoteMaxLength);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .Must(l => l.Count <= PurchaseOrder.MaxLines).WithMessage($"At most {PurchaseOrder.MaxLines} lines.")
            .Must(l => l.Select(x => x.MaterialId).Distinct().Count() == l.Count)
            .WithMessage("Each material may appear only once; combine the lines.")
            .Must(l => l.SelectMany(x => x.SourceLineIds ?? []).Distinct().Count() == l.Sum(x => x.SourceLineIds?.Count ?? 0))
            .WithMessage("A request line can be used only once per order.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialId).NotEmpty();
            line.RuleFor(l => l.Quantity).Quantity();
            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(PurchasingRules.MaxUnitPrice)
                .Must(p => decimal.Round(p, PurchasingRules.UnitPriceDecimals) == p)
                .WithMessage($"At most {PurchasingRules.UnitPriceDecimals} decimals.");
            line.RuleFor(l => l.SourceLineIds).NotEmpty().WithMessage("Each line must come from a purchase request.");
            line.RuleFor(l => l.Note).MaximumLength(PurchaseOrderLine.NoteMaxLength);
            line.RuleFor(l => l.OverOrderReason).MaximumLength(PurchaseOrder.ReasonMaxLength);
        });
    }
}
