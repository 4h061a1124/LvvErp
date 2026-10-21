namespace LvvErp.Application.Common.Security;

/// <summary>Claim names in issued JWTs (short JWT names, inbound claim mapping is disabled).</summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string UserName = "unique_name";
    public const string FullName = "name";
    public const string Role = "role";
    public const string SecurityStamp = "sstamp";
}
