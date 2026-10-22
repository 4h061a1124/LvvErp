namespace LvvErp.Domain.Features.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Employee = "Employee";

    /// <summary>Thủ kho.</summary>
    public const string Storekeeper = "Storekeeper";

    /// <summary>Nhân viên mua hàng.</summary>
    public const string Purchaser = "Purchaser";

    /// <summary>Trưởng bộ phận: approves requests of their own department (the department of their linked employee).</summary>
    public const string DepartmentHead = "DepartmentHead";

    /// <summary>Giám đốc.</summary>
    public const string Director = "Director";

    public static readonly IReadOnlyList<string> All =
        [Admin, HR, Employee, Storekeeper, Purchaser, DepartmentHead, Director];

    public static bool IsValid(string role) => All.Contains(role);
}
