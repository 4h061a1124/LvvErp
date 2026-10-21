using FluentValidation;
using LvvErp.Application.Common.Time;
using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Documents;

/// <summary>Shape of a document. Rules that need the database live in StockDocumentService.</summary>
public sealed class StockDocumentDraftValidator : AbstractValidator<StockDocumentDraft>
{
    public const decimal MaxQuantity = 999_999_999_999_999m;

    public StockDocumentDraftValidator(BusinessClock clock)
    {
        RuleFor(x => x.DocumentDate)
            .GreaterThanOrEqualTo(new DateOnly(2000, 1, 1))
            .Must(d => d <= clock.Today).WithMessage("Document date cannot be in the future.");
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ToWarehouseId).NotEmpty().When(x => x.Type == StockDocumentType.Transfer);
        RuleFor(x => x.ToWarehouseId).Null().When(x => x.Type != StockDocumentType.Transfer);
        RuleFor(x => x.Reason)
            .Must((draft, reason) => StockDocument.IsReasonAllowed(draft.Type, reason))
            .WithMessage("Reason is not valid for this document type.");
        RuleFor(x => x.Partner).MaximumLength(StockDocument.PartnerMaxLength);
        RuleFor(x => x.Note).MaximumLength(StockDocument.NoteMaxLength);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A document needs at least one line.")
            .Must(l => l.Count <= StockDocument.MaxLines).WithMessage($"At most {StockDocument.MaxLines} lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialId).NotEmpty();
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaxQuantity)
                .PrecisionScale(18, 3, ignoreTrailingZeros: true);
            line.RuleFor(l => l.LotNumber).MaximumLength(Lot.LotNumberMaxLength);
            line.RuleFor(l => l.ExpiryDate)
                .GreaterThanOrEqualTo(l => l.ManufactureDate)
                .When(l => l.ManufactureDate is not null && l.ExpiryDate is not null)
                .WithMessage("Expiry date must not be before the manufacture date.");
            line.RuleFor(l => l.Note).MaximumLength(StockDocumentLine.NoteMaxLength);
        });

        RuleForEach(x => x.Lines)
            .Must((draft, l) => draft.WarehouseId != draft.ToWarehouseId || l.LocationId != l.ToLocationId)
            .When(x => x.Type == StockDocumentType.Transfer)
            .WithMessage("Source and destination are the same.");
    }
}

public sealed class CancelStockDocumentRequestValidator : AbstractValidator<CancelStockDocumentRequest>
{
    public CancelStockDocumentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StockDocument.NoteMaxLength);
    }
}
