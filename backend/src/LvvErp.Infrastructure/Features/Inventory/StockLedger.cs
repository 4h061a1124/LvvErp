using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LvvErp.Infrastructure.Features.Inventory;

/// <summary>
/// Balance changes are single atomic statements, so concurrent approvals can never lose an
/// update or drive stock below zero: a decrease only happens if <c>quantity &gt;= x</c> at that
/// instant (the row lock makes the second writer re-check after the first commits).
/// </summary>
public sealed class StockLedger(AppDbContext db) : IStockLedger
{
    public async Task LockDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM stock_documents WHERE id = {documentId} FOR UPDATE", ct);
    }

    public async Task<int> NextDocumentNumberAsync(string prefix, int year, CancellationToken ct = default)
    {
        EnsureTransaction();
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO document_sequences (prefix, year, last_number) VALUES ({prefix}, {year}, 1)
            ON CONFLICT (prefix, year) DO UPDATE SET last_number = document_sequences.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(ct);
        return numbers.Single();
    }

    public async Task IncreaseAsync(StockKey key, decimal quantity, CancellationToken ct = default)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO stock_balances (id, material_id, warehouse_id, location_id, lot_id, quantity, updated_at)
            VALUES ({Guid.CreateVersion7()}, {key.MaterialId}, {key.WarehouseId}, {Uuid("location_id", key.LocationId)},
                    {Uuid("lot_id", key.LotId)}, {quantity}, now())
            ON CONFLICT (material_id, warehouse_id, location_id, lot_id)
            DO UPDATE SET quantity = stock_balances.quantity + EXCLUDED.quantity, updated_at = now()
            """, ct);
    }

    public async Task<bool> TryDecreaseAsync(StockKey key, decimal quantity, CancellationToken ct = default)
    {
        EnsureTransaction();
        var rows = await db.Database.ExecuteSqlAsync($"""
            UPDATE stock_balances SET quantity = quantity - {quantity}, updated_at = now()
            WHERE material_id = {key.MaterialId} AND warehouse_id = {key.WarehouseId}
              AND location_id IS NOT DISTINCT FROM {Uuid("location_id", key.LocationId)}
              AND lot_id IS NOT DISTINCT FROM {Uuid("lot_id", key.LotId)}
              AND quantity >= {quantity}
            """, ct);
        return rows == 1;
    }

    // Typed parameter so Postgres knows a NULL is a uuid.
    private static NpgsqlParameter Uuid(string name, Guid? value) =>
        new($"@{name}", NpgsqlDbType.Uuid) { Value = value is null ? DBNull.Value : value.Value };

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stock ledger operations must run inside a transaction.");
    }
}
