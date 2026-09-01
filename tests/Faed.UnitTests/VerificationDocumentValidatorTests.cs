using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Merchants;

namespace Faed.UnitTests;

/// <summary>Server-side upload validation (docs/08-SECURITY-AND-PRIVACY.md §3-4).</summary>
public class VerificationDocumentValidatorTests
{
    private static readonly MerchantVerificationOptions Options = new();

    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static readonly byte[] ValidJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMU" +
        "FRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQU" +
        "FBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUF" +
        "BAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVW" +
        "V1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi" +
        "4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
        "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVm" +
        "Z2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq" +
        "8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD9U6KKKAP/2Q==");

    private static readonly byte[] EmptyZip =
    [
        0x50, 0x4B, 0x05, 0x06,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
    ];

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
        Assert.True(VerificationDocumentValidator.ValidateSignature(ValidPng, "image/jpeg").Failed);
    }

    [Fact]
    public void Signature_Accepts_JpegAndPngHeaders()
    {
        Assert.True(VerificationDocumentValidator.ValidateSignature(ValidJpeg, "image/jpeg").Succeeded);
        Assert.True(VerificationDocumentValidator.ValidateSignature(ValidPng, "image/png").Succeeded);
    }

    [Fact]
    public void Payload_Accepts_CompleteMinimalPdf()
    {
        var pdf = MinimalPdf();

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_PdfHeaderWithoutEofMarker()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_PdfWithBytesAfterEofMarker()
    {
        var pdf = MinimalPdf().Concat("PK\u0003\u0004archive"u8.ToArray()).ToArray();

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_PdfThatEmbedsJavaScript()
    {
        var pdf = BuildPdfWithObject("<< /Type /Action /S /JavaScript /JS (app.alert('x')) >>");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_PdfWithLaunchAction()
    {
        var pdf = BuildPdfWithObject("<< /S /Launch /F (calc.exe) >>");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_RenamedExecutableClaimingToBePdf()
    {
        byte[] executable = [(byte)'M', (byte)'Z', 0x90, 0x00, 0x01];

        Assert.True(VerificationDocumentValidator.ValidatePayload(executable, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptHiddenBehindPdfNameHexEscapes()
    {
        var pdf = BuildPdfWithObject("<< /S /Java#53cript /JS (app.alert('x')) >>");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptHiddenInAFlateDecodeStream()
    {
        var hiddenObject = Encoding.ASCII.GetBytes("<< /S /JavaScript /JS (app.alert\\('x'\\)) >>");
        var pdf = BuildPdfWithStream("/FlateDecode", hiddenObject, compress: true);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_ScriptMarkerHiddenInAFlateDecodeStream()
    {
        var hiddenScript = Encoding.ASCII.GetBytes("<ScRiPt>alert('x')</script>");
        var pdf = BuildPdfWithStream("/FlateDecode", hiddenScript, compress: true);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptInAStreamWhoseFilterNameIsHexEscaped()
    {
        var hiddenObject = Encoding.ASCII.GetBytes("<< /S /JavaScript /JS (x) >>");
        var pdf = BuildPdfWithStream("/Flate#44ecode", hiddenObject, compress: true);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptWhenFilterIsMoreThan2048BytesBeforeStream()
    {
        var hiddenObject = Encoding.ASCII.GetBytes("<< /S /JavaScript /JS (x) >>");
        var pdf = BuildPdfWithStream("/FlateDecode", hiddenObject, compress: true, dictionaryPadding: 4096);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_FlateStreamThatWillNotInflate()
    {
        var pdf = BuildPdfWithStream("/FlateDecode", Encoding.ASCII.GetBytes("not deflate data"), compress: false);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_EncryptedPdf()
    {
        var pdf = BuildPdfWithObject("<< /Encrypt 9 0 R >>");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_PdfUsingLzwCompressionWithoutBinarySourceCharacters()
    {
        var opaqueBytes = new byte[4];
        var pdf = BuildPdfWithStream("/LZWDecode", opaqueBytes, compress: false);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_PdfWhoseDeclaredPredictorDoesNotDecode()
    {
        var clean = Encoding.ASCII.GetBytes("registration");
        var pdf = BuildPdfWithStream(
            "/FlateDecode",
            clean,
            compress: true,
            extraDictionary: "/DecodeParms<</Predictor 12/Columns 32>>");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_DecodeParametersWithAnUnrecognisedKey()
    {
        var pdf = BuildPdfWithPredictedStream(
            PadToRows("BT (registration) Tj ET", columns: 8),
            columns: 8,
            extraParameters: " /Unsupported 1");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Accepts_PredictorEncodedStreamWithNoActiveContent()
    {
        var pdf = BuildPdfWithPredictedStream(PadToRows("BT (registration) Tj ET", columns: 8), columns: 8);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptVisibleOnlyAfterUndoingThePredictor()
    {
        // `/JavaScript` straddles a predictor row boundary, so a filter byte splits it in the
        // inflated bytes. Only a scan of the un-predicted view — what a reader actually sees —
        // finds it.
        var payload = PadToRows("        /JavaScript (app.alert)", columns: 8);
        var pdf = BuildPdfWithPredictedStream(payload, columns: 8);

        // Ordinal, byte-wise — the comparison the validator itself performs. A culture-sensitive
        // string search would treat the intervening filter byte as ignorable and match anyway.
        Assert.True(
            PngPredict(payload, columns: 8).AsSpan().IndexOf("/JavaScript"u8) < 0,
            "Precondition: the marker must be split across rows in the inflated view.");
        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Accepts_CleanContentBehindAHexEscapedFlateFilterName()
    {
        // The rejecting twin of this test cannot tell "escape resolved, stream inflated and
        // scanned" apart from "filter unrecognised, rejected". This one can.
        var contentStream = Encoding.ASCII.GetBytes("BT /F1 12 Tf (Commercial registration) Tj ET");
        var pdf = BuildPdfWithStream("/Flate#44ecode", contentStream, compress: true);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Accepts_IncrementallySavedPdfWithSeveralEofMarkers()
    {
        var pdf = AppendPdfRevision(MinimalPdf(), "<< /Type /Metadata >>"u8.ToArray());

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_JavaScriptAddedByAnAppendedPdfRevision()
    {
        var pdf = AppendPdfRevision(
            MinimalPdf(),
            "<< /Type /Action /S /JavaScript /JS (app.alert('x')) >>"u8.ToArray());

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Accepts_CrossReferenceStreamPdf()
    {
        Assert.True(VerificationDocumentValidator.ValidatePayload(
            BuildCrossReferenceStreamPdf(), "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_EscapedNameDelimiterThatCouldHideFilterSyntax()
    {
        var body = Encoding.ASCII.GetBytes("opaque payload");
        var dictionary = $"/Length {body.Length} /Foo#28 1 /Filter /ASCII85Decode /Bar#29 2";
        var pdf = BuildPdfWithCustomStreamDictionary(dictionary, body);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Theory]
    [InlineData("/F (external.bin)")]
    [InlineData("/FFilter /FlateDecode")]
    [InlineData("/FDecodeParms << /Predictor 12 >>")]
    public void Payload_Rejects_ExternalStreamSourcesAndFilters(string externalStreamEntry)
    {
        var body = Encoding.ASCII.GetBytes("opaque payload");
        var pdf = BuildPdfWithCustomStreamDictionary($"/Length {body.Length} {externalStreamEntry}", body);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_FlateMemberWithAppendedArchiveInsideStreamLength()
    {
        var body = Compress("clean"u8.ToArray()).Concat(EmptyZip).ToArray();
        var pdf = BuildPdfWithStream("/FlateDecode", body, compress: false);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_FlateMemberWithTrailingBytesAndDuplicatedAdlerChecksum()
    {
        var compressed = Compress("clean"u8.ToArray());
        var body = compressed
            .Concat("trailing"u8.ToArray())
            .Concat(compressed[^4..])
            .ToArray();
        var pdf = BuildPdfWithStream("/FlateDecode", body, compress: false);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_StreamWhoseDirectLengthDoesNotMatchEndstreamBoundary()
    {
        var body = Encoding.ASCII.GetBytes("registration");
        var pdf = BuildPdfWithCustomStreamDictionary($"/Length {body.Length - 1}", body);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_ArchiveOutsideAStreamBeforeXref()
    {
        var pdf = BuildPdf([EmptyZip]);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Rejects_WhenPdfStreamInspectionBudgetIsExhausted()
    {
        var pdf = BuildPdfWithManyCompressedStreams(513);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Accepts_FlateDecodeStreamWithNoActiveContent()
    {
        var contentStream = Encoding.ASCII.GetBytes("BT /F1 12 Tf (Commercial registration) Tj ET");
        var pdf = BuildPdfWithStream("/FlateDecode", contentStream, compress: true);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Accepts_PdfWithAnImageOnlyStream()
    {
        var pdf = BuildPdfWithStream(
            "/DCTDecode",
            ValidJpeg,
            compress: false,
            extraDictionary: "/Type/XObject/Subtype/Image");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_DctFilterOnAnObjectStream()
    {
        var pdf = BuildPdfWithStream(
            "/DCTDecode",
            ValidJpeg,
            compress: false,
            extraDictionary: "/Type/ObjStm");

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Failed);
    }

    [Fact]
    public void Payload_Accepts_PdfWhereACompressedStreamIsFollowedByAnUncompressedOne()
    {
        var compressedBody = Compress(Encoding.ASCII.GetBytes("<< /Type /ObjStm >> 1 0 (registration)"));
        var contentBody = Encoding.ASCII.GetBytes("BT (Commercial registration) Tj ET");
        var pdf = BuildPdf(
        [
            BuildStreamObject($"/Filter /FlateDecode /Length {compressedBody.Length}", compressedBody),
            BuildStreamObject($"/Length {contentBody.Length}", contentBody),
        ]);

        Assert.True(VerificationDocumentValidator.ValidatePayload(pdf, "application/pdf").Succeeded);
    }

    [Fact]
    public void Payload_Accepts_StructurallyValidJpegAndPng()
    {
        Assert.True(VerificationDocumentValidator.ValidatePayload(ValidJpeg, "image/jpeg").Succeeded);
        Assert.True(VerificationDocumentValidator.ValidatePayload(ValidPng, "image/png").Succeeded);
    }

    [Theory]
    [InlineData("image/jpeg", "PK\u0003\u0004archive")]
    [InlineData("image/jpeg", "MZexecutable")]
    [InlineData("image/png", "PK\u0003\u0004archive")]
    [InlineData("image/png", "MZexecutable")]
    public void Payload_Rejects_ImageWithAppendedArchiveOrExecutable(string contentType, string suffix)
    {
        var image = contentType == "image/jpeg" ? ValidJpeg : ValidPng;
        var polyglot = image.Concat(Encoding.Latin1.GetBytes(suffix)).ToArray();

        Assert.True(VerificationDocumentValidator.ValidatePayload(polyglot, contentType).Failed);
    }

    [Fact]
    public void Payload_Rejects_JpegWithArchiveBeforeTerminalEoi()
    {
        var polyglot = ValidJpeg[..^2]
            .Concat(EmptyZip)
            .Concat(new byte[] { 0xFF, 0xD9 })
            .ToArray();

        Assert.True(VerificationDocumentValidator.ValidatePayload(polyglot, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_JpegWithExecutableBeforeTerminalEoi()
    {
        var polyglot = ValidJpeg[..^2]
            .Concat(MinimalPe())
            .Concat(new byte[] { 0xFF, 0xD9 })
            .ToArray();

        Assert.True(VerificationDocumentValidator.ValidatePayload(polyglot, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_PngWithArchiveInsideIdatAndValidChunkCrc()
    {
        var polyglot = AppendToPngIdat(ValidPng, EmptyZip);

        Assert.True(VerificationDocumentValidator.ValidatePayload(polyglot, "image/png").Failed);
    }

    [Fact]
    public void Payload_Accepts_PngCarryingTextAndVendorAncillaryChunks()
    {
        // What every real PNG export looks like: a software tEXt note, a colour profile and a
        // provenance chunk Faed does not model. All three are ancillary, so a decoder may
        // ignore them and Faed accepts them.
        var png = InsertPngChunkAfterHeader(ValidPng, "tEXt", PngKeywordPayload("Software", "Faed Test"u8.ToArray()));
        png = InsertPngChunkAfterHeader(png, "iCCP", PngKeywordPayload("ICC", [0x00, .. Compress("profile"u8.ToArray())]));
        png = InsertPngChunkAfterHeader(png, "caBX", "provenance"u8.ToArray());

        Assert.True(VerificationDocumentValidator.ValidatePayload(png, "image/png").Succeeded);
    }

    [Fact]
    public void Payload_Rejects_PngWithAScriptMarkerHiddenInACompressedTextChunk()
    {
        var compressed = Compress("<ScRiPt>alert('x')</script>"u8.ToArray());
        var png = InsertPngChunkAfterHeader(
            ValidPng, "zTXt", PngKeywordPayload("Comment", [0x00, .. compressed]));

        Assert.True(VerificationDocumentValidator.ValidatePayload(png, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_PngWithAnArchiveHiddenInACompressedInternationalTextChunk()
    {
        var compressed = Compress(EmptyZip);
        var payload = new List<byte>();
        payload.AddRange("Comment"u8.ToArray());
        payload.Add(0x00);
        payload.Add(0x01);
        payload.Add(0x00);
        payload.Add(0x00);
        payload.Add(0x00);
        payload.AddRange(compressed);
        var png = InsertPngChunkAfterHeader(ValidPng, "iTXt", payload.ToArray());

        Assert.True(VerificationDocumentValidator.ValidatePayload(png, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_PngWithACompressedTextChunkThatWillNotInflate()
    {
        var png = InsertPngChunkAfterHeader(
            ValidPng, "zTXt", PngKeywordPayload("Comment", [0x00, .. "not deflate data"u8.ToArray()]));

        Assert.True(VerificationDocumentValidator.ValidatePayload(png, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_PngWithAnUnknownCriticalChunk()
    {
        var png = InsertPngChunkAfterHeader(ValidPng, "CaBX", "provenance"u8.ToArray());

        Assert.True(VerificationDocumentValidator.ValidatePayload(png, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_PngWithInvalidChunkCrc()
    {
        var malformed = ValidPng.ToArray();
        malformed[29] ^= 0x01;

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_OneBitPngWhoseExpandedDimensionsExceedBudget()
    {
        var malformed = BuildOneBitGrayscalePng(width: ushort.MaxValue, height: 1025);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/png").Failed);
    }

    [Fact]
    public void Payload_Rejects_TruncatedJpeg()
    {
        var malformed = ValidJpeg[..^2];

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_StructuredJpegWithZeroQuantizationValue()
    {
        var malformed = BuildStructuredJpeg(zeroQuantizationValue: true, scanComponentId: 1, spectralEnd: 63);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_StructuredJpegWhoseScanReferencesUnknownFrameComponent()
    {
        var malformed = BuildStructuredJpeg(zeroQuantizationValue: false, scanComponentId: 2, spectralEnd: 63);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_SequentialJpegWithProgressiveScanFields()
    {
        var malformed = BuildStructuredJpeg(zeroQuantizationValue: false, scanComponentId: 1, spectralEnd: 0);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_JpegWhoseDecodedDimensionsExceedBudget()
    {
        var malformed = BuildStructuredJpeg(
            zeroQuantizationValue: false,
            scanComponentId: 1,
            spectralEnd: 63,
            width: ushort.MaxValue,
            height: ushort.MaxValue);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    [Fact]
    public void Payload_Rejects_JpegScanWithoutEntropyData()
    {
        var malformed = BuildStructuredJpeg(
            zeroQuantizationValue: false,
            scanComponentId: 1,
            spectralEnd: 63,
            includeEntropyByte: false);

        Assert.True(VerificationDocumentValidator.ValidatePayload(malformed, "image/jpeg").Failed);
    }

    private static byte[] MinimalPdf() => BuildPdf([]);

    private static byte[] BuildPdfWithObject(string objectBody) =>
        BuildPdf([Encoding.ASCII.GetBytes(objectBody)]);

    private static byte[] BuildPdfWithStream(
        string filterToken,
        byte[] streamBody,
        bool compress,
        int dictionaryPadding = 0,
        string extraDictionary = "")
    {
        var body = compress ? Compress(streamBody) : streamBody;
        var padding = dictionaryPadding == 0
            ? string.Empty
            : $" /Padding ({new string('A', dictionaryPadding)})";
        var dictionary = $"/Filter {filterToken} /Length {body.Length} {extraDictionary}{padding}";
        return BuildPdf([BuildStreamObject(dictionary, body)]);
    }

    private static byte[] BuildPdfWithCustomStreamDictionary(string dictionary, byte[] body) =>
        BuildPdf([BuildStreamObject(dictionary, body)]);

    private static byte[] BuildPdfWithManyCompressedStreams(int count)
    {
        var body = Compress("clean"u8.ToArray());
        var objects = new List<byte[]>(count);
        for (var index = 1; index <= count; index++)
        {
            objects.Add(BuildStreamObject($"/Filter /FlateDecode /Length {body.Length}", body));
        }

        return BuildPdf(objects);
    }

    private static byte[] BuildStreamObject(string dictionary, byte[] body)
    {
        using var streamObject = new MemoryStream();
        streamObject.Write(Encoding.ASCII.GetBytes($"<< {dictionary} >>\nstream\n"));
        streamObject.Write(body);
        streamObject.Write("\nendstream"u8);
        return streamObject.ToArray();
    }

    private static byte[] BuildPdf(IReadOnlyList<byte[]> additionalObjects)
    {
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));

        Write("%PDF-1.7\n");
        var offsets = new List<int>(additionalObjects.Count + 2);

        void WriteObject(int objectNumber, byte[] body)
        {
            offsets.Add(checked((int)pdf.Position));
            Write($"{objectNumber} 0 obj\n");
            pdf.Write(body);
            Write("\nendobj\n");
        }

        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray());
        WriteObject(2, "<< /Type /Pages /Count 0 /Kids [] >>"u8.ToArray());
        for (var index = 0; index < additionalObjects.Count; index++)
        {
            WriteObject(index + 3, additionalObjects[index]);
        }

        var xrefOffset = checked((int)pdf.Position);
        Write($"xref\n0 {offsets.Count + 1}\n");
        Write("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:D10} 00000 n \n");
        }

        Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return pdf.ToArray();
    }

    /// <summary>Pads to a whole number of predictor rows so every row can use filter type 0.</summary>
    private static byte[] PadToRows(string text, int columns)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        var padded = new byte[((bytes.Length + columns - 1) / columns) * columns];
        padded.AsSpan().Fill((byte)' ');
        bytes.CopyTo(padded, 0);
        return padded;
    }

    /// <summary>Wraps each row in a PNG "None" filter byte — the encoding a predictor undoes.</summary>
    private static byte[] PngPredict(byte[] rows, int columns)
    {
        using var encoded = new MemoryStream();
        for (var offset = 0; offset < rows.Length; offset += columns)
        {
            encoded.WriteByte(0x00);
            encoded.Write(rows, offset, columns);
        }

        return encoded.ToArray();
    }

    private static byte[] BuildPdfWithPredictedStream(byte[] rows, int columns, string extraParameters = "")
    {
        var body = Compress(PngPredict(rows, columns));
        var dictionary =
            $"/Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns {columns}{extraParameters} >> " +
            $"/Length {body.Length}";
        return BuildPdf([BuildStreamObject(dictionary, body)]);
    }

    /// <summary>Appends a second revision the way an incremental save does.</summary>
    private static byte[] AppendPdfRevision(byte[] basePdf, byte[] objectBody)
    {
        var previousXref = Encoding.Latin1.GetString(basePdf).LastIndexOf("xref", StringComparison.Ordinal);
        using var pdf = new MemoryStream();
        pdf.Write(basePdf);
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));

        var objectOffset = checked((int)pdf.Position);
        Write("9 0 obj\n");
        pdf.Write(objectBody);
        Write("\nendobj\n");

        var xrefOffset = checked((int)pdf.Position);
        Write("xref\n0 1\n0000000000 65535 f \n9 1\n");
        Write($"{objectOffset:D10} 00000 n \n");
        Write($"trailer\n<< /Size 10 /Root 1 0 R /Prev {previousXref} >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return pdf.ToArray();
    }

    /// <summary>A PDF 1.5 document whose cross-reference is a predictor-encoded Flate stream.</summary>
    private static byte[] BuildCrossReferenceStreamPdf()
    {
        var body = Compress(PngPredict(new byte[16], columns: 4));
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));

        Write("%PDF-1.5\n");
        Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Write("2 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\n");

        var xrefOffset = checked((int)pdf.Position);
        Write("3 0 obj\n<< /Type /XRef /Size 4 /Root 1 0 R /W [1 2 1] /Filter /FlateDecode " +
              $"/DecodeParms << /Predictor 12 /Columns 4 >> /Length {body.Length} >>\nstream\n");
        pdf.Write(body);
        Write("\nendstream\nendobj\n");
        Write($"startxref\n{xrefOffset}\n%%EOF\n");
        return pdf.ToArray();
    }

    private static byte[] PngKeywordPayload(string keyword, byte[] rest) =>
        [.. Encoding.ASCII.GetBytes(keyword), 0x00, .. rest];

    private static byte[] InsertPngChunkAfterHeader(byte[] png, string type, byte[] data)
    {
        // IHDR is always the first chunk: 8-byte signature + 12-byte envelope + 13-byte payload.
        const int afterHeader = 8 + 12 + 13;
        using var output = new MemoryStream();
        output.Write(png.AsSpan(0, afterHeader));
        WritePngChunk(output, Encoding.ASCII.GetBytes(type), data);
        output.Write(png.AsSpan(afterHeader));
        return output.ToArray();
    }

    private static byte[] AppendToPngIdat(byte[] png, byte[] suffix)
    {
        var position = 8;
        while (position + 12 <= png.Length)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position, 4)));
            if (png.AsSpan(position + 4, 4).SequenceEqual("IDAT"u8))
            {
                var extendedData = png.AsSpan(position + 8, length).ToArray().Concat(suffix).ToArray();
                using var output = new MemoryStream();
                output.Write(png.AsSpan(0, position));
                Span<byte> lengthBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)extendedData.Length);
                output.Write(lengthBytes);
                output.Write("IDAT"u8);
                output.Write(extendedData);

                var crcInput = "IDAT"u8.ToArray().Concat(extendedData).ToArray();
                Span<byte> crcBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeTestCrc32(crcInput));
                output.Write(crcBytes);
                output.Write(png.AsSpan(position + 12 + length));
                return output.ToArray();
            }

            position += 12 + length;
        }

        throw new InvalidOperationException("The PNG fixture has no IDAT chunk.");
    }

    private static byte[] BuildOneBitGrayscalePng(uint width, uint height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), height);
        header[8] = 1;

        var rowBytes = checked((int)((width + 7) / 8));
        var scanlines = new byte[checked((rowBytes + 1) * (int)height)];
        var compressed = Compress(scanlines);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WritePngChunk(png, "IHDR"u8, header);
        WritePngChunk(png, "IDAT"u8, compressed);
        WritePngChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        var crcInput = type.ToArray().Concat(data).ToArray();
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeTestCrc32(crcInput));
        output.Write(crc);
    }

    private static byte[] MinimalPe()
    {
        var executable = new byte[88];
        executable[0] = (byte)'M';
        executable[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(executable.AsSpan(0x3C, 4), 0x40);
        "PE\0\0"u8.CopyTo(executable.AsSpan(0x40));
        return executable;
    }

    private static byte[] BuildStructuredJpeg(
        bool zeroQuantizationValue,
        byte scanComponentId,
        byte spectralEnd,
        ushort width = 1,
        ushort height = 1,
        bool includeEntropyByte = true)
    {
        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8]);

        var quantization = Enumerable.Repeat((byte)1, 65).ToArray();
        quantization[0] = 0;
        if (zeroQuantizationValue)
        {
            quantization[1] = 0;
        }

        WriteJpegSegment(jpeg, 0xDB, quantization);
        var frame = new byte[9];
        frame[0] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1, 2), height);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(3, 2), width);
        frame[5] = 1;
        frame[6] = 1;
        frame[7] = 0x11;
        frame[8] = 0;
        WriteJpegSegment(jpeg, 0xC0, frame);

        var huffman = new byte[36];
        huffman[0] = 0;
        huffman[1] = 1;
        huffman[17] = 0;
        huffman[18] = 0x10;
        huffman[19] = 1;
        huffman[35] = 0;
        WriteJpegSegment(jpeg, 0xC4, huffman);
        WriteJpegSegment(jpeg, 0xDA, [1, scanComponentId, 0, 0, spectralEnd, 0]);
        if (includeEntropyByte)
        {
            jpeg.WriteByte(0);
        }

        jpeg.Write([0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    private static void WriteJpegSegment(Stream output, byte marker, byte[] data)
    {
        output.WriteByte(0xFF);
        output.WriteByte(marker);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)(data.Length + 2)));
        output.Write(length);
        output.Write(data);
    }

    private static uint ComputeTestCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(bytes);
        }

        return buffer.ToArray();
    }
}
