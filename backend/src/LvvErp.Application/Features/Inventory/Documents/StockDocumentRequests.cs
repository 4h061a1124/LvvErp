using LvvErp.Domain.Features.Inventory;

namespace LvvErp.Application.Features.Inventory.Documents;

// One request type per document kind keeps the API self-explanatory; all of them map onto
// StockDocumentDraft, and field names match the entity (WarehouseId = source for issue/transfer).

public sealed record ReceiptLineRequest(
    Guid MaterialId,
    Guid? LocationId,
    string? LotNumber,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    decimal Quantity,
    string? Note = null);

public sealed record SaveStockReceiptRequest(
    DateOnly DocumentDate,
    Guid WarehouseId,
    StockDocumentReason Reason,
    string? Partner,
    string? Note,
    IReadOnlyList<ReceiptLineRequest> Lines)
{
    public StockDocumentDraft ToDraft() => new(
        StockDocumentType.Receipt, DocumentDate, WarehouseId, null, Reason, Partner, null, null, Note,
        Lines.Select(l => new StockLineDraft(l.MaterialId, l.LocationId, null, null, l.LotNumber, l.ManufactureDate,
            l.ExpiryDate, l.Quantity, l.Note)).ToList());
}

public sealed record IssueLineRequest(Guid MaterialId, Guid? LocationId, Guid? LotId, decimal Quantity, string? Note = null);

public sealed record SaveStockIssueRequest(
    DateOnly DocumentDate,
    Guid WarehouseId,
    StockDocumentReason Reason,
    Guid? DepartmentId,
    Guid? EmployeeId,
    string? Note,
    IReadOnlyList<IssueLineRequest> Lines)
{
    public StockDocumentDraft ToDraft() => new(
        StockDocumentType.Issue, DocumentDate, WarehouseId, null, Reason, null, DepartmentId, EmployeeId, Note,
        Lines.Select(l => new StockLineDraft(l.MaterialId, l.LocationId, null, l.LotId, null, null, null, l.Quantity, l.Note))
            .ToList());
}

/// <param name="LocationId">Source location.</param>
/// <param name="ToLocationId">Destination location.</param>
public sealed record TransferLineRequest(
    Guid MaterialId,
    Guid? LotId,
    Guid? LocationId,
    Guid? ToLocationId,
    decimal Quantity,
    string? Note = null);

/// <param name="WarehouseId">Source warehouse.</param>
/// <param name="ToWarehouseId">Destination warehouse; may equal the source to move between locations.</param>
public sealed record SaveStockTransferRequest(
    DateOnly DocumentDate,
    Guid WarehouseId,
    Guid ToWarehouseId,
    string? Note,
    IReadOnlyList<TransferLineRequest> Lines)
{
    public StockDocumentDraft ToDraft() => new(
        StockDocumentType.Transfer, DocumentDate, WarehouseId, ToWarehouseId, null, null, null, null, Note,
        Lines.Select(l => new StockLineDraft(l.MaterialId, l.LocationId, l.ToLocationId, l.LotId, null, null, null,
            l.Quantity, l.Note)).ToList());
}

public sealed record CancelStockDocumentRequest(string Reason);

/// <summary>Normalized content of any document, validated and saved by <see cref="IStockDocumentService"/>.</summary>
public sealed record StockDocumentDraft(
    StockDocumentType Type,
    DateOnly DocumentDate,
    Guid WarehouseId,
    Guid? ToWarehouseId,
    StockDocumentReason? Reason,
    string? Partner,
    Guid? DepartmentId,
    Guid? EmployeeId,
    string? Note,
    IReadOnlyList<StockLineDraft> Lines);

public sealed record StockLineDraft(
    Guid MaterialId,
    Guid? LocationId,
    Guid? ToLocationId,
    Guid? LotId,
    string? LotNumber,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    decimal Quantity,
    string? Note);
