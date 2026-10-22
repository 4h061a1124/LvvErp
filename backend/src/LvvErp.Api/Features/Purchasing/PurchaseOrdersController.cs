using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Policy = Permissions.PurchasingView)]
public sealed class PurchaseOrdersController(IPurchaseOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseOrderSummaryDto>> List([FromQuery] PurchaseOrderListQuery query, CancellationToken ct) =>
        orders.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<PurchaseOrderDto> Get(Guid id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(SavePurchaseOrderRequest request, CancellationToken ct)
    {
        var created = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public Task<PurchaseOrderDto> Update(Guid id, SavePurchaseOrderRequest request, CancellationToken ct) =>
        orders.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await orders.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public Task<PurchaseOrderDto> Submit(Guid id, CancellationToken ct) => orders.SubmitAsync(id, ct);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.PurchasingApproveOrders)]
    public Task<PurchaseOrderDto> Approve(Guid id, CancellationToken ct) => orders.ApproveAsync(id, ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Permissions.PurchasingApproveOrders)]
    public Task<PurchaseOrderDto> Reject(Guid id, ReasonRequest request, CancellationToken ct) =>
        orders.RejectAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public Task<PurchaseOrderDto> Cancel(Guid id, ReasonRequest request, CancellationToken ct) =>
        orders.CancelAsync(id, request, ct);

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public Task<PurchaseOrderDto> Close(Guid id, ReasonRequest request, CancellationToken ct) =>
        orders.CloseAsync(id, request, ct);

    /// <summary>Latest approved price per material, preferring <paramref name="supplierId"/>.</summary>
    [HttpGet("last-prices")]
    public Task<IReadOnlyList<LastPriceDto>> LastPrices([FromQuery] Guid[] materialIds, [FromQuery] Guid? supplierId, CancellationToken ct) =>
        orders.LastPricesAsync(materialIds, supplierId, ct);

    [HttpGet("price-history")]
    public Task<PagedResult<PriceHistoryDto>> PriceHistory([FromQuery] PriceHistoryQuery query, CancellationToken ct) =>
        orders.PriceHistoryAsync(query, ct);
}
