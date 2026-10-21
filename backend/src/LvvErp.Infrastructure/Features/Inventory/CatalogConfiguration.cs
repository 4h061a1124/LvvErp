using LvvErp.Domain.Features.Inventory;
using LvvErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LvvErp.Infrastructure.Features.Inventory;

internal static class CatalogConfigurationExtensions
{
    public static void ConfigureCatalog<T>(this EntityTypeBuilder<T> builder, string table) where T : CatalogEntity
    {
        builder.ToTable(table);
        builder.Property(x => x.Code).HasMaxLength(CatalogEntity.CodeMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogEntity.NameMaxLength).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(x => x.CreatedBy).HasMaxLength(50);
        builder.Property(x => x.UpdatedBy).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder) => builder.ConfigureCatalog("units");
}

public sealed class MaterialGroupConfiguration : IEntityTypeConfiguration<MaterialGroup>
{
    public void Configure(EntityTypeBuilder<MaterialGroup> builder) => builder.ConfigureCatalog("material_groups");
}

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ConfigureCatalog("warehouses");
        builder.Property(w => w.Note).HasMaxLength(Warehouse.NoteMaxLength);
    }
}

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.Property(l => l.Code).HasMaxLength(Location.CodeMaxLength);
        builder.Property(l => l.Description).HasMaxLength(Location.DescriptionMaxLength);
        builder.Property(l => l.CreatedBy).HasMaxLength(50);
        builder.Property(l => l.UpdatedBy).HasMaxLength(50);

        // Locations belong to their warehouse; deleting an unused warehouse removes them too.
        builder.HasOne(l => l.Warehouse).WithMany(w => w.Locations).HasForeignKey(l => l.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => new { l.WarehouseId, l.Code }).IsUnique();
    }
}

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");
        builder.Property(m => m.Code).HasMaxLength(Material.CodeMaxLength);
        builder.HasIndex(m => m.Code).IsUnique();
        builder.Property(m => m.Name).HasMaxLength(Material.NameMaxLength).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(m => m.Specification).HasMaxLength(Material.SpecificationMaxLength);
        builder.Property(m => m.MinStock).HasPrecision(18, 3);
        builder.Property(m => m.PhotoPath).HasMaxLength(300);
        builder.Property(m => m.CreatedBy).HasMaxLength(50);
        builder.Property(m => m.UpdatedBy).HasMaxLength(50);

        builder.HasOne(m => m.Unit).WithMany().HasForeignKey(m => m.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Group).WithMany().HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.GroupId);

        builder.ToTable(t => t.HasCheckConstraint("ck_materials_min_stock", "min_stock >= 0"));
    }
}

public sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lots");
        builder.Property(l => l.LotNumber).HasMaxLength(Lot.LotNumberMaxLength);
        builder.Property(l => l.CreatedBy).HasMaxLength(50);
        builder.Property(l => l.UpdatedBy).HasMaxLength(50);

        builder.HasOne(l => l.Material).WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.MaterialId, l.LotNumber }).IsUnique();
    }
}
