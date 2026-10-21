using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Documents;

/// <summary>A signed quantity change at one stock key, caused by one document line.</summary>
public sealed record StockMovement(Guid LineId, int LineNo, StockKey Key, decimal Quantity);

/// <summary>Turns documents into ledger movements. Pure functions, no database.</summary>
public static class StockPosting
{
    /// <summary>Movements for approving <paramref name="document"/>. Receipt lines of lot-tracked materials must have LotId set.</summary>
    public static IReadOnlyList<StockMovement> ForApproval(StockDocument document)
    {
        var movements = new List<StockMovement>();
        foreach (var line in document.Lines.OrderBy(l => l.LineNo))
        {
            var source = new StockKey(line.MaterialId, document.WarehouseId, line.LocationId, line.LotId);
            switch (document.Type)
            {
                case StockDocumentType.Receipt:
                    movements.Add(new(line.Id, line.LineNo, source, line.Quantity));
                    break;
                case StockDocumentType.Issue:
                    movements.Add(new(line.Id, line.LineNo, source, -line.Quantity));
                    break;
                case StockDocumentType.Transfer:
                    var destination = new StockKey(line.MaterialId, document.ToWarehouseId!.Value, line.ToLocationId, line.LotId);
                    movements.Add(new(line.Id, line.LineNo, source, -line.Quantity));
                    movements.Add(new(line.Id, line.LineNo, destination, line.Quantity));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(document), document.Type, "Unknown document type.");
            }
        }
        return movements;
    }

    /// <summary>Exact opposite of what approval posted.</summary>
    public static IReadOnlyList<StockMovement> ForCancellation(
        IEnumerable<StockTransaction> posted, IReadOnlyDictionary<Guid, int> lineNoById) =>
        posted
            .Where(t => !t.IsReversal)
            .Select(t => new StockMovement(t.DocumentLineId, lineNoById.GetValueOrDefault(t.DocumentLineId), t.Key, -t.Quantity))
            .ToList();

    /// <summary>
    /// Order in which balance rows are touched: a single global order (by key) so two
    /// concurrent approvals never lock the same rows in opposite order.
    /// </summary>
    public static IReadOnlyList<StockMovement> InLockOrder(IEnumerable<StockMovement> movements) =>
        movements.OrderBy(m => m.Key).ThenBy(m => m.LineNo).ToList();
}
