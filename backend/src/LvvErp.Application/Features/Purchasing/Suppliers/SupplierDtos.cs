namespace LvvErp.Application.Features.Purchasing.Suppliers;

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxCode,
    string? Address,
    string? ContactName,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    string? PaymentTerms,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class SupplierListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Code, tax code or name (accent-insensitive).</summary>
    public string? Search { get; init; }
    /// <summary>null = all, true = only active (for pickers).</summary>
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    /// <summary>code | name; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

public sealed record SaveSupplierRequest(
    string Code,
    string Name,
    string? TaxCode,
    string? Address,
    string? ContactName,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    string? PaymentTerms,
    bool IsActive = true);
