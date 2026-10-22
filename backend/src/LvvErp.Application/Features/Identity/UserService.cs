using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Identity;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
}

public sealed class UserService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking().OrderBy(u => u.UserName).Select(ToDto).ToListAsync(ct);

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Where(u => u.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw NotFoundException.For<User>(id);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var userName = User.NormalizeUserName(request.UserName);
        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            throw new ConflictException($"Username '{userName}' is already taken.", ErrorCodes.DuplicateUserName);
        await EnsureEmployeeLinkableAsync(request.EmployeeId, userId: null, ct);

        var user = new User
        {
            UserName = userName,
            FullName = request.FullName.Trim(),
            Email = TextNormalizer.NullIfBlank(request.Email),
            Roles = request.Roles.Distinct().ToList(),
            PasswordHash = passwordHasher.Hash(request.Password),
            EmployeeId = request.EmployeeId,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var user = await FindAsync(id, ct);
        var roles = request.Roles.Distinct().ToList();

        // Guard against an admin locking themselves out.
        if (user.Id == currentUser.UserId)
        {
            if (!request.IsActive)
                throw new ConflictException("You cannot deactivate your own account.", ErrorCodes.CannotChangeOwnAccess);
            if (user.Roles.Contains(Roles.Admin) && !roles.Contains(Roles.Admin))
                throw new ConflictException("You cannot remove your own Admin role.", ErrorCodes.CannotChangeOwnAccess);
        }
        await EnsureEmployeeLinkableAsync(request.EmployeeId, user.Id, ct);

        var securityRelevantChange =
            user.IsActive != request.IsActive
            || !user.Roles.Order().SequenceEqual(roles.Order())
            || request.NewPassword is not null;

        user.FullName = request.FullName.Trim();
        user.Email = TextNormalizer.NullIfBlank(request.Email);
        user.Roles = roles;
        user.IsActive = request.IsActive;
        user.EmployeeId = request.EmployeeId;
        if (request.NewPassword is not null)
            user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        if (securityRelevantChange)
            user.RotateSecurityStamp();

        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    private async Task EnsureEmployeeLinkableAsync(Guid? employeeId, Guid? userId, CancellationToken ct)
    {
        if (employeeId is null) return;

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
            throw new ValidationException([new ValidationFailure("EmployeeId", "Employee does not exist.")]);

        if (await db.Users.AnyAsync(u => u.EmployeeId == employeeId && u.Id != userId, ct))
            throw new ConflictException("This employee is already linked to another account.", ErrorCodes.EmployeeAlreadyLinked);
    }

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For<User>(id);

    // A soft-deleted employee drops out through the query filter, so the link shows as empty.
    private static readonly System.Linq.Expressions.Expression<Func<User, UserDto>> ToDto = u => new UserDto(
        u.Id, u.UserName, u.FullName, u.Email, u.Roles, u.IsActive, u.LastLoginAt, u.CreatedAt,
        u.Employee != null ? u.EmployeeId : null,
        u.Employee != null ? u.Employee.Code : null,
        u.Employee != null ? u.Employee.FullName : null,
        u.Employee != null ? u.Employee.Department.Name : null);
}
