namespace LvvErp.Application.Common.Persistence;

/// <summary>
/// Database functions usable inside LINQ queries. They are mapped to SQL in AppDbContext and
/// throw if called outside a query.
/// </summary>
public static class AppDbFunctions
{
    /// <summary>PostgreSQL <c>unaccent()</c>: removes diacritics ("Nguyễn Đức" → "Nguyen Duc").</summary>
    public static string Unaccent(string value) =>
        throw new InvalidOperationException($"{nameof(Unaccent)} can only be used in EF Core queries.");
}
