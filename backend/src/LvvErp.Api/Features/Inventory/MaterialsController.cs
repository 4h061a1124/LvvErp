using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Inventory;

[ApiController]
[Route("api/materials")]
[Authorize(Policy = Permissions.InventoryView)]
public sealed class MaterialsController(IMaterialService materials, IMaterialImportService import) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public Task<PagedResult<MaterialDto>> List([FromQuery] MaterialListQuery query, CancellationToken ct) =>
        materials.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<MaterialDto> Get(Guid id, CancellationToken ct) => materials.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<ActionResult<MaterialDto>> Create(SaveMaterialRequest request, CancellationToken ct)
    {
        var material = await materials.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = material.Id }, material);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public Task<MaterialDto> Update(Guid id, SaveMaterialRequest request, CancellationToken ct) =>
        materials.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await materials.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/photo")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    [RequestSizeLimit(PhotoRules.MaxBytes + 64 * 1024)]
    public async Task<MaterialDto> UploadPhoto(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await materials.SetPhotoAsync(id, stream, ct);
    }

    [HttpDelete("{id:guid}/photo")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken ct)
    {
        await materials.RemovePhotoAsync(id, ct);
        return NoContent();
    }

    [HttpGet("import/template")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct) =>
        File(await import.CreateTemplateAsync(ct), XlsxContentType, "mau-import-vat-tu.xlsx");

    /// <summary>Always 200 for a readable file; check <c>succeeded</c> and <c>errors</c> in the body.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    [RequestSizeLimit(ImportLimits.MaxFileBytes)]
    public async Task<ImportResult> Import(IFormFile file, [FromQuery] bool updateExisting, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream, updateExisting, ct);
    }
}
