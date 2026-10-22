using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

/// <summary>Price history by material: GET api/purchase-orders/price-history.</summary>
[ApiController]
[Route("api/purchasing/reports")]
[Authorize(Policy = Permissions.PurchasingView)]
public sealed class PurchasingReportsController(IPurchasingReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<PurchasingSummaryDto> Summary([FromQuery] PurchasingSummaryQuery query, CancellationToken ct) =>
        reports.SummaryAsync(query, ct);

    [HttpGet("overdue-lines")]
    public Task<PagedResult<OverdueLineDto>> OverdueLines([FromQuery] OverdueLinesQuery query, CancellationToken ct) =>
        reports.OverdueLinesAsync(query, ct);
}
