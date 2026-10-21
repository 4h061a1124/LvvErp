using System.Net.Mail;
using System.Text.RegularExpressions;
using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Application.Features.Employees;

/// <summary>Field rules shared by the form validator and the Excel import.</summary>
public static partial class EmployeeRules
{
    public const long MaxPhotoBytes = PhotoRules.MaxBytes;

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    public static partial Regex CodePattern();

    [GeneratedRegex(@"^[0-9+\-\s().]+$")]
    public static partial Regex PhonePattern();

    public static bool IsValidEmail(string email) =>
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    private static readonly Dictionary<string, EmployeeStatus> StatusAliases = new()
    {
        [TextNormalizer.ToMatchKey("Đang làm")] = EmployeeStatus.Active,
        ["active"] = EmployeeStatus.Active,
        [TextNormalizer.ToMatchKey("Tạm nghỉ")] = EmployeeStatus.OnLeave,
        ["onleave"] = EmployeeStatus.OnLeave,
        ["on leave"] = EmployeeStatus.OnLeave,
        [TextNormalizer.ToMatchKey("Đã nghỉ")] = EmployeeStatus.Resigned,
        [TextNormalizer.ToMatchKey("Nghỉ việc")] = EmployeeStatus.Resigned,
        ["resigned"] = EmployeeStatus.Resigned,
    };

    /// <summary>Vietnamese labels, used in the Excel template.</summary>
    public static readonly IReadOnlyDictionary<EmployeeStatus, string> StatusLabelsVi = new Dictionary<EmployeeStatus, string>
    {
        [EmployeeStatus.Active] = "Đang làm",
        [EmployeeStatus.OnLeave] = "Tạm nghỉ",
        [EmployeeStatus.Resigned] = "Đã nghỉ",
    };

    /// <summary>Accepts Vietnamese ("Đang làm", "dang lam") or English ("Active") labels.</summary>
    public static bool TryParseStatus(string? text, out EmployeeStatus status) =>
        StatusAliases.TryGetValue(TextNormalizer.ToMatchKey(text), out status);
}
