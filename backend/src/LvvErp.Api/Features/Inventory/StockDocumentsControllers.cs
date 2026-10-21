using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Inventory;

/// <summary>Endpoints shared by the three document kinds; subclasses add create/update with their own body.</summary>
[ApiController]
[Authorize(Policy = Permissions.InventoryView)]
public abstract class StockDocumentsControllerBase(IStockDocumentService documents, StockDocumentType type) : ControllerBase
{
    protected IStockDocumentService Documents { get; } = documents;

    [HttpGet]
    public Task<PagedResult<StockDocumentSummaryDto>> List([FromQuery] StockDocumentListQuery query, CancellationToken ct) =>
        Documents.ListAsync(type, query, ct);

    [HttpGet("{id:guid}")]
    public Task<StockDocumentDto> Get(Guid id, CancellationToken ct) => Documents.GetAsync(id, type, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Documents.DeleteAsync(id, type, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.InventoryApproveDocuments)]
    public Task<StockDocumentDto> Approve(Guid id, CancellationToken ct) => Documents.ApproveAsync(id, type, ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.InventoryCancelDocuments)]
    public Task<StockDocumentDto> Cancel(Guid id, CancelStockDocumentRequest request, CancellationToken ct) =>
        Documents.CancelAsync(id, type, request, ct);

    protected async Task<ActionResult<StockDocumentDto>> CreatedDocument(StockDocumentDraft draft, CancellationToken ct)
    {
        var document = await Documents.CreateAsync(draft, ct);
        return CreatedAtAction(nameof(Get), new { id = document.Id }, document);
    }
}

[Route("api/stock-receipts")]
public sealed class StockReceiptsController(IStockDocumentService documents)
    : StockDocumentsControllerBase(documents, StockDocumentType.Receipt)
{
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<ActionResult<StockDocumentDto>> Create(SaveStockReceiptRequest request, CancellationToken ct) =>
        CreatedDocument(request.ToDraft(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<StockDocumentDto> Update(Guid id, SaveStockReceiptRequest request, CancellationToken ct) =>
        Documents.UpdateAsync(id, request.ToDraft(), ct);
}

[Route("api/stock-issues")]
public sealed class StockIssuesController(IStockDocumentService documents)
    : StockDocumentsControllerBase(documents, StockDocumentType.Issue)
{
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<ActionResult<StockDocumentDto>> Create(SaveStockIssueRequest request, CancellationToken ct) =>
        CreatedDocument(request.ToDraft(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<StockDocumentDto> Update(Guid id, SaveStockIssueRequest request, CancellationToken ct) =>
        Documents.UpdateAsync(id, request.ToDraft(), ct);
}

[Route("api/stock-transfers")]
public sealed class StockTransfersController(IStockDocumentService documents)
    : StockDocumentsControllerBase(documents, StockDocumentType.Transfer)
{
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<ActionResult<StockDocumentDto>> Create(SaveStockTransferRequest request, CancellationToken ct) =>
        CreatedDocument(request.ToDraft(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public Task<StockDocumentDto> Update(Guid id, SaveStockTransferRequest request, CancellationToken ct) =>
        Documents.UpdateAsync(id, request.ToDraft(), ct);
}

/// <summary>Any document by id (links from the stock card) and stock lookups used while filling in documents.</summary>
[ApiController]
[Authorize(Policy = Permissions.InventoryView)]
public sealed class StockController(IStockDocumentService documents, IStockQueryService stock) : ControllerBase
{
    [HttpGet("api/stock-documents/{id:guid}")]
    public Task<StockDocumentDto> GetDocument(Guid id, CancellationToken ct) => documents.GetAsync(id, ct: ct);

    [HttpGet("api/stock/available")]
    public Task<IReadOnlyList<AvailableStockDto>> Available([FromQuery] Guid materialId, [FromQuery] Guid warehouseId, CancellationToken ct) =>
        stock.GetAvailableAsync(materialId, warehouseId, ct);

    [HttpGet("api/materials/{materialId:guid}/lots")]
    public Task<IReadOnlyList<LotDto>> Lots(Guid materialId, CancellationToken ct) => stock.GetLotsAsync(materialId, ct);
}
