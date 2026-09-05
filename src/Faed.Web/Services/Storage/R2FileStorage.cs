using System.Net;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Faed.Web.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Storage;

public sealed partial class R2FileStorage : IFileStorage, IDisposable
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png"
        };

    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly ILogger<R2FileStorage> _logger;

    public R2FileStorage(
        IOptions<R2FileStorageOptions> options,
        ILogger<R2FileStorage> logger)
    {
        _logger = logger;

        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.AccountId))
        {
            throw new InvalidOperationException(
                "FileStorage:R2:AccountId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.AccessKeyId))
        {
            throw new InvalidOperationException(
                "FileStorage:R2:AccessKeyId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.SecretAccessKey))
        {
            throw new InvalidOperationException(
                "FileStorage:R2:SecretAccessKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.BucketName))
        {
            throw new InvalidOperationException(
                "FileStorage:R2:BucketName is not configured.");
        }

        _bucketName = settings.BucketName.Trim();

        var credentials = new BasicAWSCredentials(
            settings.AccessKeyId,
            settings.SecretAccessKey);

        var config = new AmazonS3Config
        {
            ServiceURL =
                $"https://{settings.AccountId.Trim()}.r2.cloudflarestorage.com"
        };

        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<string> SaveAsync(
        string container,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeContainer = SanitizeSegment(container);

        var extension =
            Path.GetExtension(originalFileName ?? string.Empty)
                .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            extension = string.Empty;
        }

        var objectKey =
            $"{safeContainer}/{Guid.NewGuid():N}{extension}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,

            // Do not dispose a stream owned by the caller.
            AutoCloseStream = false,

            // Preserve the same stream-position behavior as LocalFileStorage.
            AutoResetStreamPosition = false,

            // Required for Cloudflare R2 with AWSSDK.S3.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _client.PutObjectAsync(request, cancellationToken);

        return objectKey;
    }

    public async Task<Stream?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsWellFormedKey(objectKey))
        {
            _logger.LogWarning(
                "Rejected malformed R2 storage object key.");

            return null;
        }

        try
        {
            var response = await _client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                },
                cancellationToken);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == HttpStatusCode.NotFound
                  || string.Equals(
                      exception.ErrorCode,
                      "NoSuchKey",
                      StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public async Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsWellFormedKey(objectKey))
        {
            _logger.LogWarning(
                "Rejected malformed R2 storage object key.");

            return;
        }

        await _client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static bool IsWellFormedKey(string? objectKey) =>
        !string.IsNullOrWhiteSpace(objectKey)
        && !objectKey.Contains("..", StringComparison.Ordinal)
        && !Path.IsPathRooted(objectKey)
        && KeyPattern().IsMatch(objectKey);

    private static string SanitizeSegment(string segment)
    {
        var cleaned = SegmentPattern()
            .Replace(segment ?? string.Empty, "-")
            .Trim('-', '.');

        return string.IsNullOrEmpty(cleaned)
            ? "misc"
            : cleaned;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9/_.-]{0,398}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"[^a-zA-Z0-9_-]")]
    private static partial Regex SegmentPattern();
}
