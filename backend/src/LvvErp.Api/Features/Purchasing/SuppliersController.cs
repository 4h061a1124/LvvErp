using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = Permissions.PurchasingView)]
public sealed class SuppliersController(ISupplierService suppliers, ISupplierImportService import) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public Task<PagedResult<SupplierDto>> List([FromQuery] SupplierListQuery query, CancellationToken ct) =>
        suppliers.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<SupplierDto> Get(Guid id, CancellationToken ct) => suppliers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.PurchasingManageSuppliers)]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierRequest request, CancellationToken ct)
    {
        var supplier = await suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.PurchasingManageSuppliers)]
    public Task<SupplierDto> Update(Guid id, SaveSupplierRequest request, CancellationToken ct) =>
        suppliers.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.PurchasingManageSuppliers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await suppliers.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("import/template")]
    [Authorize(Policy = Permissions.PurchasingManageSuppliers)]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct) =>
        File(await import.CreateTemplateAsync(ct), XlsxContentType, "mau-import-nha-cung-cap.xlsx");

    /// <summary>Always 200 for a readable file; check <c>succeeded</c> and <c>errors</c> in the body.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.PurchasingManageSuppliers)]
    [RequestSizeLimit(ImportLimits.MaxFileBytes)]
    public async Task<ImportResult> Import(IFormFile file, [FromQuery] bool updateExisting, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream, updateExisting, ct);
    }
}
