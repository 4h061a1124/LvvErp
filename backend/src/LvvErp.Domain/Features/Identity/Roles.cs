namespace LvvErp.Domain.Features.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Employee = "Employee";

    /// <summary>Thủ kho.</summary>
    public const string Storekeeper = "Storekeeper";

    public static readonly IReadOnlyList<string> All = [Admin, HR, Employee, Storekeeper];

    public static bool IsValid(string role) => All.Contains(role);
}
