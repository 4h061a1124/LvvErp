using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

/// <summary>Department scope (own department unless Purchasing.View) and approver rules are checked in the service.</summary>
[ApiController]
[Route("api/purchase-requests")]
[Authorize(Policy = Permissions.PurchasingCreateRequests)]
public sealed class PurchaseRequestsController(IPurchaseRequestService requests) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseRequestSummaryDto>> List([FromQuery] PurchaseRequestListQuery query, CancellationToken ct) =>
        requests.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<PurchaseRequestDto> Get(Guid id, CancellationToken ct) => requests.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PurchaseRequestDto>> Create(SavePurchaseRequestRequest request, CancellationToken ct)
    {
        var created = await requests.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<PurchaseRequestDto> Update(Guid id, SavePurchaseRequestRequest request, CancellationToken ct) =>
        requests.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await requests.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public Task<PurchaseRequestDto> Submit(Guid id, CancellationToken ct) => requests.SubmitAsync(id, ct);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.PurchasingApproveRequests)]
    public Task<PurchaseRequestDto> Approve(Guid id, CancellationToken ct) => requests.ApproveAsync(id, ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Permissions.PurchasingApproveRequests)]
    public Task<PurchaseRequestDto> Reject(Guid id, ReasonRequest request, CancellationToken ct) =>
        requests.RejectAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<PurchaseRequestDto> Cancel(Guid id, ReasonRequest request, CancellationToken ct) =>
        requests.CancelAsync(id, request, ct);

    [HttpPost("{id:guid}/close")]
    public Task<PurchaseRequestDto> Close(Guid id, ReasonRequest request, CancellationToken ct) =>
        requests.CloseAsync(id, request, ct);

    /// <summary>Approved lines still to be ordered, for building a purchase order.</summary>
    [HttpGet("orderable-lines")]
    [Authorize(Policy = Permissions.PurchasingManageOrders)]
    public Task<PagedResult<OrderableRequestLineDto>> OrderableLines([FromQuery] OrderableLinesQuery query, CancellationToken ct) =>
        requests.ListOrderableLinesAsync(query, ct);
}
