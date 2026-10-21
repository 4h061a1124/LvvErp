using LvvErp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace LvvErp.Application.Common.Persistence;

/// <summary>
/// Starts a transaction, or joins the one already running. Only the scope that started it commits,
/// so a service can be used on its own or as one step of a larger all-or-nothing operation (imports).
/// </summary>
public sealed class DbTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _owned;

    private DbTransactionScope(IDbContextTransaction? owned) => _owned = owned;

    public static async Task<DbTransactionScope> BeginAsync(IAppDbContext db, CancellationToken ct = default) =>
        new(db.CurrentTransaction is null ? await db.BeginTransactionAsync(ct) : null);

    public Task CompleteAsync(CancellationToken ct = default) => _owned?.CommitAsync(ct) ?? Task.CompletedTask;

    /// <summary>Rolls back if <see cref="CompleteAsync"/> wasn't reached (owned transactions only).</summary>
    public ValueTask DisposeAsync() => _owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}
