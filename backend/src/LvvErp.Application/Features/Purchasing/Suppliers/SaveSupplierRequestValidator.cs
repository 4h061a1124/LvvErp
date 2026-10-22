using FluentValidation;
using LvvErp.Application.Common.Validation;
using LvvErp.Application.Features.Employees;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing.Suppliers;

public sealed class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.Code).Code(Supplier.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Supplier.NameMaxLength);
        RuleFor(x => x.TaxCode!.Trim())
            .Matches(Supplier.TaxCodePattern()).WithMessage("Tax code must be 10 digits, or 10 digits-3 digits for a branch.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode))
            .OverridePropertyName(nameof(SaveSupplierRequest.TaxCode));
        RuleFor(x => x.Address).MaximumLength(Supplier.AddressMaxLength);
        RuleFor(x => x.ContactName).MaximumLength(Supplier.ContactNameMaxLength);
        RuleFor(x => x.Phone!)
            .MaximumLength(Supplier.PhoneMaxLength)
            .Matches(EmployeeRules.PhonePattern()).WithMessage("Phone is not valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email!)
            .MaximumLength(Supplier.EmailMaxLength)
            .Must(EmployeeRules.IsValidEmail).WithMessage("Email is not valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PaymentTermDays).InclusiveBetween(0, Supplier.MaxPaymentTermDays);
        RuleFor(x => x.PaymentTerms).MaximumLength(Supplier.PaymentTermsMaxLength);
    }
}
