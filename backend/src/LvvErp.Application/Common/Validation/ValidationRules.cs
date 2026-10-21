using System.Text.RegularExpressions;
using FluentValidation;

namespace LvvErp.Application.Common.Validation;

public static partial class ValidationRules
{
    public const int PasswordMinLength = 8;

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    public static partial Regex CodePattern();

    /// <summary>At least 8 characters with at least one letter and one digit.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty()
        .MinimumLength(PasswordMinLength)
        .MaximumLength(128)
        .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
        .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

    /// <summary>Business code: required, ASCII letters/digits and . _ - only.</summary>
    public static IRuleBuilderOptions<T, string> Code<T>(this IRuleBuilder<T, string> rule, int maxLength) => rule
        .NotEmpty()
        .MaximumLength(maxLength)
        .Matches(CodePattern()).WithMessage("Code may only contain letters, digits, '.', '_' and '-'.");
}
