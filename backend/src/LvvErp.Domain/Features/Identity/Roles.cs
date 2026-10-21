namespace LvvErp.Domain.Features.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Employee = "Employee";

    public static readonly IReadOnlyList<string> All = [Admin, HR, Employee];

    public static bool IsValid(string role) => All.Contains(role);
}
