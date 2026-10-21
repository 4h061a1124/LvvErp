using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core DbContext used by application services.
/// Each module adds its DbSet properties here.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Employee> Employees { get; }
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
