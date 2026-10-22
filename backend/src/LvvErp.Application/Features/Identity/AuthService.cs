using FluentValidation;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Security;
using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Identity;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator) : IAuthService
{
    // Same message for unknown user, wrong password and inactive account: don't leak which one.
    private const string InvalidCredentials = "Invalid username or password.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var userName = User.NormalizeUserName(request.UserName);
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == userName, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException(InvalidCredentials);

        var verification = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (verification == PasswordVerification.Failed)
            throw new UnauthorizedException(InvalidCredentials);

        if (verification == PasswordVerification.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.Hash(request.Password);

        user.LastLoginAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var token = tokenService.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, await BuildCurrentUserAsync(user.Id, ct));
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Not authenticated.");
        return await BuildCurrentUserAsync(userId, ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        await changePasswordValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetCurrentUserEntityAsync(ct);
        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerification.Failed)
            throw new ValidationException(
                [new FluentValidation.Results.ValidationFailure(nameof(request.CurrentPassword), "Current password is incorrect.")]);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.RotateSecurityStamp();
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> GetCurrentUserEntityAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Not authenticated.");
        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("Not authenticated.");
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id, x.UserName, x.FullName, x.Email, x.Roles,
                EmployeeId = x.Employee != null ? x.EmployeeId : null,
                DepartmentId = x.Employee != null ? (Guid?)x.Employee.DepartmentId : null,
                DepartmentName = x.Employee != null ? x.Employee.Department.Name : null,
            })
            .SingleOrDefaultAsync(ct) ?? throw new UnauthorizedException("Not authenticated.");

        return new CurrentUserDto(u.Id, u.UserName, u.FullName, u.Email, u.Roles, RolePermissions.ForRoles(u.Roles),
            u.EmployeeId, u.DepartmentId, u.DepartmentName);
    }
}
