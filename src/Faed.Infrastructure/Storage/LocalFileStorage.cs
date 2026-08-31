using System.Text.RegularExpressions;
using Faed.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Infrastructure.Storage;

/// <summary>
/// Development <see cref="IFileStorage"/> that writes to a private directory on disk,
/// outside <c>wwwroot</c> (docs/08-SECURITY-AND-PRIVACY.md §3). Object keys are generated
/// server-side and validated on read to prevent path traversal.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };

    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;

        var configured = options.Value.LocalRootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "FileStorage:LocalRootPath is not set. The web host must provide a private storage root.");
        }

        _root = Path.GetFullPath(configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string container,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var safeContainer = SanitizeSegment(container);
        var extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            extension = string.Empty;
        }

        var objectKey = $"{safeContainer}/{Guid.NewGuid():N}{extension}";
        var fullPath = ResolveWithinRoot(objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return objectKey;
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!IsWellFormedKey(objectKey))
        {
            _logger.LogWarning("Rejected malformed storage object key.");
            return Task.FromResult<Stream?>(null);
        }

        var fullPath = ResolveWithinRoot(objectKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (IsWellFormedKey(objectKey))
        {
            var fullPath = ResolveWithinRoot(objectKey);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }

    private string ResolveWithinRoot(string objectKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, objectKey));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved storage path escapes the storage root.");
        }

        return combined;
    }

    private static bool IsWellFormedKey(string? objectKey) =>
        !string.IsNullOrWhiteSpace(objectKey)
        && !objectKey.Contains("..", StringComparison.Ordinal)
        && !Path.IsPathRooted(objectKey)
        && KeyPattern().IsMatch(objectKey);

    private static string SanitizeSegment(string segment)
    {
        var cleaned = SegmentPattern().Replace(segment ?? string.Empty, "-").Trim('-', '.');
        return string.IsNullOrEmpty(cleaned) ? "misc" : cleaned;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9/_.-]{0,398}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"[^a-zA-Z0-9_-]")]
    private static partial Regex SegmentPattern();
}
