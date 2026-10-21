using FluentValidation;

namespace LvvErp.Application.Common.Validation;

public static class ValidationRules
{
    public const int PasswordMinLength = 8;

    /// <summary>At least 8 characters with at least one letter and one digit.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty()
        .MinimumLength(PasswordMinLength)
        .MaximumLength(128)
        .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
        .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
}
