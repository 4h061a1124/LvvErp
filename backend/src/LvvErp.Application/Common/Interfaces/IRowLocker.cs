using LvvErp.Domain.Common;

namespace LvvErp.Application.Common.Interfaces;

/// <summary>Row locks (SELECT … FOR UPDATE) held until the current transaction ends.</summary>
public interface IRowLocker
{
    /// <summary>Locks the rows in id order, so callers locking overlapping sets cannot deadlock each other.</summary>
    Task LockAsync<TEntity>(IEnumerable<Guid> ids, CancellationToken ct = default) where TEntity : Entity;
}
