using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LvvErp.Infrastructure.Persistence;

public sealed class RowLocker(AppDbContext db) : IRowLocker
{
    public async Task LockAsync<TEntity>(IEnumerable<Guid> ids, CancellationToken ct = default) where TEntity : Entity
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Row locks must be taken inside a transaction.");

        var idArray = ids.Distinct().Order().ToArray();
        if (idArray.Length == 0) return;

        // Table name comes from the EF model, never from user input.
        var table = db.Model.FindEntityType(typeof(TEntity))?.GetTableName()
                    ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");
        var parameter = new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = idArray };
#pragma warning disable EF1002 // table name is from the model
        await db.Database.ExecuteSqlRawAsync($"SELECT 1 FROM \"{table}\" WHERE id = ANY(@ids) ORDER BY id FOR UPDATE", [parameter], ct);
#pragma warning restore EF1002
    }
}
