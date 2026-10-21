namespace LvvErp.Application.Features.Identity;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? EmployeeId);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
