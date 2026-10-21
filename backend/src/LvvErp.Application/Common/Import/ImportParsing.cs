using System.Globalization;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Text;

namespace LvvErp.Application.Common.Import;

/// <summary>Lenient parsing of what people type into Excel.</summary>
public static class ImportParsing
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];

    /// <summary>
    /// Numbers: invariant ("12.5") or Vietnamese decimal comma ("12,5"). Values with both "." and ","
    /// are ambiguous ("1.234,5" vs "1,234.5") and rejected.
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().Replace(" ", "");
        if (s.Contains('.') && s.Contains(',')) return false;
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>yyyy-MM-dd (how real date cells are read) or day-first dd/MM/yyyy as used in Vietnam.</summary>
    public static bool TryParseDate(string? text, out DateOnly value) =>
        DateOnly.TryParseExact(text?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static readonly HashSet<string> Yes = ["co", "x", "1", "yes", "y", "true"];
    private static readonly HashSet<string> No = ["khong", "0", "no", "n", "false"];

    /// <summary>Có / Không, x, 1/0, yes/no, true/false.</summary>
    public static bool TryParseBool(string? text, out bool value)
    {
        var key = TextNormalizer.ToMatchKey(text);
        value = Yes.Contains(key);
        return value || No.Contains(key);
    }

    /// <summary>At most three decimals (decimal(18,3) columns).</summary>
    public static bool HasAtMostThreeDecimals(decimal value) => decimal.Round(value, 3) == value;
}

/// <summary>Matches user-typed text to a catalog entry by code or name, ignoring case and accents.</summary>
public sealed class LookupMatcher
{
    private readonly Dictionary<string, Guid> _byKey = new();

    public LookupMatcher(IEnumerable<LookupDto> items)
    {
        // Codes win over names when a name happens to equal another entry's code.
        var list = items.ToList();
        foreach (var item in list) _byKey.TryAdd(TextNormalizer.ToMatchKey(item.Code), item.Id);
        foreach (var item in list) _byKey.TryAdd(TextNormalizer.ToMatchKey(item.Name), item.Id);
    }

    public bool TryMatch(string? text, out Guid id) => _byKey.TryGetValue(TextNormalizer.ToMatchKey(text), out id);
}
