using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Inventory.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Inventory;

[ApiController]
[Authorize(Policy = Permissions.InventoryView)]
public sealed class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet("api/warehouses")]
    public Task<IReadOnlyList<WarehouseDto>> List([FromQuery] bool activeOnly, CancellationToken ct) =>
        warehouses.ListAsync(activeOnly, ct);

    [HttpGet("api/warehouses/{id:guid}")]
    public Task<WarehouseDto> Get(Guid id, CancellationToken ct) => warehouses.GetAsync(id, ct);

    [HttpPost("api/warehouses")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<ActionResult<WarehouseDto>> Create(SaveWarehouseRequest request, CancellationToken ct)
    {
        var warehouse = await warehouses.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = warehouse.Id }, warehouse);
    }

    [HttpPut("api/warehouses/{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public Task<WarehouseDto> Update(Guid id, SaveWarehouseRequest request, CancellationToken ct) =>
        warehouses.UpdateAsync(id, request, ct);

    [HttpDelete("api/warehouses/{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await warehouses.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("api/warehouses/{warehouseId:guid}/locations")]
    public Task<IReadOnlyList<LocationDto>> ListLocations(Guid warehouseId, [FromQuery] bool activeOnly, CancellationToken ct) =>
        warehouses.ListLocationsAsync(warehouseId, activeOnly, ct);

    [HttpPost("api/warehouses/{warehouseId:guid}/locations")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<ActionResult<LocationDto>> CreateLocation(Guid warehouseId, SaveLocationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await warehouses.CreateLocationAsync(warehouseId, request, ct));

    [HttpPut("api/locations/{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public Task<LocationDto> UpdateLocation(Guid id, SaveLocationRequest request, CancellationToken ct) =>
        warehouses.UpdateLocationAsync(id, request, ct);

    [HttpDelete("api/locations/{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManageCatalog)]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken ct)
    {
        await warehouses.DeleteLocationAsync(id, ct);
        return NoContent();
    }
}
