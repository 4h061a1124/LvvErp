using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LvvErp.Infrastructure.Features.Inventory;

public sealed class StockDocumentConfiguration : IEntityTypeConfiguration<StockDocument>
{
    public void Configure(EntityTypeBuilder<StockDocument> builder)
    {
        builder.ToTable("stock_documents");
        builder.Property(d => d.Number).HasMaxLength(StockDocument.NumberMaxLength);
        builder.HasIndex(d => d.Number).IsUnique();
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.Partner).HasMaxLength(StockDocument.PartnerMaxLength);
        builder.Property(d => d.Note).HasMaxLength(StockDocument.NoteMaxLength);
        builder.Property(d => d.CancelReason).HasMaxLength(StockDocument.NoteMaxLength);
        builder.Property(d => d.CreatedBy).HasMaxLength(50);
        builder.Property(d => d.UpdatedBy).HasMaxLength(50);
        builder.Property(d => d.ApprovedBy).HasMaxLength(50);
        builder.Property(d => d.CancelledBy).HasMaxLength(50);

        builder.HasOne(d => d.Warehouse).WithMany().HasForeignKey(d => d.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.ToWarehouse).WithMany().HasForeignKey(d => d.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Lines).WithOne(l => l.Document).HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.Type, d.DocumentDate });
        builder.HasIndex(d => d.WarehouseId);
        builder.HasIndex(d => d.ToWarehouseId);
    }
}

public sealed class StockDocumentLineConfiguration : IEntityTypeConfiguration<StockDocumentLine>
{
    public void Configure(EntityTypeBuilder<StockDocumentLine> builder)
    {
        builder.ToTable("stock_document_lines", t => t.HasCheckConstraint("ck_stock_document_lines_quantity", "quantity > 0"));
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.LotNumber).HasMaxLength(Lot.LotNumberMaxLength);
        builder.Property(l => l.Note).HasMaxLength(StockDocumentLine.NoteMaxLength);

        builder.HasOne(l => l.Material).WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Location).WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.ToLocation).WithMany().HasForeignKey(l => l.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Lot).WithMany().HasForeignKey(l => l.LotId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.DocumentId, l.LineNo }).IsUnique();
        builder.HasIndex(l => l.MaterialId);
    }
}

public sealed class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("stock_transactions", t => t.HasCheckConstraint("ck_stock_transactions_quantity", "quantity <> 0"));
        builder.Ignore(t => t.Key);
        builder.Property(t => t.Quantity).HasPrecision(18, 3);
        builder.Property(t => t.CreatedBy).HasMaxLength(50);

        builder.HasOne(t => t.Document).WithMany().HasForeignKey(t => t.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.DocumentLine).WithMany().HasForeignKey(t => t.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Material).WithMany().HasForeignKey(t => t.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Warehouse).WithMany().HasForeignKey(t => t.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Location).WithMany().HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Lot).WithMany().HasForeignKey(t => t.LotId).OnDelete(DeleteBehavior.Restrict);

        // Stock card: one material, ordered by date.
        builder.HasIndex(t => new { t.MaterialId, t.TransactionDate });
        builder.HasIndex(t => t.DocumentId);
    }
}

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances", t => t.HasCheckConstraint("ck_stock_balances_quantity", "quantity >= 0"));
        builder.Property(b => b.Quantity).HasPrecision(18, 3);

        builder.HasOne(b => b.Material).WithMany().HasForeignKey(b => b.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Warehouse).WithMany().HasForeignKey(b => b.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Location).WithMany().HasForeignKey(b => b.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Lot).WithMany().HasForeignKey(b => b.LotId).OnDelete(DeleteBehavior.Restrict);

        // One row per key; NULL location/lot count as a value (target of ON CONFLICT in StockLedger).
        builder.HasIndex(b => new { b.MaterialId, b.WarehouseId, b.LocationId, b.LotId }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(b => b.WarehouseId);
    }
}

public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequences");
        builder.HasKey(s => new { s.Prefix, s.Year });
        builder.Property(s => s.Prefix).HasMaxLength(10);
    }
}
