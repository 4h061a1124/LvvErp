using LvvErp.Domain.Features.Employees;
using LvvErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LvvErp.Infrastructure.Features.Employees;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.Property(e => e.Code).HasMaxLength(Employee.CodeMaxLength);
        // Unique among live rows only, so a code can be reused after its employee is deleted.
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("is_deleted = false");

        builder.Property(e => e.FullName).HasMaxLength(Employee.FullNameMaxLength).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(e => e.Email).HasMaxLength(Employee.EmailMaxLength);
        builder.Property(e => e.Phone).HasMaxLength(Employee.PhoneMaxLength);
        builder.Property(e => e.PhotoPath).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedBy).HasMaxLength(50);
        builder.Property(e => e.UpdatedBy).HasMaxLength(50);
        builder.Property(e => e.DeletedBy).HasMaxLength(50);

        builder.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Position).WithMany().HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.Property(d => d.Code).HasMaxLength(20);
        builder.Property(d => d.Name).HasMaxLength(100).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(d => d.CreatedBy).HasMaxLength(50);
        builder.Property(d => d.UpdatedBy).HasMaxLength(50);
        builder.HasIndex(d => d.Code).IsUnique();
        builder.HasIndex(d => d.Name).IsUnique();
    }
}

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions");
        builder.Property(p => p.Code).HasMaxLength(20);
        builder.Property(p => p.Name).HasMaxLength(100).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(p => p.CreatedBy).HasMaxLength(50);
        builder.Property(p => p.UpdatedBy).HasMaxLength(50);
        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.Name).IsUnique();
    }
}
