using System.Buffers.Binary;
using System.IO.Compression;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;

namespace Faed.Web.Services.Merchants;

/// <summary>
/// Server-side validation of an uploaded verification document
/// (docs/08-SECURITY-AND-PRIVACY.md §3-4). The client-supplied file name is never trusted
/// for storage; the client-supplied content type and extension are trusted only after
/// they agree with each other <em>and</em> with the file's actual structure.
/// </summary>
public static class VerificationDocumentValidator
{
    private static readonly IReadOnlyDictionary<string, string[]> ContentTypeExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
        };

    /// <summary>Number of leading bytes needed to recognise every supported signature.</summary>
    public const int SignatureProbeBytes = 12;

    private const int MaxInflatedPdfScanBytes = 64 * 1024 * 1024;
    private const int MaxInspectedPdfStreams = 512;
    private const int MaxPdfStreamDictionaryBytes = 1024 * 1024;
    private const int MaxPdfPredictorRowBytes = 1024 * 1024;
    private const int MaxInflatedImageBytes = 64 * 1024 * 1024;
    private const int MaxInflatedPngAncillaryBytes = 16 * 1024 * 1024;

    /// <summary>Validates the declared metadata (type, size, content type / extension pairing).</summary>
    public static Result ValidateMetadata(AddVerificationDocumentInput input, MerchantVerificationOptions options)
    {
        if (!Enum.IsDefined(input.DocumentType))
        {
            return Result.Validation("Choose a valid document type.");
        }

        if (input.LengthBytes <= 0)
        {
            return Result.Validation("The file is empty.");
        }

        if (input.LengthBytes > options.MaxDocumentBytes)
        {
            return Result.Validation($"The file exceeds the {MaxMegabytes(options)} MB limit.");
        }

        var contentType = (input.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!options.AllowedContentTypes.Contains(contentType)
            || !ContentTypeExtensions.TryGetValue(contentType, out var extensions))
        {
            return Result.Validation("Only PDF, JPG and PNG documents are accepted.");
        }

        var extension = Path.GetExtension(input.OriginalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension)
            || !extensions.Contains(extension)
            || !options.AllowedExtensions.Contains(extension))
        {
            return Result.Validation("The file extension does not match its type. Use a genuine PDF, JPG or PNG.");
        }

        return Result.Success();
    }

    /// <summary>Checks only the leading signature. Full uploads must also call <see cref="ValidatePayload"/>.</summary>
    public static Result ValidateSignature(ReadOnlySpan<byte> header, string contentType)
    {
        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var matches = normalized switch
        {
            "application/pdf" => StartsWith(header, "%PDF-"u8),
            "image/png" => StartsWith(header, PngSignature),
            "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            _ => false,
        };

        return matches
            ? Result.Success()
            : Result.Validation("The file content is not a valid PDF, JPG or PNG.");
    }

    /// <summary>
    /// Performs bounded, fail-closed structural inspection of the complete buffered upload.
    /// See docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md.
    /// </summary>
    public static Result ValidatePayload(ReadOnlySpan<byte> content, string contentType)
    {
        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var signature = ValidateSignature(content, normalized);
        if (signature.Failed)
        {
            return signature;
        }

        if (ContainsScriptMarker(content))
        {
            return ScriptContentFailure();
        }

        if (normalized is "image/jpeg" or "image/png" && ContainsDisallowedBinaryPayload(content))
        {
            return InvalidImageFailure();
        }

        return normalized switch
        {
            "application/pdf" => ValidatePdfContent(content),
            "image/jpeg" => ValidateJpegContent(content),
            "image/png" => ValidatePngContent(content),
            _ => Result.Validation("Only PDF, JPG and PNG documents are accepted."),
        };
    }

    public static double MaxMegabytes(MerchantVerificationOptions options) =>
        Math.Round(options.MaxDocumentBytes / (1024d * 1024d), 1);

    private static Result ValidatePdfContent(ReadOnlySpan<byte> pdf)
    {
        if (!HasValidPdfHeader(pdf)
            || !HasTerminalPdfEof(pdf)
            || !HasInspectablePdfEnvelope(pdf)
            || ContainsDisallowedBinaryPayload(pdf, allowLeadingPdf: true))
        {
            return Result.Validation("The file is not a complete PDF document. Upload the original PDF.");
        }

        var deEscaped = DecodePdfNameEscapes(pdf);
        if (PdfHasActiveContent(pdf) || (deEscaped is not null && PdfHasActiveContent(deEscaped)))
        {
            return PdfActiveContentFailure();
        }

        if (Mentions(pdf, deEscaped, "/Encrypt"u8))
        {
            return PdfUninspectableFailure("is password-protected or encrypted");
        }

        if (Mentions(pdf, deEscaped, "/LZWDecode"u8))
        {
            return PdfUninspectableFailure("uses a compression filter Faed cannot inspect");
        }

        return ScanPdfStreams(pdf);
    }

    private static Result ScanPdfStreams(ReadOnlySpan<byte> pdf)
    {
        var inflatedBudget = MaxInflatedPdfScanBytes;
        var inspectedStreams = 0;
        var searchStart = 0;

        while (true)
        {
            var findResult = FindNextPdfStreamKeyword(pdf, searchStart, out var streamAt);
            if (findResult == PdfScanStatus.Malformed)
            {
                return PdfUninspectableFailure("contains malformed PDF syntax");
            }

            if (findResult == PdfScanStatus.NotFound)
            {
                return Result.Success();
            }

            if (++inspectedStreams > MaxInspectedPdfStreams)
            {
                return PdfUninspectableFailure("contains too many streams to inspect");
            }

            if (!TryGetStreamDictionary(pdf, searchStart, streamAt, out var dictionary))
            {
                return PdfUninspectableFailure("contains a stream dictionary Faed could not parse");
            }

            if (dictionary.Length > MaxPdfStreamDictionaryBytes)
            {
                return PdfUninspectableFailure("contains a stream dictionary that exceeds the scan budget");
            }

            if (ContainsEscapedPdfDelimiterOrWhiteSpace(dictionary))
            {
                return PdfUninspectableFailure("contains an escaped PDF name delimiter Faed cannot safely parse");
            }

            var normalizedDictionary = DecodePdfNameEscapes(dictionary) ?? dictionary.ToArray();
            if (!TryReadDirectStreamLength(normalizedDictionary, out var streamLength))
            {
                return PdfUninspectableFailure("contains a stream with a missing, indirect or invalid length");
            }

            if (!TryClassifyStreamFilter(normalizedDictionary, out var filter, out var predictor))
            {
                return PdfUninspectableFailure("declares a filter Faed cannot inspect");
            }

            if (!TryGetStreamBounds(pdf, streamAt, streamLength, out var dataStart, out var dataEnd, out var afterEndStream))
            {
                return PdfUninspectableFailure("contains malformed stream boundaries");
            }

            var rawStream = pdf[dataStart..dataEnd];
            if (ContainsScriptMarker(rawStream))
            {
                return ScriptContentFailure();
            }

            if (ContainsDisallowedBinaryPayload(rawStream))
            {
                return PdfUninspectableFailure("contains an embedded archive or executable payload");
            }

            if (filter == PdfStreamFilter.Flate)
            {
                var inflated = InflateZlib(rawStream, inflatedBudget);
                if (inflated.Status == InflateStatus.BudgetExhausted)
                {
                    return PdfUninspectableFailure("is too large to fully inspect");
                }

                if (inflated.Status != InflateStatus.Success || inflated.Bytes is null)
                {
                    return PdfUninspectableFailure("contains a compressed stream Faed could not decode");
                }

                inflatedBudget -= inflated.Bytes.Length;
                if (inflatedBudget <= 0)
                {
                    return PdfUninspectableFailure("is too large to fully inspect");
                }

                var inspection = InspectInflatedPdfStream(inflated.Bytes);
                if (inspection.Failed)
                {
                    return inspection;
                }

                // With a predictor the inflated bytes are not yet what a reader consumes, so
                // undo it and scan that view too. Failing to undo it means the reader's view
                // cannot be established, which is a rejection, not a pass.
                if (predictor.IsActive)
                {
                    if (!TryApplyPdfPredictor(inflated.Bytes, predictor, out var decoded))
                    {
                        return PdfUninspectableFailure(
                            "contains a predictor-encoded stream Faed could not decode");
                    }

                    inspection = InspectInflatedPdfStream(decoded);
                    if (inspection.Failed)
                    {
                        return inspection;
                    }
                }
            }
            else if (filter == PdfStreamFilter.Dct
                && ValidateJpegContent(rawStream).Failed)
            {
                return PdfUninspectableFailure("contains an invalid JPEG image stream");
            }

            searchStart = afterEndStream;
        }
    }

    /// <summary>Runs every decoded-content check over one fully decoded stream view.</summary>
    private static Result InspectInflatedPdfStream(byte[] decoded)
    {
        if (ContainsScriptMarker(decoded))
        {
            return ScriptContentFailure();
        }

        if (ContainsDisallowedBinaryPayload(decoded))
        {
            return PdfUninspectableFailure("contains an embedded archive or executable payload");
        }

        var normalized = DecodePdfNameEscapes(decoded);
        return PdfHasActiveContent(decoded) || (normalized is not null && PdfHasActiveContent(normalized))
            ? PdfActiveContentFailure()
            : Result.Success();
    }

    /// <summary>
    /// Reads a stream's <c>/DecodeParms</c>. Only the standard Flate/LZW predictor parameters
    /// are accepted, with values Faed can actually reverse; anything else — an unknown key, an
    /// out-of-range value, an array of parameter dictionaries, or an indirect reference —
    /// returns false so the stream is rejected rather than scanned in the wrong view.
    /// </summary>
    private static bool TryReadPdfPredictor(ReadOnlySpan<byte> dictionary, out PdfPredictor predictor)
    {
        predictor = PdfPredictor.None;
        if (!TryFindTopLevelNameValues(dictionary, "DecodeParms"u8, out var values))
        {
            return false;
        }

        if (values.Count == 0)
        {
            return true;
        }

        if (values.Count != 1)
        {
            return false;
        }

        var index = values[0];
        SkipPdfWhitespaceAndComments(dictionary, ref index);

        // `/DecodeParms null` is the explicit "this filter takes no parameters" form.
        if (StartsWith(dictionary[index..], "null"u8)
            && IsPdfTokenBoundary(dictionary, index + "null"u8.Length))
        {
            return true;
        }

        if (!TryGetNestedPdfDictionary(dictionary, index, out var parameters)
            || !TryScanTopLevelPdfNames(parameters, out var entries))
        {
            return false;
        }

        var kind = 1;
        var colors = 1;
        var bitsPerComponent = 8;
        var columns = 1;

        foreach (var entry in entries)
        {
            var name = parameters.Slice(entry.NameStart, entry.NameLength);
            if (name.SequenceEqual("Predictor"u8))
            {
                if (!TryReadPdfIntegerValue(parameters, entry.ValueAt, out kind))
                {
                    return false;
                }
            }
            else if (name.SequenceEqual("Colors"u8))
            {
                if (!TryReadPdfIntegerValue(parameters, entry.ValueAt, out colors))
                {
                    return false;
                }
            }
            else if (name.SequenceEqual("BitsPerComponent"u8))
            {
                if (!TryReadPdfIntegerValue(parameters, entry.ValueAt, out bitsPerComponent))
                {
                    return false;
                }
            }
            else if (name.SequenceEqual("Columns"u8))
            {
                if (!TryReadPdfIntegerValue(parameters, entry.ValueAt, out columns))
                {
                    return false;
                }
            }
            else if (name.SequenceEqual("EarlyChange"u8))
            {
                if (!TryReadPdfIntegerValue(parameters, entry.ValueAt, out var earlyChange)
                    || earlyChange is not (0 or 1))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (kind == 1)
        {
            return true;
        }

        // TIFF differencing below 8 bits per component is vanishingly rare and fiddly to
        // reverse; refuse it rather than scan a view Faed cannot reproduce exactly.
        if (kind is not (2 or (>= 10 and <= 15))
            || colors is < 1 or > 4
            || bitsPerComponent is not (1 or 2 or 4 or 8 or 16)
            || columns < 1
            || (kind == 2 && bitsPerComponent != 8))
        {
            return false;
        }

        var rowBytes = (((long)columns * colors * bitsPerComponent) + 7) / 8;
        if (rowBytes is <= 0 or > MaxPdfPredictorRowBytes)
        {
            return false;
        }

        predictor = new PdfPredictor(kind, Math.Max(1, colors * bitsPerComponent / 8), (int)rowBytes);
        return true;
    }

    /// <summary>
    /// Reverses a PNG (predictor 10-15) or TIFF (predictor 2) predictor so the bytes scanned
    /// are the bytes a PDF reader consumes. Any inconsistency fails, which rejects the file.
    /// </summary>
    private static bool TryApplyPdfPredictor(byte[] encoded, PdfPredictor predictor, out byte[] decoded)
    {
        decoded = [];
        if (predictor.Kind == 2)
        {
            if (encoded.Length == 0 || encoded.Length % predictor.RowBytes != 0)
            {
                return false;
            }

            decoded = (byte[])encoded.Clone();
            for (var rowStart = 0; rowStart < decoded.Length; rowStart += predictor.RowBytes)
            {
                for (var offset = predictor.BytesPerPixel; offset < predictor.RowBytes; offset++)
                {
                    decoded[rowStart + offset] += decoded[rowStart + offset - predictor.BytesPerPixel];
                }
            }

            return true;
        }

        var stride = predictor.RowBytes + 1;
        if (encoded.Length == 0 || encoded.Length % stride != 0)
        {
            return false;
        }

        var rows = encoded.Length / stride;
        decoded = new byte[rows * predictor.RowBytes];

        for (var row = 0; row < rows; row++)
        {
            var filterType = encoded[row * stride];
            if (filterType > 4)
            {
                return false;
            }

            var source = row * stride + 1;
            var target = row * predictor.RowBytes;
            var previous = target - predictor.RowBytes;

            for (var offset = 0; offset < predictor.RowBytes; offset++)
            {
                var raw = encoded[source + offset];
                var left = offset >= predictor.BytesPerPixel ? decoded[target + offset - predictor.BytesPerPixel] : (byte)0;
                var up = row > 0 ? decoded[previous + offset] : (byte)0;
                var upperLeft = row > 0 && offset >= predictor.BytesPerPixel
                    ? decoded[previous + offset - predictor.BytesPerPixel]
                    : (byte)0;

                decoded[target + offset] = filterType switch
                {
                    0 => raw,
                    1 => (byte)(raw + left),
                    2 => (byte)(raw + up),
                    3 => (byte)(raw + ((left + up) / 2)),
                    _ => (byte)(raw + PaethPredictor(left, up, upperLeft)),
                };
            }
        }

        return true;
    }

    private static byte PaethPredictor(byte left, byte up, byte upperLeft)
    {
        var estimate = left + up - upperLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUp = Math.Abs(estimate - up);
        var distanceUpperLeft = Math.Abs(estimate - upperLeft);

        return distanceLeft <= distanceUp && distanceLeft <= distanceUpperLeft
            ? left
            : distanceUp <= distanceUpperLeft ? up : upperLeft;
    }

    /// <summary>Slices the <c>&lt;&lt; ... &gt;&gt;</c> dictionary that starts at <paramref name="start"/>.</summary>
    private static bool TryGetNestedPdfDictionary(
        ReadOnlySpan<byte> bytes,
        int start,
        out ReadOnlySpan<byte> dictionary)
    {
        dictionary = default;
        if (start + 1 >= bytes.Length || bytes[start] != (byte)'<' || bytes[start + 1] != (byte)'<')
        {
            return false;
        }

        var depth = 0;
        var index = start;
        while (index < bytes.Length)
        {
            if (bytes[index] == (byte)'%')
            {
                SkipPdfComment(bytes, ref index);
                continue;
            }

            if (bytes[index] == (byte)'(')
            {
                if (!TrySkipPdfLiteralString(bytes, ref index))
                {
                    return false;
                }

                continue;
            }

            if (bytes[index] == (byte)'<' && index + 1 < bytes.Length && bytes[index + 1] == (byte)'<')
            {
                depth++;
                index += 2;
                continue;
            }

            if (bytes[index] == (byte)'>' && index + 1 < bytes.Length && bytes[index + 1] == (byte)'>')
            {
                depth--;
                index += 2;
                if (depth == 0)
                {
                    dictionary = bytes[start..index];
                    return true;
                }

                if (depth < 0)
                {
                    return false;
                }

                continue;
            }

            if (bytes[index] == (byte)'<')
            {
                if (!TrySkipPdfHexString(bytes, ref index))
                {
                    return false;
                }

                continue;
            }

            index++;
        }

        return false;
    }

    /// <summary>Reads a direct, non-negative integer value; indirect references are refused.</summary>
    private static bool TryReadPdfIntegerValue(ReadOnlySpan<byte> bytes, int valueAt, out int value)
    {
        value = 0;
        var index = valueAt;
        SkipPdfWhitespaceAndComments(bytes, ref index);
        if (!TryReadPdfUnsignedInteger(bytes, ref index, out value))
        {
            return false;
        }

        // `12 0 R` is an indirect reference, not the number 12.
        SkipPdfWhitespaceAndComments(bytes, ref index);
        return index >= bytes.Length || bytes[index] is < (byte)'0' or > (byte)'9';
    }

    private static PdfScanStatus FindNextPdfStreamKeyword(
        ReadOnlySpan<byte> pdf,
        int start,
        out int streamAt)
    {
        streamAt = -1;
        var index = start;

        while (index < pdf.Length)
        {
            var current = pdf[index];
            if (current == (byte)'%')
            {
                SkipPdfComment(pdf, ref index);
                continue;
            }

            if (current == (byte)'(')
            {
                if (!TrySkipPdfLiteralString(pdf, ref index))
                {
                    return PdfScanStatus.Malformed;
                }

                continue;
            }

            if (current == (byte)'<' && (index + 1 >= pdf.Length || pdf[index + 1] != (byte)'<'))
            {
                if (!TrySkipPdfHexString(pdf, ref index))
                {
                    return PdfScanStatus.Malformed;
                }

                continue;
            }

            if (current == (byte)'/')
            {
                index++;
                while (index < pdf.Length && !IsPdfDelimiterOrWhiteSpace(pdf[index]))
                {
                    index++;
                }

                continue;
            }

            if (StartsWith(pdf[index..], "stream"u8)
                && IsPdfTokenBoundary(pdf, index - 1)
                && IsPdfTokenBoundary(pdf, index + "stream"u8.Length))
            {
                streamAt = index;
                return PdfScanStatus.Found;
            }

            index++;
        }

        return PdfScanStatus.NotFound;
    }

    private static bool TryGetStreamDictionary(
        ReadOnlySpan<byte> pdf,
        int scanStart,
        int streamAt,
        out ReadOnlySpan<byte> dictionary)
    {
        dictionary = default;
        var stack = new List<int>();
        var lastDictionaryStart = -1;
        var lastDictionaryEnd = -1;
        var index = scanStart;

        while (index < streamAt)
        {
            if (pdf[index] == (byte)'%')
            {
                SkipPdfComment(pdf[..streamAt], ref index);
                continue;
            }

            if (pdf[index] == (byte)'(')
            {
                if (!TrySkipPdfLiteralString(pdf[..streamAt], ref index))
                {
                    return false;
                }

                continue;
            }

            if (pdf[index] == (byte)'<' && index + 1 < streamAt && pdf[index + 1] == (byte)'<')
            {
                stack.Add(index);
                index += 2;
                continue;
            }

            if (pdf[index] == (byte)'>' && index + 1 < streamAt && pdf[index + 1] == (byte)'>')
            {
                if (stack.Count == 0)
                {
                    return false;
                }

                lastDictionaryStart = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                lastDictionaryEnd = index + 2;
                index += 2;
                continue;
            }

            if (pdf[index] == (byte)'<')
            {
                if (!TrySkipPdfHexString(pdf[..streamAt], ref index))
                {
                    return false;
                }

                continue;
            }

            index++;
        }

        if (stack.Count != 0
            || lastDictionaryStart < 0
            || !ContainsOnlyPdfWhitespaceAndComments(pdf[lastDictionaryEnd..streamAt]))
        {
            return false;
        }

        dictionary = pdf[lastDictionaryStart..lastDictionaryEnd];
        return true;
    }

    private static bool TryReadDirectStreamLength(ReadOnlySpan<byte> dictionary, out int length)
    {
        length = 0;
        if (!TryFindTopLevelNameValues(dictionary, "Length"u8, out var values) || values.Count != 1)
        {
            return false;
        }

        var index = values[0];
        SkipPdfWhitespaceAndComments(dictionary, ref index);
        if (index >= dictionary.Length || dictionary[index] is < (byte)'0' or > (byte)'9')
        {
            return false;
        }

        long parsed = 0;
        while (index < dictionary.Length && dictionary[index] is >= (byte)'0' and <= (byte)'9')
        {
            parsed = (parsed * 10) + dictionary[index] - (byte)'0';
            if (parsed > int.MaxValue)
            {
                return false;
            }

            index++;
        }

        if (index < dictionary.Length && !IsPdfDelimiterOrWhiteSpace(dictionary[index]))
        {
            return false;
        }

        SkipPdfWhitespaceAndComments(dictionary, ref index);
        if (index < dictionary.Length && dictionary[index] is >= (byte)'0' and <= (byte)'9')
        {
            return false;
        }

        if (index + 1 >= dictionary.Length
            || (dictionary[index] != (byte)'/'
                && !(dictionary[index] == (byte)'>' && dictionary[index + 1] == (byte)'>')))
        {
            return false;
        }

        length = (int)parsed;
        return true;
    }

    private static bool TryClassifyStreamFilter(
        ReadOnlySpan<byte> dictionary,
        out PdfStreamFilter filter,
        out PdfPredictor predictor)
    {
        filter = PdfStreamFilter.None;
        predictor = PdfPredictor.None;

        if (HasTopLevelPdfName(dictionary, "DP"u8)
            || HasTopLevelPdfName(dictionary, "F"u8)
            || HasTopLevelPdfName(dictionary, "FFilter"u8)
            || HasTopLevelPdfName(dictionary, "FDecodeParms"u8))
        {
            // An external stream source, or an abbreviated parameter form only legal in inline
            // images, means the bytes a reader consumes are not the bytes in this buffer.
            return false;
        }

        // A predictor is a reversible post-filter transform, so the inflated bytes alone are
        // not the reader's view. Rather than refuse — which rejected every PDF 1.5+
        // cross-reference stream, and so most modern documents — parse the parameters and
        // undo the transform before scanning (see ApplyPdfPredictor).
        if (!TryReadPdfPredictor(dictionary, out predictor))
        {
            return false;
        }

        if (!TryFindTopLevelNameValues(dictionary, "Filter"u8, out var values))
        {
            return false;
        }

        if (values.Count == 0)
        {
            return true;
        }

        if (values.Count != 1)
        {
            return false;
        }

        var index = values[0];
        SkipPdfWhitespaceAndComments(dictionary, ref index);
        ReadOnlySpan<byte> filterName;

        if (index < dictionary.Length && dictionary[index] == (byte)'[')
        {
            index++;
            SkipPdfWhitespaceAndComments(dictionary, ref index);
            if (!TryReadPdfName(dictionary, ref index, out var nameStart, out var nameLength))
            {
                return false;
            }

            filterName = dictionary.Slice(nameStart, nameLength);

            SkipPdfWhitespaceAndComments(dictionary, ref index);
            if (index >= dictionary.Length || dictionary[index] != (byte)']')
            {
                return false;
            }
        }
        else if (TryReadPdfName(dictionary, ref index, out var nameStart, out var nameLength))
        {
            filterName = dictionary.Slice(nameStart, nameLength);
        }
        else
        {
            return false;
        }

        if (filterName.SequenceEqual("FlateDecode"u8))
        {
            filter = PdfStreamFilter.Flate;
            return true;
        }

        if (filterName.SequenceEqual("DCTDecode"u8))
        {
            if (!HasSingleTopLevelNameValue(dictionary, "Type"u8, "XObject"u8)
                || !HasSingleTopLevelNameValue(dictionary, "Subtype"u8, "Image"u8))
            {
                return false;
            }

            filter = PdfStreamFilter.Dct;
            return true;
        }

        return false;
    }

    private static bool HasTopLevelPdfName(ReadOnlySpan<byte> dictionary, ReadOnlySpan<byte> name) =>
        !TryFindTopLevelNameValues(dictionary, name, out var values) || values.Count != 0;

    private static bool HasSingleTopLevelNameValue(
        ReadOnlySpan<byte> dictionary,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> expectedValue)
    {
        if (!TryFindTopLevelNameValues(dictionary, key, out var values) || values.Count != 1)
        {
            return false;
        }

        var index = values[0];
        SkipPdfWhitespaceAndComments(dictionary, ref index);
        if (!TryReadPdfName(dictionary, ref index, out var valueStart, out var valueLength))
        {
            return false;
        }

        return dictionary.Slice(valueStart, valueLength).SequenceEqual(expectedValue);
    }

    private static bool TryFindTopLevelNameValues(
        ReadOnlySpan<byte> dictionary,
        ReadOnlySpan<byte> name,
        out List<int> values)
    {
        values = [];
        if (!TryScanTopLevelPdfNames(dictionary, out var entries))
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (dictionary.Slice(entry.NameStart, entry.NameLength).SequenceEqual(name))
            {
                values.Add(entry.ValueAt);
            }
        }

        return true;
    }

    /// <summary>
    /// Lists every key of the outermost dictionary — entries nested inside a sub-dictionary
    /// or an array are deliberately excluded, so a nested <c>/Filter</c> can never be mistaken
    /// for this stream's own. Returns false when the dictionary does not parse, which every
    /// caller treats as a rejection.
    /// </summary>
    private static bool TryScanTopLevelPdfNames(
        ReadOnlySpan<byte> dictionary,
        out List<PdfDictionaryEntry> entries)
    {
        entries = [];
        var dictionaryDepth = 0;
        var arrayDepth = 0;
        var index = 0;

        while (index < dictionary.Length)
        {
            if (dictionary[index] == (byte)'%')
            {
                SkipPdfComment(dictionary, ref index);
                continue;
            }

            if (dictionary[index] == (byte)'(')
            {
                if (!TrySkipPdfLiteralString(dictionary, ref index))
                {
                    return false;
                }

                continue;
            }

            if (dictionary[index] == (byte)'<'
                && index + 1 < dictionary.Length
                && dictionary[index + 1] == (byte)'<')
            {
                dictionaryDepth++;
                index += 2;
                continue;
            }

            if (dictionary[index] == (byte)'>'
                && index + 1 < dictionary.Length
                && dictionary[index + 1] == (byte)'>')
            {
                dictionaryDepth--;
                if (dictionaryDepth < 0)
                {
                    return false;
                }

                index += 2;
                continue;
            }

            if (dictionary[index] == (byte)'<')
            {
                if (!TrySkipPdfHexString(dictionary, ref index))
                {
                    return false;
                }

                continue;
            }

            if (dictionary[index] == (byte)'[')
            {
                arrayDepth++;
                index++;
                continue;
            }

            if (dictionary[index] == (byte)']')
            {
                arrayDepth--;
                if (arrayDepth < 0)
                {
                    return false;
                }

                index++;
                continue;
            }

            if (dictionary[index] != (byte)'/')
            {
                index++;
                continue;
            }

            index++;
            var nameStart = index;
            while (index < dictionary.Length && !IsPdfDelimiterOrWhiteSpace(dictionary[index]))
            {
                index++;
            }

            if (dictionaryDepth == 1 && arrayDepth == 0 && index > nameStart)
            {
                entries.Add(new PdfDictionaryEntry(nameStart, index - nameStart, index));
            }
        }

        return dictionaryDepth == 0 && arrayDepth == 0;
    }

    private static bool TryReadPdfName(
        ReadOnlySpan<byte> bytes,
        ref int index,
        out int nameStart,
        out int nameLength)
    {
        nameStart = 0;
        nameLength = 0;
        if (index >= bytes.Length || bytes[index] != (byte)'/')
        {
            return false;
        }

        index++;
        var start = index;
        while (index < bytes.Length && !IsPdfDelimiterOrWhiteSpace(bytes[index]))
        {
            index++;
        }

        if (index == start)
        {
            return false;
        }

        nameStart = start;
        nameLength = index - start;
        return true;
    }

    private static bool TryGetStreamBounds(
        ReadOnlySpan<byte> pdf,
        int streamAt,
        int streamLength,
        out int dataStart,
        out int dataEnd,
        out int afterEndStream)
    {
        dataStart = streamAt + "stream"u8.Length;
        dataEnd = 0;
        afterEndStream = 0;

        if (dataStart >= pdf.Length)
        {
            return false;
        }

        if (pdf[dataStart] == (byte)'\r')
        {
            dataStart++;
            if (dataStart < pdf.Length && pdf[dataStart] == (byte)'\n')
            {
                dataStart++;
            }
        }
        else if (pdf[dataStart] == (byte)'\n')
        {
            dataStart++;
        }
        else
        {
            return false;
        }

        if (streamLength > pdf.Length - dataStart)
        {
            return false;
        }

        dataEnd = dataStart + streamLength;
        var endStreamAt = dataEnd;
        if (endStreamAt < pdf.Length && pdf[endStreamAt] == (byte)'\r')
        {
            endStreamAt++;
            if (endStreamAt < pdf.Length && pdf[endStreamAt] == (byte)'\n')
            {
                endStreamAt++;
            }
        }
        else if (endStreamAt < pdf.Length && pdf[endStreamAt] == (byte)'\n')
        {
            endStreamAt++;
        }

        if (!StartsWith(pdf[endStreamAt..], "endstream"u8)
            || !IsPdfTokenBoundary(pdf, endStreamAt + "endstream"u8.Length))
        {
            return false;
        }

        afterEndStream = endStreamAt + "endstream"u8.Length;
        return true;
    }

    private static Result ValidateJpegContent(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return InvalidImageFailure();
        }

        var position = 2;
        var sawFrame = false;
        var sawQuantizationTable = false;
        var sawHuffmanTable = false;
        var sawScan = false;
        byte frameMarker = 0;
        Span<bool> frameComponents = stackalloc bool[256];
        Span<byte> componentQuantizationTables = stackalloc byte[256];
        Span<bool> quantizationTables = stackalloc bool[4];
        Span<bool> dcHuffmanTables = stackalloc bool[4];
        Span<bool> acHuffmanTables = stackalloc bool[4];
        frameComponents.Clear();
        componentQuantizationTables.Fill(byte.MaxValue);
        quantizationTables.Clear();
        dcHuffmanTables.Clear();
        acHuffmanTables.Clear();

        while (position < jpeg.Length)
        {
            if (jpeg[position] != 0xFF)
            {
                return InvalidImageFailure();
            }

            while (position < jpeg.Length && jpeg[position] == 0xFF)
            {
                position++;
            }

            if (position >= jpeg.Length)
            {
                return InvalidImageFailure();
            }

            var marker = jpeg[position++];
            if (marker == 0xD9)
            {
                return sawFrame && sawScan && position == jpeg.Length
                    ? Result.Success()
                    : InvalidImageFailure();
            }

            if (marker is 0x00 or 0x01 or 0xD8 or >= 0xD0 and <= 0xD7)
            {
                return InvalidImageFailure();
            }

            if (position + 2 > jpeg.Length)
            {
                return InvalidImageFailure();
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(jpeg[position..]);
            if (segmentLength < 2 || segmentLength > jpeg.Length - position)
            {
                return InvalidImageFailure();
            }

            var segment = jpeg.Slice(position + 2, segmentLength - 2);
            position += segmentLength;

            switch (marker)
            {
                case 0xC0:
                case 0xC1:
                case 0xC2:
                    if (sawFrame
                        || !ValidateJpegFrame(
                            marker,
                            segment,
                            frameComponents,
                            componentQuantizationTables))
                    {
                        return InvalidImageFailure();
                    }

                    frameMarker = marker;
                    sawFrame = true;
                    break;

                case 0xC4:
                    if (!ValidateJpegHuffmanTables(segment, dcHuffmanTables, acHuffmanTables))
                    {
                        return InvalidImageFailure();
                    }

                    sawHuffmanTable = true;
                    break;

                case 0xDB:
                    if (!ValidateJpegQuantizationTables(segment, quantizationTables))
                    {
                        return InvalidImageFailure();
                    }

                    sawQuantizationTable = true;
                    break;

                case 0xDA:
                    if (!sawFrame
                        || !sawQuantizationTable
                        || !sawHuffmanTable
                        || !ValidateJpegScanHeader(
                            frameMarker,
                            segment,
                            frameComponents,
                            componentQuantizationTables,
                            quantizationTables,
                            dcHuffmanTables,
                            acHuffmanTables)
                        || !TrySkipJpegEntropyData(jpeg, ref position))
                    {
                        return InvalidImageFailure();
                    }

                    sawScan = true;
                    break;

                case >= 0xE0 and <= 0xEF:
                case 0xFE:
                    break;

                case 0xDD:
                    if (segment.Length != 2)
                    {
                        return InvalidImageFailure();
                    }

                    break;

                default:
                    // Arithmetic coding, hierarchical/lossless frames and extension markers
                    // are deliberately outside the inspectable standalone-JPEG subset.
                    return InvalidImageFailure();
            }
        }

        return InvalidImageFailure();
    }

    private static bool ValidateJpegFrame(
        byte frameMarker,
        ReadOnlySpan<byte> segment,
        Span<bool> componentIds,
        Span<byte> componentQuantizationTables)
    {
        if (segment.Length < 9
            || (frameMarker == 0xC0 && segment[0] != 8)
            || (frameMarker is 0xC1 or 0xC2 && segment[0] is not (8 or 12)))
        {
            return false;
        }

        var height = BinaryPrimitives.ReadUInt16BigEndian(segment[1..]);
        var width = BinaryPrimitives.ReadUInt16BigEndian(segment[3..]);
        var componentCount = segment[5];
        if (height == 0 || width == 0 || componentCount is not (1 or 3 or 4)
            || segment.Length != 6 + (3 * componentCount)
            || (long)width * height * componentCount * (segment[0] > 8 ? 2 : 1) >= MaxInflatedImageBytes)
        {
            return false;
        }

        componentIds.Clear();
        componentQuantizationTables.Fill(byte.MaxValue);
        for (var index = 0; index < componentCount; index++)
        {
            var offset = 6 + (index * 3);
            var id = segment[offset];
            var sampling = segment[offset + 1];
            if (componentIds[id]
                || (sampling >> 4) is < 1 or > 4
                || (sampling & 0x0F) is < 1 or > 4
                || segment[offset + 2] > 3)
            {
                return false;
            }

            componentIds[id] = true;
            componentQuantizationTables[id] = segment[offset + 2];
        }

        return true;
    }

    private static bool ValidateJpegQuantizationTables(
        ReadOnlySpan<byte> segment,
        Span<bool> definedTables)
    {
        var position = 0;
        while (position < segment.Length)
        {
            var tableInfo = segment[position++];
            var precision = tableInfo >> 4;
            var tableId = tableInfo & 0x0F;
            if (precision > 1 || tableId > 3)
            {
                return false;
            }

            var tableBytes = precision == 0 ? 64 : 128;
            if (tableBytes > segment.Length - position)
            {
                return false;
            }

            for (var valueIndex = 0; valueIndex < 64; valueIndex++)
            {
                var value = precision == 0
                    ? segment[position + valueIndex]
                    : BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(position + (valueIndex * 2), 2));
                if (value == 0)
                {
                    return false;
                }
            }

            position += tableBytes;
            definedTables[tableId] = true;
        }

        return position == segment.Length && position > 0;
    }

    private static bool ValidateJpegHuffmanTables(
        ReadOnlySpan<byte> segment,
        Span<bool> definedDcTables,
        Span<bool> definedAcTables)
    {
        var position = 0;
        while (position < segment.Length)
        {
            var tableInfo = segment[position++];
            var tableClass = tableInfo >> 4;
            var tableId = tableInfo & 0x0F;
            if (tableClass > 1 || tableId > 3 || position + 16 > segment.Length)
            {
                return false;
            }

            var symbolCount = 0;
            var availableCodes = 1;
            for (var index = 0; index < 16; index++)
            {
                var codeCount = segment[position + index];
                symbolCount += codeCount;
                availableCodes = (availableCodes * 2) - codeCount;
                if (availableCodes < 0)
                {
                    return false;
                }
            }

            position += 16;
            if (symbolCount == 0 || symbolCount > 256 || symbolCount > segment.Length - position)
            {
                return false;
            }

            position += symbolCount;
            if (tableClass == 0)
            {
                definedDcTables[tableId] = true;
            }
            else
            {
                definedAcTables[tableId] = true;
            }
        }

        return position == segment.Length && position > 0;
    }

    private static bool ValidateJpegScanHeader(
        byte frameMarker,
        ReadOnlySpan<byte> segment,
        ReadOnlySpan<bool> frameComponents,
        ReadOnlySpan<byte> componentQuantizationTables,
        ReadOnlySpan<bool> quantizationTables,
        ReadOnlySpan<bool> definedDcTables,
        ReadOnlySpan<bool> definedAcTables)
    {
        if (segment.Length < 6)
        {
            return false;
        }

        var componentCount = segment[0];
        if (componentCount is < 1 or > 4 || segment.Length != 4 + (2 * componentCount))
        {
            return false;
        }

        Span<bool> componentIds = stackalloc bool[256];
        componentIds.Clear();
        var spectralStart = segment[^3];
        var spectralEnd = segment[^2];
        var successiveHigh = segment[^1] >> 4;
        var successiveLow = segment[^1] & 0x0F;

        if (frameMarker is 0xC0 or 0xC1)
        {
            if (spectralStart != 0 || spectralEnd != 63 || successiveHigh != 0 || successiveLow != 0)
            {
                return false;
            }
        }
        else if (frameMarker == 0xC2)
        {
            if (spectralStart > spectralEnd
                || spectralEnd > 63
                || (spectralStart == 0 && spectralEnd != 0)
                || (spectralStart > 0 && componentCount != 1)
                || successiveHigh > 13
                || successiveLow > 13
                || (successiveHigh > 0 && successiveHigh != successiveLow + 1))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        for (var index = 0; index < componentCount; index++)
        {
            var offset = 1 + (index * 2);
            var id = segment[offset];
            var tables = segment[offset + 1];
            var dcTable = tables >> 4;
            var acTable = tables & 0x0F;
            var quantizationTable = componentQuantizationTables[id];
            if (componentIds[id]
                || !frameComponents[id]
                || quantizationTable > 3
                || !quantizationTables[quantizationTable]
                || dcTable > 3
                || acTable > 3)
            {
                return false;
            }

            if (frameMarker is 0xC0 or 0xC1)
            {
                if (!definedDcTables[dcTable] || !definedAcTables[acTable])
                {
                    return false;
                }
            }
            else if (spectralStart == 0)
            {
                if (acTable != 0 || !definedDcTables[dcTable])
                {
                    return false;
                }
            }
            else if (dcTable != 0 || !definedAcTables[acTable])
            {
                return false;
            }

            componentIds[id] = true;
        }

        return true;
    }

    private static bool TrySkipJpegEntropyData(ReadOnlySpan<byte> jpeg, ref int position)
    {
        var sawEntropyByte = false;
        while (position < jpeg.Length)
        {
            if (jpeg[position] != 0xFF)
            {
                sawEntropyByte = true;
                position++;
                continue;
            }

            var markerStart = position;
            while (position < jpeg.Length && jpeg[position] == 0xFF)
            {
                position++;
            }

            if (position >= jpeg.Length)
            {
                return false;
            }

            if (jpeg[position] == 0x00)
            {
                sawEntropyByte = true;
                position++;
                continue;
            }

            if (jpeg[position] is >= 0xD0 and <= 0xD7)
            {
                position++;
                continue;
            }

            position = markerStart;
            return sawEntropyByte;
        }

        return false;
    }

    private static Result ValidatePngContent(ReadOnlySpan<byte> png)
    {
        if (!StartsWith(png, PngSignature))
        {
            return InvalidImageFailure();
        }

        using var idat = new MemoryStream();
        var position = PngSignature.Length;
        var width = 0u;
        var height = 0u;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        var paletteEntries = 0;
        var sawHeader = false;
        var sawPalette = false;
        var sawImageData = false;
        var imageDataEnded = false;
        var seenAncillaryChunks = new HashSet<uint>();
        var ancillaryInflateBudget = MaxInflatedPngAncillaryBytes;

        while (position < png.Length)
        {
            if (png.Length - position < 12)
            {
                return InvalidImageFailure();
            }

            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(png[position..]);
            if (lengthValue > int.MaxValue)
            {
                return InvalidImageFailure();
            }

            var length = (int)lengthValue;
            if (length > png.Length - position - 12)
            {
                return InvalidImageFailure();
            }

            var type = png.Slice(position + 4, 4);
            var data = png.Slice(position + 8, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(position + 8 + length, 4));
            if (!IsPngChunkType(type)
                || ComputeCrc32(png.Slice(position + 4, 4 + length)) != expectedCrc)
            {
                return InvalidImageFailure();
            }

            var nextPosition = position + 12 + length;
            if (!sawHeader && !type.SequenceEqual("IHDR"u8))
            {
                return InvalidImageFailure();
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || position != PngSignature.Length || !ValidatePngHeader(data))
                {
                    return InvalidImageFailure();
                }

                width = BinaryPrimitives.ReadUInt32BigEndian(data);
                height = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
                sawHeader = true;
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (sawPalette
                    || sawImageData
                    || colorType is 0 or 4
                    || data.Length is < 3 or > 768
                    || data.Length % 3 != 0)
                {
                    return InvalidImageFailure();
                }

                paletteEntries = data.Length / 3;
                if (colorType == 3 && paletteEntries > 1 << bitDepth)
                {
                    return InvalidImageFailure();
                }

                sawPalette = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (imageDataEnded || (colorType == 3 && !sawPalette))
                {
                    return InvalidImageFailure();
                }

                sawImageData = true;
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (data.Length != 0 || !sawImageData || nextPosition != png.Length)
                {
                    return InvalidImageFailure();
                }

                return ValidatePngRaster(
                    idat.ToArray(),
                    width,
                    height,
                    bitDepth,
                    colorType,
                    interlace);
            }
            else
            {
                if (sawImageData)
                {
                    imageDataEnded = true;
                }

                // An unrecognised *critical* chunk means the image cannot be understood at all.
                // Ancillary chunks are, by definition, ignorable by a decoder, so they are
                // allowed — a plain export from Office, a phone or an AI tool routinely carries
                // tEXt, iCCP, eXIf or a C2PA provenance chunk, and rejecting them refused every
                // real-world PNG tested. Their bytes are still covered by the whole-file script
                // and archive/executable scans, and the compressed carriers below are inflated
                // and scanned so nothing hides behind zlib.
                if ((type[0] & 0x20) == 0)
                {
                    return InvalidImageFailure();
                }

                var chunkCode = BinaryPrimitives.ReadUInt32BigEndian(type);
                if (IsSingleOccurrencePngChunk(type) && !seenAncillaryChunks.Add(chunkCode))
                {
                    return InvalidImageFailure();
                }

                var ancillary = ValidatePngAncillaryChunk(
                    type, data, sawPalette, sawImageData, colorType, paletteEntries, ref ancillaryInflateBudget);
                if (ancillary.Failed)
                {
                    return ancillary;
                }
            }

            position = nextPosition;
        }

        return InvalidImageFailure();
    }

    private static bool ValidatePngHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(data);
        var height = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var bitDepth = data[8];
        var colorType = data[9];
        if (width == 0
            || height == 0
            || data[10] != 0
            || data[11] != 0
            || data[12] > 1)
        {
            return false;
        }

        return colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            2 => bitDepth is 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            4 => bitDepth is 8 or 16,
            6 => bitDepth is 8 or 16,
            _ => false,
        };
    }

    /// <summary>PNG chunks the specification allows at most once per datastream.</summary>
    private static bool IsSingleOccurrencePngChunk(ReadOnlySpan<byte> type) =>
        type.SequenceEqual("tRNS"u8)
        || type.SequenceEqual("gAMA"u8)
        || type.SequenceEqual("cHRM"u8)
        || type.SequenceEqual("sRGB"u8)
        || type.SequenceEqual("pHYs"u8)
        || type.SequenceEqual("tIME"u8)
        || type.SequenceEqual("bKGD"u8)
        || type.SequenceEqual("sBIT"u8)
        || type.SequenceEqual("hIST"u8)
        || type.SequenceEqual("iCCP"u8)
        || type.SequenceEqual("eXIf"u8);

    private static Result ValidatePngAncillaryChunk(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data,
        bool sawPalette,
        bool sawImageData,
        byte colorType,
        int paletteEntries,
        ref int inflateBudget)
    {
        var structurallyValid = true;

        if (type.SequenceEqual("tRNS"u8))
        {
            structurallyValid = !sawImageData && colorType switch
            {
                0 => data.Length == 2,
                2 => data.Length == 6,
                3 => sawPalette && data.Length is > 0 && data.Length <= paletteEntries,
                _ => false,
            };
        }
        else if (type.SequenceEqual("gAMA"u8))
        {
            structurallyValid = !sawPalette
                && !sawImageData
                && data.Length == 4
                && BinaryPrimitives.ReadUInt32BigEndian(data) != 0;
        }
        else if (type.SequenceEqual("cHRM"u8))
        {
            structurallyValid = !sawPalette && !sawImageData && data.Length == 32;
        }
        else if (type.SequenceEqual("sRGB"u8))
        {
            structurallyValid = !sawPalette && !sawImageData && data.Length == 1 && data[0] <= 3;
        }
        else if (type.SequenceEqual("pHYs"u8))
        {
            structurallyValid = !sawImageData && data.Length == 9 && data[8] <= 1;
        }
        else if (type.SequenceEqual("tIME"u8))
        {
            structurallyValid = IsValidPngTime(data);
        }
        else if (type.SequenceEqual("bKGD"u8))
        {
            structurallyValid = !sawImageData && colorType switch
            {
                0 or 4 => data.Length == 2,
                2 or 6 => data.Length == 6,
                3 => sawPalette && data.Length == 1 && data[0] < paletteEntries,
                _ => false,
            };
        }
        else if (type.SequenceEqual("sBIT"u8))
        {
            structurallyValid = !sawPalette && !sawImageData && colorType switch
            {
                0 => data.Length == 1,
                2 or 3 => data.Length == 3,
                4 => data.Length == 2,
                6 => data.Length == 4,
                _ => false,
            };
        }
        else if (type.SequenceEqual("hIST"u8))
        {
            structurallyValid = sawPalette && !sawImageData && data.Length == paletteEntries * 2;
        }
        else if (type.SequenceEqual("tEXt"u8))
        {
            structurallyValid = TrySplitPngKeyword(data, out _);
        }
        else if (type.SequenceEqual("zTXt"u8))
        {
            return ValidateCompressedPngTextChunk(data, keywordFirst: true, ref inflateBudget);
        }
        else if (type.SequenceEqual("iCCP"u8))
        {
            structurallyValid = !sawPalette && !sawImageData;
            if (structurallyValid)
            {
                return ValidateCompressedPngTextChunk(data, keywordFirst: true, ref inflateBudget);
            }
        }
        else if (type.SequenceEqual("iTXt"u8))
        {
            return ValidateInternationalPngTextChunk(data, ref inflateBudget);
        }

        // Everything else is an ancillary chunk Faed does not model (eXIf, sPLT, APNG frames,
        // vendor/provenance chunks). Its CRC is already verified and its bytes are covered by
        // the whole-file scans, so it is accepted rather than refused.
        return structurallyValid ? Result.Success() : InvalidImageFailure();
    }

    /// <summary>Splits a PNG <c>keyword\0…</c> payload and validates the keyword's shape.</summary>
    private static bool TrySplitPngKeyword(ReadOnlySpan<byte> data, out int separatorAt)
    {
        separatorAt = data.IndexOf((byte)0);
        return separatorAt is >= 1 and <= 79;
    }

    /// <summary>
    /// Inflates a <c>zTXt</c> or <c>iCCP</c> payload and scans it, so a script marker or an
    /// embedded archive cannot ride into the file behind zlib. A payload that will not inflate
    /// cannot be shown to be safe, so it is rejected.
    /// </summary>
    private static Result ValidateCompressedPngTextChunk(
        ReadOnlySpan<byte> data,
        bool keywordFirst,
        ref int inflateBudget)
    {
        var offset = 0;
        if (keywordFirst)
        {
            if (!TrySplitPngKeyword(data, out var separatorAt))
            {
                return InvalidImageFailure();
            }

            offset = separatorAt + 1;
        }

        // One compression-method byte, and only method 0 (zlib/deflate) is defined.
        if (offset >= data.Length || data[offset] != 0)
        {
            return InvalidImageFailure();
        }

        return ScanCompressedPngPayload(data[(offset + 1)..], ref inflateBudget);
    }

    private static Result ValidateInternationalPngTextChunk(ReadOnlySpan<byte> data, ref int inflateBudget)
    {
        if (!TrySplitPngKeyword(data, out var separatorAt) || separatorAt + 2 >= data.Length)
        {
            return InvalidImageFailure();
        }

        var compressionFlag = data[separatorAt + 1];
        var compressionMethod = data[separatorAt + 2];
        if (compressionFlag > 1 || (compressionFlag == 1 && compressionMethod != 0))
        {
            return InvalidImageFailure();
        }

        var rest = data[(separatorAt + 3)..];
        var languageAt = rest.IndexOf((byte)0);
        if (languageAt < 0)
        {
            return InvalidImageFailure();
        }

        rest = rest[(languageAt + 1)..];
        var translatedAt = rest.IndexOf((byte)0);
        if (translatedAt < 0)
        {
            return InvalidImageFailure();
        }

        var text = rest[(translatedAt + 1)..];
        return compressionFlag == 0
            ? Result.Success()
            : ScanCompressedPngPayload(text, ref inflateBudget);
    }

    private static Result ScanCompressedPngPayload(ReadOnlySpan<byte> compressed, ref int inflateBudget)
    {
        if (inflateBudget <= 0)
        {
            return InvalidImageFailure();
        }

        var inflated = InflateZlib(compressed, inflateBudget);
        if (inflated.Status != InflateStatus.Success || inflated.Bytes is null)
        {
            return InvalidImageFailure();
        }

        inflateBudget -= inflated.Bytes.Length;
        if (ContainsScriptMarker(inflated.Bytes))
        {
            return ScriptContentFailure();
        }

        return ContainsDisallowedBinaryPayload(inflated.Bytes)
            ? InvalidImageFailure()
            : Result.Success();
    }

    private static bool IsValidPngTime(ReadOnlySpan<byte> data)
    {
        if (data.Length != 7)
        {
            return false;
        }

        var year = BinaryPrimitives.ReadUInt16BigEndian(data);
        var month = data[2];
        var day = data[3];
        if (year == 0 || month is < 1 or > 12 || day == 0)
        {
            return false;
        }

        var leapYear = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
        var daysInMonth = month switch
        {
            2 => leapYear ? 29 : 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31,
        };

        return day <= daysInMonth
            && data[4] <= 23
            && data[5] <= 59
            && data[6] <= 60;
    }

    private static Result ValidatePngRaster(
        byte[] compressed,
        uint width,
        uint height,
        byte bitDepth,
        byte colorType,
        byte interlace)
    {
        if (!TryGetPngPasses(width, height, bitDepth, colorType, interlace, out var passes, out var expectedLength)
            || expectedLength >= MaxInflatedImageBytes)
        {
            return InvalidImageFailure();
        }

        var inflated = InflateZlib(compressed, MaxInflatedImageBytes);
        if (inflated.Status != InflateStatus.Success
            || inflated.Bytes is null
            || inflated.Bytes.Length != expectedLength)
        {
            return InvalidImageFailure();
        }

        var position = 0;
        foreach (var pass in passes)
        {
            for (var row = 0u; row < pass.Height; row++)
            {
                if (inflated.Bytes[position] > 4)
                {
                    return InvalidImageFailure();
                }

                position += 1 + pass.RowBytes;
            }
        }

        return position == inflated.Bytes.Length ? Result.Success() : InvalidImageFailure();
    }

    private static bool TryGetPngPasses(
        uint width,
        uint height,
        byte bitDepth,
        byte colorType,
        byte interlace,
        out List<PngPass> passes,
        out int expectedLength)
    {
        passes = [];
        expectedLength = 0;
        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => 0,
        };

        if (channels == 0)
        {
            return false;
        }

        var decodedBytesPerChannel = bitDepth == 16 ? 2 : 1;
        if ((long)width * height * channels * decodedBytesPerChannel >= MaxInflatedImageBytes)
        {
            return false;
        }

        ReadOnlySpan<byte> startsX = interlace == 0 ? [0] : [0, 4, 0, 2, 0, 1, 0];
        ReadOnlySpan<byte> startsY = interlace == 0 ? [0] : [0, 0, 4, 0, 2, 0, 1];
        ReadOnlySpan<byte> stepsX = interlace == 0 ? [1] : [8, 8, 4, 4, 2, 2, 1];
        ReadOnlySpan<byte> stepsY = interlace == 0 ? [1] : [8, 8, 8, 4, 4, 2, 2];
        long total = 0;

        for (var index = 0; index < startsX.Length; index++)
        {
            var passWidth = PngPassDimension(width, startsX[index], stepsX[index]);
            var passHeight = PngPassDimension(height, startsY[index], stepsY[index]);
            if (passWidth == 0 || passHeight == 0)
            {
                continue;
            }

            var rowBits = (long)passWidth * channels * bitDepth;
            var rowBytes = (rowBits + 7) / 8;
            total += (rowBytes + 1) * passHeight;
            if (rowBytes > int.MaxValue || total >= MaxInflatedImageBytes)
            {
                return false;
            }

            passes.Add(new PngPass(passHeight, (int)rowBytes));
        }

        expectedLength = (int)total;
        return expectedLength > 0;
    }

    private static uint PngPassDimension(uint size, byte start, byte step) =>
        size <= start ? 0 : ((size - start) + step - 1) / step;

    private static InflateResult InflateZlib(ReadOnlySpan<byte> compressed, int maxBytes)
    {
        if (compressed.Length < 6 || maxBytes <= 0)
        {
            return InflateResult.Invalid;
        }

        var cmf = compressed[0];
        var flg = compressed[1];
        if ((cmf & 0x0F) != 8
            || (cmf >> 4) > 7
            || (((cmf << 8) | flg) % 31) != 0
            || (flg & 0x20) != 0)
        {
            return InflateResult.Invalid;
        }

        try
        {
            using var input = new MemoryStream(compressed.ToArray(), writable: false);
            using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
            var buffer = new byte[8192];

            while (true)
            {
                var read = decompressor.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read >= maxBytes)
                {
                    return InflateResult.BudgetExhausted;
                }

                output.Write(buffer, 0, read);
            }

            var inflated = output.ToArray();
            var adlerBytes = compressed[^4..];
            var expectedAdler = BinaryPrimitives.ReadUInt32BigEndian(adlerBytes);
            if (ComputeAdler32(inflated) != expectedAdler
                || compressed[..^4].IndexOf(adlerBytes) >= 0)
            {
                return InflateResult.Invalid;
            }

            return InflateResult.Success(inflated);
        }
        catch (InvalidDataException)
        {
            return InflateResult.Invalid;
        }
        catch (IOException)
        {
            return InflateResult.Invalid;
        }
    }

    private static bool HasValidPdfHeader(ReadOnlySpan<byte> pdf) =>
        pdf.Length > 8
        && StartsWith(pdf, "%PDF-"u8)
        && pdf[6] == (byte)'.'
        && ((pdf[5] == (byte)'1' && pdf[7] is >= (byte)'0' and <= (byte)'7')
            || (pdf[5] == (byte)'2' && pdf[7] == (byte)'0'))
        && pdf[8] is (byte)'\r' or (byte)'\n';

    /// <summary>
    /// Checks that the file ends in a well-formed cross-reference pointer: the final
    /// <c>startxref</c> names an offset inside the file, <c>%%EOF</c> terminates it, and the
    /// offset lands on either a classic <c>xref</c> table with a <c>/Root</c> trailer or a
    /// PDF 1.5+ cross-reference stream object.
    ///
    /// Earlier revisions of this method also required exactly one <c>startxref</c> and one
    /// <c>%%EOF</c> in the whole file. That rejected every linearized ("Fast Web View") and
    /// incrementally-saved PDF — 8 of 10 real-world sample documents — without adding any
    /// safety: <see cref="ScanPdfStreams"/> walks every stream in the buffer and the
    /// active-content and binary-payload scans cover every byte, so an appended revision is
    /// inspected exactly like the first one.
    /// </summary>
    private static bool HasInspectablePdfEnvelope(ReadOnlySpan<byte> pdf)
    {
        var startXrefAt = pdf.LastIndexOf("startxref"u8);
        if (startXrefAt < 0
            || !IsPdfTokenBoundary(pdf, startXrefAt - 1)
            || !IsPdfTokenBoundary(pdf, startXrefAt + "startxref"u8.Length))
        {
            return false;
        }

        var tail = startXrefAt + "startxref"u8.Length;
        while (tail < pdf.Length && IsPdfWhiteSpace(pdf[tail]))
        {
            tail++;
        }

        if (!TryReadPdfUnsignedInteger(pdf, ref tail, out var xrefOffset)
            || xrefOffset >= startXrefAt)
        {
            return false;
        }

        while (tail < pdf.Length && IsPdfWhiteSpace(pdf[tail]))
        {
            tail++;
        }

        if (!StartsWith(pdf[tail..], "%%EOF"u8))
        {
            return false;
        }

        tail += "%%EOF"u8.Length;
        while (tail < pdf.Length && IsPdfWhiteSpace(pdf[tail]))
        {
            tail++;
        }

        if (tail != pdf.Length)
        {
            return false;
        }

        return StartsWith(pdf[xrefOffset..], "xref"u8)
                && IsPdfTokenBoundary(pdf, xrefOffset + "xref"u8.Length)
            ? HasRootBearingTrailer(pdf[xrefOffset..startXrefAt])
            : HasCrossReferenceStreamAt(pdf, xrefOffset);
    }

    /// <summary>Classic layout: an <c>xref</c> table followed by a <c>trailer</c> naming <c>/Root</c>.</summary>
    private static bool HasRootBearingTrailer(ReadOnlySpan<byte> xrefTail)
    {
        var trailerRelative = xrefTail.LastIndexOf("trailer"u8);
        if (trailerRelative < 0)
        {
            return false;
        }

        var trailer = xrefTail[trailerRelative..];
        var normalizedTrailer = DecodePdfNameEscapes(trailer);
        return !ContainsEscapedPdfDelimiterOrWhiteSpace(trailer)
            && Mentions(trailer, normalizedTrailer, "/Root"u8);
    }

    /// <summary>
    /// PDF 1.5+ layout: <c>startxref</c> points at an indirect object whose dictionary is a
    /// cross-reference stream (<c>/Type /XRef</c>) carrying <c>/Root</c>. The stream body
    /// itself is inspected like every other stream by <see cref="ScanPdfStreams"/>.
    /// </summary>
    private static bool HasCrossReferenceStreamAt(ReadOnlySpan<byte> pdf, int xrefOffset)
    {
        var index = xrefOffset;
        if (!TryReadPdfUnsignedInteger(pdf, ref index, out _))
        {
            return false;
        }

        SkipPdfWhitespaceAndComments(pdf, ref index);
        if (!TryReadPdfUnsignedInteger(pdf, ref index, out _))
        {
            return false;
        }

        SkipPdfWhitespaceAndComments(pdf, ref index);
        if (!StartsWith(pdf[index..], "obj"u8)
            || !IsPdfTokenBoundary(pdf, index + "obj"u8.Length))
        {
            return false;
        }

        index += "obj"u8.Length;
        SkipPdfWhitespaceAndComments(pdf, ref index);

        var streamAt = pdf[index..].IndexOf("stream"u8);
        if (streamAt < 0 || !TryGetStreamDictionary(pdf, index, index + streamAt, out var dictionary))
        {
            return false;
        }

        if (ContainsEscapedPdfDelimiterOrWhiteSpace(dictionary))
        {
            return false;
        }

        var normalized = DecodePdfNameEscapes(dictionary) ?? dictionary.ToArray();
        return HasSingleTopLevelNameValue(normalized, "Type"u8, "XRef"u8)
            && !HasTopLevelPdfName(normalized, "Encrypt"u8)
            && normalized.AsSpan().IndexOf("/Root"u8) >= 0;
    }

    private static bool TryReadPdfUnsignedInteger(ReadOnlySpan<byte> bytes, ref int index, out int value)
    {
        value = 0;
        if (index >= bytes.Length || bytes[index] is < (byte)'0' or > (byte)'9')
        {
            return false;
        }

        long parsed = 0;
        while (index < bytes.Length && bytes[index] is >= (byte)'0' and <= (byte)'9')
        {
            parsed = (parsed * 10) + bytes[index] - (byte)'0';
            if (parsed > int.MaxValue)
            {
                return false;
            }

            index++;
        }

        if (index < bytes.Length && !IsPdfDelimiterOrWhiteSpace(bytes[index]))
        {
            return false;
        }

        value = (int)parsed;
        return true;
    }

    private static bool HasTerminalPdfEof(ReadOnlySpan<byte> pdf)
    {
        var end = pdf.Length;
        while (end > 0 && IsPdfWhiteSpace(pdf[end - 1]))
        {
            end--;
        }

        if (end < "%%EOF"u8.Length)
        {
            return false;
        }

        // Only the terminal marker is required. Earlier `%%EOF` markers are normal in
        // linearized and incrementally-saved PDFs, and they hide nothing: every byte of every
        // revision is still walked by ScanPdfStreams and the whole-buffer marker scans.
        var terminalMarkerAt = end - "%%EOF"u8.Length;
        return pdf[terminalMarkerAt..end].SequenceEqual("%%EOF"u8);
    }

    private static bool Mentions(ReadOnlySpan<byte> pdf, byte[]? deEscaped, ReadOnlySpan<byte> needle) =>
        pdf.IndexOf(needle) >= 0 || (deEscaped is not null && deEscaped.AsSpan().IndexOf(needle) >= 0);

    private static bool PdfHasActiveContent(ReadOnlySpan<byte> bytes)
    {
        foreach (var marker in PdfActiveContentMarkers)
        {
            if (bytes.IndexOf(marker) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static byte[]? DecodePdfNameEscapes(ReadOnlySpan<byte> input)
    {
        if (input.IndexOf((byte)'#') < 0)
        {
            return null;
        }

        var output = new byte[input.Length];
        var count = 0;
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] == (byte)'#'
                && index + 2 < input.Length
                && TryHexNibble(input[index + 1], out var high)
                && TryHexNibble(input[index + 2], out var low))
            {
                output[count++] = (byte)((high << 4) | low);
                index += 2;
            }
            else
            {
                output[count++] = input[index];
            }
        }

        return output.AsSpan(0, count).ToArray();
    }

    private static bool ContainsEscapedPdfDelimiterOrWhiteSpace(ReadOnlySpan<byte> input)
    {
        for (var index = 0; index + 2 < input.Length; index++)
        {
            if (input[index] == (byte)'#'
                && TryHexNibble(input[index + 1], out var high)
                && TryHexNibble(input[index + 2], out var low)
                && IsPdfDelimiterOrWhiteSpace((byte)((high << 4) | low)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryHexNibble(byte value, out int nibble)
    {
        nibble = value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
            >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
            _ => -1,
        };

        return nibble >= 0;
    }

    private static bool TrySkipPdfLiteralString(ReadOnlySpan<byte> bytes, ref int index)
    {
        var depth = 0;
        while (index < bytes.Length)
        {
            var value = bytes[index++];
            if (value == (byte)'\\')
            {
                if (index < bytes.Length)
                {
                    if (bytes[index] == (byte)'\r')
                    {
                        index++;
                        if (index < bytes.Length && bytes[index] == (byte)'\n')
                        {
                            index++;
                        }
                    }
                    else
                    {
                        index++;
                    }
                }

                continue;
            }

            if (value == (byte)'(')
            {
                depth++;
            }
            else if (value == (byte)')' && --depth == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySkipPdfHexString(ReadOnlySpan<byte> bytes, ref int index)
    {
        index++;
        while (index < bytes.Length)
        {
            if (bytes[index++] == (byte)'>')
            {
                return true;
            }
        }

        return false;
    }

    private static void SkipPdfComment(ReadOnlySpan<byte> bytes, ref int index)
    {
        while (index < bytes.Length && bytes[index] is not ((byte)'\r' or (byte)'\n'))
        {
            index++;
        }
    }

    private static void SkipPdfWhitespaceAndComments(ReadOnlySpan<byte> bytes, ref int index)
    {
        while (index < bytes.Length)
        {
            if (IsPdfWhiteSpace(bytes[index]))
            {
                index++;
            }
            else if (bytes[index] == (byte)'%')
            {
                SkipPdfComment(bytes, ref index);
            }
            else
            {
                break;
            }
        }
    }

    private static bool ContainsOnlyPdfWhitespaceAndComments(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        SkipPdfWhitespaceAndComments(bytes, ref index);
        return index == bytes.Length;
    }

    private static bool IsPdfTokenBoundary(ReadOnlySpan<byte> bytes, int index) =>
        index < 0 || index >= bytes.Length || IsPdfDelimiterOrWhiteSpace(bytes[index]);

    private static bool IsPdfDelimiterOrWhiteSpace(byte value) =>
        IsPdfWhiteSpace(value) || value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>'
            or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private static bool IsPdfWhiteSpace(byte value) =>
        value is 0 or (byte)'\t' or (byte)'\n' or (byte)'\f' or (byte)'\r' or (byte)' ';

    private static Result ScriptContentFailure() => Result.Validation(
        "The file contains embedded scripts or active content. Upload a clean PDF, JPG or PNG.");

    private static Result PdfActiveContentFailure() => Result.Validation(
        "The PDF embeds scripts, a launch action, rich media or an attached file. " +
        "Re-export it as a plain (flattened) PDF, or upload a JPG/PNG scan.");

    private static Result PdfUninspectableFailure(string reason) => Result.Validation(
        $"This PDF {reason}, so Faed cannot confirm it is free of embedded scripts. " +
        "Re-export it as a plain (flattened) PDF, or upload a JPG/PNG scan.");

    private static Result InvalidImageFailure() => Result.Validation(
        "The image is malformed or contains data outside a complete JPG or PNG structure. " +
        "Re-export it as a plain JPG or PNG image.");

    private static bool ContainsScriptMarker(ReadOnlySpan<byte> bytes)
    {
        foreach (var marker in ScriptMarkers)
        {
            if (ContainsAsciiIgnoreCase(bytes, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDisallowedBinaryPayload(ReadOnlySpan<byte> bytes, bool allowLeadingPdf = false)
    {
        var pdfAt = bytes.IndexOf("%PDF-"u8);
        if ((pdfAt >= 0 && (!allowLeadingPdf || pdfAt != 0 || bytes[5..].IndexOf("%PDF-"u8) >= 0))
            || bytes.IndexOf(SevenZipSignature) >= 0
            || bytes.IndexOf(Rar4Signature) >= 0
            || bytes.IndexOf(Rar5Signature) >= 0)
        {
            return true;
        }

        for (var index = 0; index + 20 <= bytes.Length; index++)
        {
            if (StartsWith(bytes[index..], [0x7F, (byte)'E', (byte)'L', (byte)'F'])
                && index + 16 <= bytes.Length
                && bytes[index + 4] is 1 or 2
                && bytes[index + 5] is 1 or 2
                && bytes[index + 6] == 1)
            {
                return true;
            }

            if (StartsWith(bytes[index..], "MZ"u8)
                && index + 0x40 <= bytes.Length)
            {
                var peOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(index + 0x3C, 4));
                if (peOffset >= 0x40
                    && peOffset <= bytes.Length - index - 24
                    && StartsWith(bytes[(index + peOffset)..], [(byte)'P', (byte)'E', 0x00, 0x00]))
                {
                    return true;
                }
            }

            if (StartsWith(bytes[index..], [0x50, 0x4B, 0x05, 0x06])
                && index + 22 <= bytes.Length)
            {
                var diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 4, 2));
                var centralDirectoryDisk = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 6, 2));
                var recordsOnDisk = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 8, 2));
                var totalRecords = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 10, 2));
                var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 20, 2));
                if (diskNumber == 0
                    && centralDirectoryDisk == 0
                    && recordsOnDisk == totalRecords
                    && commentLength <= bytes.Length - index - 22)
                {
                    return true;
                }
            }

            if (StartsWith(bytes[index..], [0x50, 0x4B, 0x03, 0x04])
                && index + 30 <= bytes.Length)
            {
                var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 6, 2));
                var compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(index + 18, 4));
                var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 26, 2));
                var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index + 28, 2));
                var headerLength = 30L + nameLength + extraLength;
                if (headerLength <= bytes.Length - index
                    && ((flags & 0x08) != 0 || compressedLength <= bytes.Length - index - headerLength))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsAsciiIgnoreCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needleLower)
    {
        for (var index = 0; index + needleLower.Length <= haystack.Length; index++)
        {
            var matches = true;
            for (var offset = 0; offset < needleLower.Length; offset++)
            {
                var value = haystack[index + offset];
                if (value is >= (byte)'A' and <= (byte)'Z')
                {
                    value = (byte)(value + 32);
                }

                if (value != needleLower[offset])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPngChunkType(ReadOnlySpan<byte> type)
    {
        if (type.Length != 4)
        {
            return false;
        }

        foreach (var value in type)
        {
            if (value is not (>= (byte)'A' and <= (byte)'Z')
                and not (>= (byte)'a' and <= (byte)'z'))
            {
                return false;
            }
        }

        return true;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint ComputeAdler32(ReadOnlySpan<byte> bytes)
    {
        const uint modulus = 65521;
        uint first = 1;
        uint second = 0;
        foreach (var value in bytes)
        {
            first = (first + value) % modulus;
            second = (second + first) % modulus;
        }

        return (second << 16) | first;
    }

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] SevenZipSignature = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
    private static readonly byte[] Rar4Signature = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00];
    private static readonly byte[] Rar5Signature = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00];

    private static readonly byte[][] ScriptMarkers =
    [
        "<script"u8.ToArray(),
        "<?php"u8.ToArray(),
    ];

    private static readonly byte[][] PdfActiveContentMarkers =
    [
        "/JavaScript"u8.ToArray(),
        "/Launch"u8.ToArray(),
        "/EmbeddedFile"u8.ToArray(),
        "/RichMedia"u8.ToArray(),
    ];

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private enum PdfStreamFilter
    {
        None,
        Flate,
        Dct,
    }

    /// <summary>One key of a PDF dictionary: the name's bytes and where its value starts.</summary>
    private readonly record struct PdfDictionaryEntry(int NameStart, int NameLength, int ValueAt);

    /// <summary>A resolved <c>/DecodeParms</c> predictor. <see cref="Kind"/> 1 means none.</summary>
    private readonly record struct PdfPredictor(int Kind, int BytesPerPixel, int RowBytes)
    {
        public static PdfPredictor None => new(1, 1, 0);

        public bool IsActive => Kind != 1;
    }

    private enum PdfScanStatus
    {
        NotFound,
        Found,
        Malformed,
    }

    private enum InflateStatus
    {
        Success,
        Invalid,
        BudgetExhausted,
    }

    private readonly record struct InflateResult(InflateStatus Status, byte[]? Bytes)
    {
        public static InflateResult Invalid => new(InflateStatus.Invalid, null);

        public static InflateResult BudgetExhausted => new(InflateStatus.BudgetExhausted, null);

        public static InflateResult Success(byte[] bytes) => new(InflateStatus.Success, bytes);
    }

    private readonly record struct PngPass(uint Height, int RowBytes);
}
