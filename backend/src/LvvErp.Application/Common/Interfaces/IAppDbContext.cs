using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    DbSet<Unit> Units { get; }
    DbSet<MaterialGroup> MaterialGroups { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Location> Locations { get; }
    DbSet<Material> Materials { get; }
    DbSet<Lot> Lots { get; }
    DbSet<StockDocument> StockDocuments { get; }
    DbSet<StockDocumentLine> StockDocumentLines { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<StockBalance> StockBalances { get; }

    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchasingSettings> PurchasingSettings { get; }
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    DbSet<PurchaseRequestLine> PurchaseRequestLines { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<PurchaseOrderLineSource> PurchaseOrderLineSources { get; }

    /// <summary>For generic services over several entity types (e.g. simple catalogs).</summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Explicit transaction, for work that mixes SaveChanges with raw SQL (stock ledger).</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>The transaction in progress, if any (see DbTransactionScope).</summary>
    IDbContextTransaction? CurrentTransaction { get; }
}
