using LvvErp.Domain.Features.Purchasing;
using LvvErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LvvErp.Infrastructure.Features.Purchasing;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.Property(s => s.Code).HasMaxLength(Supplier.CodeMaxLength);
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(Supplier.NameMaxLength).UseCollation(AppDbContext.VietnameseCollation);
        builder.Property(s => s.TaxCode).HasMaxLength(Supplier.TaxCodeMaxLength);
        builder.HasIndex(s => s.TaxCode).IsUnique().HasFilter("tax_code IS NOT NULL");
        builder.Property(s => s.Address).HasMaxLength(Supplier.AddressMaxLength);
        builder.Property(s => s.ContactName).HasMaxLength(Supplier.ContactNameMaxLength);
        builder.Property(s => s.Phone).HasMaxLength(Supplier.PhoneMaxLength);
        builder.Property(s => s.Email).HasMaxLength(Supplier.EmailMaxLength);
        builder.Property(s => s.PaymentTerms).HasMaxLength(Supplier.PaymentTermsMaxLength);
        builder.Property(s => s.CreatedBy).HasMaxLength(50);
        builder.Property(s => s.UpdatedBy).HasMaxLength(50);

        builder.ToTable(t => t.HasCheckConstraint("ck_suppliers_payment_term_days",
            $"payment_term_days BETWEEN 0 AND {Supplier.MaxPaymentTermDays}"));
    }
}

public sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("purchase_requests");
        builder.Property(r => r.Number).HasMaxLength(PurchaseRequest.NumberMaxLength);
        builder.HasIndex(r => r.Number).IsUnique();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Note).HasMaxLength(PurchaseRequest.NoteMaxLength);
        builder.Property(r => r.RejectReason).HasMaxLength(PurchaseRequest.ReasonMaxLength);
        builder.Property(r => r.CloseReason).HasMaxLength(PurchaseRequest.ReasonMaxLength);
        builder.Property(r => r.CancelReason).HasMaxLength(PurchaseRequest.ReasonMaxLength);
        builder.Property(r => r.CreatedBy).HasMaxLength(50);
        builder.Property(r => r.UpdatedBy).HasMaxLength(50);
        builder.Property(r => r.ApprovedBy).HasMaxLength(50);
        builder.Property(r => r.RejectedBy).HasMaxLength(50);
        builder.Property(r => r.ClosedBy).HasMaxLength(50);
        builder.Property(r => r.CancelledBy).HasMaxLength(50);

        builder.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Requester).WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Lines).WithOne(l => l.Request).HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.Status, r.DepartmentId });
        builder.HasIndex(r => r.RequestDate);
        builder.HasIndex(r => r.RequesterId);
    }
}

public sealed class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.ToTable("purchase_request_lines", t => t.HasCheckConstraint("ck_purchase_request_lines_quantity", "quantity > 0"));
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.Note).HasMaxLength(PurchaseRequestLine.NoteMaxLength);

        builder.HasOne(l => l.Material).WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.RequestId, l.LineNo }).IsUnique();
        builder.HasIndex(l => l.MaterialId);
    }
}

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", t =>
        {
            t.HasCheckConstraint("ck_purchase_orders_payment_term_days", $"payment_term_days BETWEEN 0 AND {Supplier.MaxPaymentTermDays}");
            t.HasCheckConstraint("ck_purchase_orders_total", "subtotal >= 0 AND vat_amount >= 0 AND total = subtotal + vat_amount");
        });
        builder.Property(o => o.Number).HasMaxLength(PurchaseOrder.NumberMaxLength);
        builder.HasIndex(o => o.Number).IsUnique();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.VatRate).HasConversion<string>().HasMaxLength(10);
        builder.Property(o => o.ApprovalLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Subtotal).HasPrecision(18, 0);
        builder.Property(o => o.VatAmount).HasPrecision(18, 0);
        builder.Property(o => o.Total).HasPrecision(18, 0);
        builder.Property(o => o.Note).HasMaxLength(PurchaseOrder.NoteMaxLength);
        builder.Property(o => o.RejectReason).HasMaxLength(PurchaseOrder.ReasonMaxLength);
        builder.Property(o => o.CloseReason).HasMaxLength(PurchaseOrder.ReasonMaxLength);
        builder.Property(o => o.CancelReason).HasMaxLength(PurchaseOrder.ReasonMaxLength);
        builder.Property(o => o.CreatedBy).HasMaxLength(50);
        builder.Property(o => o.UpdatedBy).HasMaxLength(50);
        builder.Property(o => o.SubmittedBy).HasMaxLength(50);
        builder.Property(o => o.ApprovedBy).HasMaxLength(50);
        builder.Property(o => o.RejectedBy).HasMaxLength(50);
        builder.Property(o => o.ClosedBy).HasMaxLength(50);
        builder.Property(o => o.CancelledBy).HasMaxLength(50);

        builder.HasOne(o => o.Supplier).WithMany().HasForeignKey(o => o.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Warehouse).WithMany().HasForeignKey(o => o.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne(l => l.Order).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.Status, o.OrderDate });
        builder.HasIndex(o => o.SupplierId);
        builder.HasIndex(o => o.ExpectedDate);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", t =>
        {
            t.HasCheckConstraint("ck_purchase_order_lines_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_purchase_order_lines_price", "unit_price >= 0 AND amount >= 0");
        });
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.Amount).HasPrecision(18, 0);
        builder.Property(l => l.Note).HasMaxLength(PurchaseOrderLine.NoteMaxLength);
        builder.Property(l => l.OverOrderReason).HasMaxLength(PurchaseOrder.ReasonMaxLength);

        builder.HasOne(l => l.Material).WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(l => l.Sources).WithOne(s => s.OrderLine).HasForeignKey(s => s.OrderLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => new { l.OrderId, l.LineNo }).IsUnique();
        builder.HasIndex(l => new { l.OrderId, l.MaterialId }).IsUnique();
        builder.HasIndex(l => l.MaterialId);
    }
}

public sealed class PurchaseOrderLineSourceConfiguration : IEntityTypeConfiguration<PurchaseOrderLineSource>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineSource> builder)
    {
        builder.ToTable("purchase_order_line_sources", t => t.HasCheckConstraint("ck_purchase_order_line_sources_quantity", "quantity >= 0"));
        builder.Property(s => s.Quantity).HasPrecision(18, 3);

        // A request line with orders can't be deleted (its request is no longer a draft anyway).
        builder.HasOne(s => s.RequestLine).WithMany().HasForeignKey(s => s.RequestLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.OrderLineId, s.RequestLineId }).IsUnique();
        builder.HasIndex(s => s.RequestLineId);
    }
}

public sealed class PurchasingSettingsConfiguration : IEntityTypeConfiguration<PurchasingSettings>
{
    public void Configure(EntityTypeBuilder<PurchasingSettings> builder)
    {
        builder.ToTable("purchasing_settings");
        builder.Property(s => s.DirectorApprovalThreshold).HasPrecision(18, 0);
        builder.Property(s => s.CompanyName).HasMaxLength(PurchasingSettings.CompanyNameMaxLength);
        builder.Property(s => s.CompanyAddress).HasMaxLength(PurchasingSettings.CompanyAddressMaxLength);
        builder.Property(s => s.CompanyTaxCode).HasMaxLength(PurchasingSettings.CompanyTaxCodeMaxLength);
        builder.Property(s => s.CompanyPhone).HasMaxLength(PurchasingSettings.CompanyPhoneMaxLength);
        builder.Property(s => s.CreatedBy).HasMaxLength(50);
        builder.Property(s => s.UpdatedBy).HasMaxLength(50);

        builder.HasOne(s => s.PurchasingDepartment).WithMany().HasForeignKey(s => s.PurchasingDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_purchasing_settings_threshold", "director_approval_threshold > 0"));
    }
}
