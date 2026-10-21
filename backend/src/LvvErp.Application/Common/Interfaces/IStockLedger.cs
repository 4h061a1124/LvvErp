using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Common.Interfaces;

/// <summary>
/// Atomic SQL operations that EF change tracking can't express safely under concurrency.
/// Every method must run inside a transaction started with <see cref="IAppDbContext.BeginTransactionAsync"/>.
/// </summary>
public interface IStockLedger
{
    /// <summary>Row lock (SELECT … FOR UPDATE) on a document; concurrent writers wait until commit.</summary>
    Task LockDocumentAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Next number for the prefix and year, gap-free under concurrency (but not reused after delete).</summary>
    Task<int> NextDocumentNumberAsync(string prefix, int year, CancellationToken ct = default);

    Task IncreaseAsync(StockKey key, decimal quantity, CancellationToken ct = default);

    /// <summary>Subtracts only if enough stock is there; returns false (and changes nothing) otherwise.</summary>
    Task<bool> TryDecreaseAsync(StockKey key, decimal quantity, CancellationToken ct = default);
}
