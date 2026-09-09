using Faed.Web.Services.Merchants;
using Xunit;

namespace Faed.Web.Tests.Services.Merchants;

public class VerificationDocumentValidatorTests
{
    [Fact]
    public void ValidatePayload_RejectsExecutableRenamedAsPdf()
    {
        // "MZ" DOS-stub header, as a real Windows .exe starts, with a fake PDF extension/content type.
        byte[] executableBytes = [(byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        var result = VerificationDocumentValidator.ValidatePayload(executableBytes, "application/pdf");

        Assert.True(result.Failed);
    }

    [Fact]
    public void ValidatePayload_AcceptsGenuinePdfSignature()
    {
        byte[] pdfBytes = "%PDF-1.7\n"u8.ToArray();

        var result = VerificationDocumentValidator.ValidatePayload(pdfBytes, "application/pdf");

        Assert.True(result.Succeeded);
    }
}
