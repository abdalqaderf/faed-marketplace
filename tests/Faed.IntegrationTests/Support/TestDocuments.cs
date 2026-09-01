using System.Text;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// Byte fixtures for upload tests. The PDF is a complete classic-xref document with a
/// catalog, an empty page tree, correct object offsets, a trailer, <c>startxref</c> and
/// a terminal <c>%%EOF</c> marker, so it exercises the hardened validator's accepted path
/// (docs/08-SECURITY-AND-PRIVACY.md §3).
/// </summary>
internal static class TestDocuments
{
    public static byte[] MinimalPdf { get; } = BuildMinimalPdf();

    public static MemoryStream MinimalPdfStream() => new(MinimalPdf);

    private static byte[] BuildMinimalPdf()
    {
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));

        Write("%PDF-1.7\n");
        var catalogOffset = checked((int)pdf.Position);
        Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        var pagesOffset = checked((int)pdf.Position);
        Write("2 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\n");
        var xrefOffset = checked((int)pdf.Position);
        Write("xref\n0 3\n");
        Write("0000000000 65535 f \n");
        Write($"{catalogOffset:D10} 00000 n \n");
        Write($"{pagesOffset:D10} 00000 n \n");
        Write($"trailer\n<< /Size 3 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return pdf.ToArray();
    }
}
