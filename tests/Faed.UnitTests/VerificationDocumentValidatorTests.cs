using System.Text;
using Faed.Application.Merchants;
using Faed.Domain.Enums;

namespace Faed.UnitTests;

/// <summary>Server-side upload validation (docs/08-SECURITY-AND-PRIVACY.md §3-4).</summary>
public class VerificationDocumentValidatorTests
{
    private static readonly MerchantVerificationOptions Options = new();

    private static AddVerificationDocumentInput Input(
        string fileName,
        string contentType,
        long length,
        MerchantVerificationDocumentType type = MerchantVerificationDocumentType.CommercialRegistration) =>
        new(type, Stream.Null, fileName, contentType, length);

    [Fact]
    public void Metadata_Accepts_Pdf_WithinLimit()
    {
        Assert.True(VerificationDocumentValidator.ValidateMetadata(Input("reg.pdf", "application/pdf", 2048), Options).Succeeded);
    }

    [Fact]
    public void Metadata_Rejects_EmptyFile()
    {
        Assert.True(VerificationDocumentValidator.ValidateMetadata(Input("reg.pdf", "application/pdf", 0), Options).Failed);
    }

    [Fact]
    public void Metadata_Rejects_OversizeFile()
    {
        Assert.True(VerificationDocumentValidator
            .ValidateMetadata(Input("reg.pdf", "application/pdf", Options.MaxDocumentBytes + 1), Options).Failed);
    }

    [Fact]
    public void Metadata_Rejects_DisallowedContentType()
    {
        Assert.True(VerificationDocumentValidator
            .ValidateMetadata(Input("reg.exe", "application/x-msdownload", 10), Options).Failed);
    }

    [Theory]
    [InlineData("reg.exe", "application/pdf")]
    [InlineData("photo.png", "image/jpeg")]
    [InlineData("scan.pdf", "image/png")]
    public void Metadata_Rejects_MismatchedContentTypeAndExtension(string fileName, string contentType)
    {
        Assert.True(VerificationDocumentValidator.ValidateMetadata(Input(fileName, contentType, 10), Options).Failed);
    }

    [Fact]
    public void Metadata_Rejects_UndefinedDocumentType()
    {
        var forged = (MerchantVerificationDocumentType)42;

        Assert.True(VerificationDocumentValidator
            .ValidateMetadata(Input("reg.pdf", "application/pdf", 10, forged), Options).Failed);
    }

    [Fact]
    public void Signature_Accepts_RealPdfHeader()
    {
        var header = Encoding.ASCII.GetBytes("%PDF-1.7");

        Assert.True(VerificationDocumentValidator.ValidateSignature(header, "application/pdf").Succeeded);
    }

    [Fact]
    public void Signature_Rejects_RenamedTextFileClaimingToBePdf()
    {
        var header = Encoding.ASCII.GetBytes("This is not a PDF");

        Assert.True(VerificationDocumentValidator.ValidateSignature(header, "application/pdf").Failed);
    }

    [Fact]
    public void Signature_Rejects_PngBytesClaimingToBeJpeg()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.True(VerificationDocumentValidator.ValidateSignature(png, "image/jpeg").Failed);
    }

    [Fact]
    public void Signature_Accepts_JpegAndPngHeaders()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.True(VerificationDocumentValidator.ValidateSignature(jpeg, "image/jpeg").Succeeded);
        Assert.True(VerificationDocumentValidator.ValidateSignature(png, "image/png").Succeeded);
    }
}
