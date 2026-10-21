using System.Linq.Expressions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Persistence;
using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Accent-insensitive search for Vietnamese names ("nguyen" matches "Nguyễn").
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.HasDbFunction(typeof(AppDbFunctions).GetMethod(nameof(AppDbFunctions.Unaccent))!)
            .HasName("unaccent")
            .IsBuiltIn();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplySoftDeleteFilters(modelBuilder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
                continue;

            // e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
