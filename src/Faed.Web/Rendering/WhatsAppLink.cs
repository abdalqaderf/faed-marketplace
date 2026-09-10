namespace Faed.Web.Rendering;

/// <summary>
/// Builds a <c>wa.me</c> deep link with a pre-filled message — no API, no business account,
/// no approvals (docs/BUSINESS-MODEL.md §8.3). An ordinary link that opens WhatsApp on the
/// recipient's phone, the one app a small merchant keeps open all day.
/// </summary>
public static class WhatsAppLink
{
    /// <summary>
    /// A link to message <paramref name="phone"/> with <paramref name="message"/> pre-filled,
    /// or <c>null</c> when the number has too few digits to be dialled. The merchant enters
    /// their phone as free text, so the digits are normalised best-effort: a local
    /// <c>07xxxxxxxx</c> number is promoted to Jordan's <c>+962</c> international form, which is
    /// what <c>wa.me</c> requires (with no leading plus).
    /// </summary>
    public static string? Build(string? phone, string message)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
        {
            return null;
        }

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            digits = "962" + digits[1..];
        }

        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
    }
}
