using FluentValidation;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Application.Features.Employees;

public sealed class SaveEmployeeRequestValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Employee.CodeMaxLength)
            .Matches(EmployeeRules.CodePattern()).WithMessage("Code may only contain letters, digits, '.', '_' and '-'.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Employee.FullNameMaxLength);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.PositionId).NotEmpty();
        RuleFor(x => x.Email!).MaximumLength(Employee.EmailMaxLength)
            .Must(EmployeeRules.IsValidEmail).WithMessage("Email is not valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone!).MaximumLength(Employee.PhoneMaxLength)
            .Matches(EmployeeRules.PhonePattern()).WithMessage("Phone is not valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Status).IsInEnum();
    }
}
