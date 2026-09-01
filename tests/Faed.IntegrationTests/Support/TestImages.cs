namespace Faed.IntegrationTests.Support;

/// <summary>
/// A genuine 1x1 PNG that passes the fail-closed structural inspector
/// (docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md), reused for listing photo upload tests.
/// </summary>
internal static class TestImages
{
    public static byte[] MinimalPng { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    public static MemoryStream MinimalPngStream() => new(MinimalPng);
}
