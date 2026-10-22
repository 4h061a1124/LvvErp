using FluentValidation;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing.Requests;

public sealed class SavePurchaseRequestRequestValidator : AbstractValidator<SavePurchaseRequestRequest>
{
    public SavePurchaseRequestRequestValidator()
    {
        RuleFor(x => x.RequestDate).NotEmpty();
        RuleFor(x => x.NeededDate)
            .GreaterThanOrEqualTo(x => x.RequestDate).WithMessage("Needed date cannot be before the request date.")
            .When(x => x.NeededDate is not null);
        RuleFor(x => x.Note).MaximumLength(PurchaseRequest.NoteMaxLength);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .Must(l => l.Count <= PurchaseRequest.MaxLines).WithMessage($"At most {PurchaseRequest.MaxLines} lines.")
            .Must(l => l.Select(x => x.MaterialId).Distinct().Count() == l.Count)
            .WithMessage("Each material may appear only once; combine the quantities.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialId).NotEmpty();
            line.RuleFor(l => l.Quantity).Quantity();
            line.RuleFor(l => l.Note).MaximumLength(PurchaseRequestLine.NoteMaxLength);
        });
    }
}

public sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(PurchaseRequest.ReasonMaxLength);
}

internal static class PurchasingValidationRules
{
    public static IRuleBuilderOptions<T, decimal> Quantity<T>(this IRuleBuilder<T, decimal> rule) => rule
        .GreaterThan(0)
        .LessThan(1_000_000_000_000m)
        .Must(q => decimal.Round(q, PurchasingRules.QuantityDecimals) == q)
        .WithMessage($"At most {PurchasingRules.QuantityDecimals} decimals.");
}
