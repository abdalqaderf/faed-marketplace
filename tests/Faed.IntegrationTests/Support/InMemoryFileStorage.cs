using System.Collections.Concurrent;
using Faed.Application.Abstractions;

namespace Faed.IntegrationTests.Support;

/// <summary>In-memory <see cref="IFileStorage"/> so document tests do not touch disk.</summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public int Count => _files.Count;

    public async Task<string> SaveAsync(string container, Stream content, string originalFileName, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var key = $"{container}/{Guid.NewGuid():N}";
        _files[key] = buffer.ToArray();
        return key;
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_files.TryGetValue(objectKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }
}
