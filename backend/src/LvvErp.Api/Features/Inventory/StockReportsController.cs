using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Inventory.Import;
using LvvErp.Application.Features.Inventory.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Inventory;

[ApiController]
[Route("api/stock")]
[Authorize(Policy = Permissions.InventoryView)]
public sealed class StockReportsController(IStockReportService reports, IOpeningBalanceImportService openingBalance) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Tồn kho hiện tại, one row per material.</summary>
    [HttpGet("on-hand")]
    public Task<PagedResult<StockOnHandDto>> OnHand([FromQuery] StockOnHandQuery query, CancellationToken ct) =>
        reports.GetOnHandAsync(query, ct);

    /// <summary>Breakdown of one material by warehouse, location and lot.</summary>
    [HttpGet("on-hand/{materialId:guid}")]
    public Task<IReadOnlyList<StockOnHandDetailDto>> OnHandDetail(Guid materialId, [FromQuery] Guid? warehouseId, CancellationToken ct) =>
        reports.GetOnHandDetailAsync(materialId, warehouseId, ct);

    [HttpGet("below-min")]
    public Task<IReadOnlyList<BelowMinStockDto>> BelowMin(CancellationToken ct) => reports.GetBelowMinAsync(ct);

    /// <summary>Thẻ kho.</summary>
    [HttpGet("card")]
    public Task<StockCardDto> Card([FromQuery] StockCardQuery query, CancellationToken ct) => reports.GetStockCardAsync(query, ct);

    [HttpGet("opening-balance/template")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    public async Task<IActionResult> OpeningBalanceTemplate(CancellationToken ct) =>
        File(await openingBalance.CreateTemplateAsync(ct), XlsxContentType, "mau-ton-dau-ky.xlsx");

    /// <summary>Creates draft opening-balance receipts. Always 200 for a readable file; check <c>succeeded</c>.</summary>
    [HttpPost("opening-balance/import")]
    [Authorize(Policy = Permissions.InventoryEditDocuments)]
    [RequestSizeLimit(ImportLimits.MaxFileBytes)]
    public async Task<OpeningBalanceImportResult> ImportOpeningBalance(
        IFormFile file, [FromQuery] DateOnly? documentDate, [FromQuery] string? note, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await openingBalance.ImportAsync(stream, documentDate, note, ct);
    }
}
