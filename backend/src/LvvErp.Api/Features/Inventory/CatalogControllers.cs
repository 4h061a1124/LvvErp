using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Domain.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Inventory;

/// <summary>Shared endpoints for code/name catalogs.</summary>
[ApiController]
[Authorize(Policy = Permissions.InventoryView)]
public abstract class CatalogControllerBase<T>(ICatalogService<T> catalog) : ControllerBase where T : CatalogEntity
{
    /// <param name="activeOnly">true for pickers (hides deactivated entries).</param>
    [HttpGet]
    public Task<IReadOnlyList<CatalogItemDto>> List([FromQuery] bool activeOnly, CancellationToken ct) =>
        catalog.ListAsync(activeOnly, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<ActionResult<CatalogItemDto>> Create(SaveCatalogItemRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public Task<CatalogItemDto> Update(Guid id, SaveCatalogItemRequest request, CancellationToken ct) =>
        catalog.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await catalog.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/units")]
public sealed class UnitsController(ICatalogService<Unit> catalog) : CatalogControllerBase<Unit>(catalog);

[Route("api/material-groups")]
public sealed class MaterialGroupsController(ICatalogService<MaterialGroup> catalog) : CatalogControllerBase<MaterialGroup>(catalog);
