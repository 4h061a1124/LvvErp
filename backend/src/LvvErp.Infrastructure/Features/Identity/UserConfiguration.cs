using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LvvErp.Infrastructure.Features.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.Property(u => u.UserName).HasMaxLength(50);
        builder.HasIndex(u => u.UserName).IsUnique();

        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.CreatedBy).HasMaxLength(50);
        builder.Property(u => u.UpdatedBy).HasMaxLength(50);

        // Stored as a PostgreSQL text[] column.
        builder.Property(u => u.Roles);

        builder.HasOne(u => u.Employee).WithMany().HasForeignKey(u => u.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(u => u.EmployeeId).IsUnique();
    }
}
