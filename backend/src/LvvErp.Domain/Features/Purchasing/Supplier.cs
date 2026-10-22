using System.Text.RegularExpressions;
using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Purchasing;

/// <summary>Nhà cung cấp. Deactivated instead of deleted once used on an order.</summary>
public partial class Supplier : AuditableEntity
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 200;
    public const int TaxCodeMaxLength = 14;
    public const int AddressMaxLength = 500;
    public const int ContactNameMaxLength = 100;
    public const int PhoneMaxLength = 20;
    public const int EmailMaxLength = 200;
    public const int PaymentTermsMaxLength = 200;
    public const int MaxPaymentTermDays = 365;

    /// <summary>Stored upper-case.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Mã số thuế: 10 digits, or 10 digits + "-" + 3 digits for a branch. Unique when present.</summary>
    public string? TaxCode { get; set; }

    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Số ngày công nợ, copied onto new orders.</summary>
    public int PaymentTermDays { get; set; }

    /// <summary>Free-text payment terms, e.g. "Chuyển khoản 50% khi đặt hàng".</summary>
    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; } = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    public static partial Regex TaxCodePattern();
}
