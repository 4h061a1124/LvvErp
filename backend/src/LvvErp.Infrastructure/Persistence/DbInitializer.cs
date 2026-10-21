using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LvvErp.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUserName { get; set; } = "admin";
    public string AdminFullName { get; set; } = "Administrator";

    /// <summary>Required only when the users table is empty. Set via user-secrets or environment variable.</summary>
    public string? AdminPassword { get; set; }
}

/// <summary>Applies pending migrations and seeds required data. Every step is idempotent.</summary>
public sealed class DbInitializer(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> seedOptions,
    ILogger<DbInitializer> logger)
{
    private static readonly (string Code, string Name)[] DefaultDepartments =
    [
        ("BGD", "Ban giám đốc"),
        ("SX", "Sản xuất"),
        ("KHO", "Kho"),
        ("QC", "Quản lý chất lượng"),
        ("BT", "Bảo trì"),
        ("KHSX", "Kế hoạch sản xuất"),
        ("MH", "Mua hàng"),
        ("KD", "Kinh doanh"),
        ("KT", "Kế toán"),
        ("NS", "Nhân sự"),
    ];

    private static readonly (string Code, string Name)[] DefaultPositions =
    [
        ("GD", "Giám đốc"),
        ("PGD", "Phó giám đốc"),
        ("TP", "Trưởng phòng"),
        ("QD", "Quản đốc"),
        ("TT", "Tổ trưởng"),
        ("KS", "Kỹ sư"),
        ("KTV", "Kỹ thuật viên"),
        ("NV", "Nhân viên"),
        ("CN", "Công nhân"),
    ];

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedLookupsAsync(ct);
        await SeedAdminAsync(ct);
    }

    private async Task SeedLookupsAsync(CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(ct))
        {
            db.Departments.AddRange(DefaultDepartments.Select((d, i) =>
                new Department { Code = d.Code, Name = d.Name, SortOrder = i + 1 }));
            logger.LogInformation("Seeding {Count} default departments.", DefaultDepartments.Length);
        }

        if (!await db.Positions.AnyAsync(ct))
        {
            db.Positions.AddRange(DefaultPositions.Select((p, i) =>
                new Position { Code = p.Code, Name = p.Name, SortOrder = i + 1 }));
            logger.LogInformation("Seeding {Count} default positions.", DefaultPositions.Length);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var seed = seedOptions.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            logger.LogWarning("No users exist and Seed:AdminPassword is not set; skipping admin account creation.");
            return;
        }

        db.Users.Add(new User
        {
            UserName = User.NormalizeUserName(seed.AdminUserName),
            FullName = seed.AdminFullName,
            Roles = [Roles.Admin],
            PasswordHash = passwordHasher.Hash(seed.AdminPassword),
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created initial admin account '{UserName}'.", seed.AdminUserName);
    }
}
