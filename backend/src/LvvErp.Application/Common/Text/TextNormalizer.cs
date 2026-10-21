using System.Globalization;
using System.Text;

namespace LvvErp.Application.Common.Text;

public static class TextNormalizer
{
    /// <summary>
    /// Key for loose matching of user-typed text: trimmed, lower-case, Vietnamese diacritics
    /// removed, inner whitespace collapsed. "  Sản  Xuất " and "san xuat" give the same key.
    /// </summary>
    public static string ToMatchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace) sb.Append(' ');
                previousWasSpace = true;
                continue;
            }
            sb.Append(c);
            previousWasSpace = false;
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
