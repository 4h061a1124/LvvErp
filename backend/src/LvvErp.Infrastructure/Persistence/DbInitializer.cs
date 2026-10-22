using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
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
    /// <summary>Default purchasing department; its head approves orders under the director threshold.</summary>
    private const string PurchasingDepartmentCode = "MH";

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

    private static readonly (string Code, string Name)[] DefaultUnits =
    [
        ("CAI", "Cái"),
        ("BO", "Bộ"),
        ("KG", "Kg"),
        ("G", "Gram"),
        ("M", "Mét"),
        ("L", "Lít"),
        ("CUON", "Cuộn"),
        ("THUNG", "Thùng"),
        ("TAM", "Tấm"),
        ("HOP", "Hộp"),
    ];

    private static readonly (string Code, string Name)[] DefaultMaterialGroups =
    [
        ("NVL", "Nguyên vật liệu"),
        ("PT", "Phụ tùng thay thế"),
        ("VTTH", "Vật tư tiêu hao"),
        ("CCDC", "Công cụ dụng cụ"),
        ("BB", "Bao bì"),
    ];

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedLookupsAsync(ct);
        await SeedInventoryCatalogAsync(ct);
        await SeedPurchasingSettingsAsync(ct);
        await SeedAdminAsync(ct);
    }

    private async Task SeedPurchasingSettingsAsync(CancellationToken ct)
    {
        if (await db.PurchasingSettings.AnyAsync(ct)) return;

        var purchasingDepartmentId = await db.Departments
            .Where(d => d.Code == PurchasingDepartmentCode)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(ct);
        db.PurchasingSettings.Add(new PurchasingSettings
        {
            Id = PurchasingSettings.SingletonId,
            PurchasingDepartmentId = purchasingDepartmentId,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded purchasing settings.");
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

    private async Task SeedInventoryCatalogAsync(CancellationToken ct)
    {
        if (!await db.Units.AnyAsync(ct))
        {
            db.Units.AddRange(DefaultUnits.Select(u => new Unit { Code = u.Code, Name = u.Name }));
            logger.LogInformation("Seeding {Count} default units.", DefaultUnits.Length);
        }

        if (!await db.MaterialGroups.AnyAsync(ct))
        {
            db.MaterialGroups.AddRange(DefaultMaterialGroups.Select(g => new MaterialGroup { Code = g.Code, Name = g.Name }));
            logger.LogInformation("Seeding {Count} default material groups.", DefaultMaterialGroups.Length);
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
