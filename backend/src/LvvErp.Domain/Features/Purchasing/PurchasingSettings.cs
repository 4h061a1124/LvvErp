using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Domain.Features.Purchasing;

/// <summary>Single-row settings of the purchasing module, edited by Admin.</summary>
public class PurchasingSettings : AuditableEntity
{
    /// <summary>The one row's id.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public const decimal DefaultDirectorApprovalThreshold = 50_000_000m;
    public const int CompanyNameMaxLength = 200;
    public const int CompanyAddressMaxLength = 500;
    public const int CompanyTaxCodeMaxLength = 14;
    public const int CompanyPhoneMaxLength = 20;

    /// <summary>
    /// Orders whose total including VAT is at or above this amount (VND) are approved by a Director;
    /// cheaper ones by the head of the purchasing department.
    /// </summary>
    public decimal DirectorApprovalThreshold { get; set; } = DefaultDirectorApprovalThreshold;

    /// <summary>Phòng mua hàng: its department head approves orders under the threshold.</summary>
    public Guid? PurchasingDepartmentId { get; set; }
    public Department? PurchasingDepartment { get; set; }

    /// <summary>The buyer as printed on purchase orders.</summary>
    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyTaxCode { get; set; }
    public string? CompanyPhone { get; set; }
}
