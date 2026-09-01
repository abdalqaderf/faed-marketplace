using System.Globalization;
using System.Text;

namespace Faed.Web.Services.Common;

/// <summary>
/// Turns merchant free text into a human-readable public identifier. Slugs are display and
/// routing identifiers only, never authorization keys (docs/06-ARCHITECTURE.md §12), so a
/// collision is resolved by appending a counter rather than by rejecting the input.
///
/// Merchant product text is Unicode-safe in storage; only the slug is reduced to ASCII,
/// because it appears in URLs (docs/02-SCOPE-AND-DECISIONS.md "English UI decision").
/// </summary>
public static class Slug
{
    public static string Create(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? fallback : slug;
    }

    /// <summary>Truncates a slug at a word boundary so a long title still yields a usable URL.</summary>
    public static string Truncate(string slug, int maxLength) =>
        slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
}
