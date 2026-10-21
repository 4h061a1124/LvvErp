namespace LvvErp.Application.Features.Inventory.Catalog;

/// <summary>Đơn vị tính / nhóm vật tư.</summary>
public sealed record CatalogItemDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record SaveCatalogItemRequest(string Code, string Name, bool IsActive = true);

public sealed record WarehouseDto(Guid Id, string Code, string Name, string? Note, bool IsActive, int LocationCount);

public sealed record SaveWarehouseRequest(string Code, string Name, string? Note, bool IsActive = true);

public sealed record LocationDto(Guid Id, Guid WarehouseId, string Code, string? Description, bool IsActive);

public sealed record SaveLocationRequest(string Code, string? Description, bool IsActive = true);
