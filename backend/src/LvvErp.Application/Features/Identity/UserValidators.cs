using FluentValidation;
using LvvErp.Application.Common.Validation;
using LvvErp.Domain.Features.Identity;

namespace LvvErp.Application.Features.Identity;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().Length(3, 50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("Username may only contain letters, digits, '.', '_' and '-'.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.Roles).ValidRoles();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.NewPassword!).StrongPassword().When(x => x.NewPassword is not null);
        RuleFor(x => x.Roles).ValidRoles();
    }
}

internal static class RoleRules
{
    public static IRuleBuilderOptions<T, IReadOnlyList<string>> ValidRoles<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule) => rule
        .NotEmpty().WithMessage("At least one role is required.")
        .Must(roles => roles.All(Roles.IsValid))
        .WithMessage($"Roles must be one of: {string.Join(", ", Roles.All)}.");
}
