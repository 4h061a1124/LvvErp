namespace LvvErp.Application.Features.Inventory.Catalog;

public sealed record MaterialDto(
    Guid Id,
    string Code,
    string Name,
    string? Specification,
    Guid UnitId,
    string UnitName,
    Guid GroupId,
    string GroupName,
    decimal MinStock,
    bool TrackLots,
    string? PhotoUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Query string of GET /api/materials.</summary>
public sealed class MaterialListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Matches code, name or specification (accent-insensitive).</summary>
    public string? Search { get; init; }
    public Guid? GroupId { get; init; }
    /// <summary>null = all, true = only active (for pickers), false = only inactive.</summary>
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>code | name | group | createdAt; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

public sealed record SaveMaterialRequest(
    string Code,
    string Name,
    string? Specification,
    Guid UnitId,
    Guid GroupId,
    decimal MinStock,
    bool TrackLots,
    bool IsActive = true);
