namespace Faed.Application.Abstractions;

/// <summary>
/// Abstraction over private object storage (docs/06-ARCHITECTURE.md §8,
/// docs/08-SECURITY-AND-PRIVACY.md §3). Implementations must:
/// generate the object key server-side, never expose a public URL, and keep bytes
/// outside any web-served directory.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> under a newly generated, unguessable object key
    /// within <paramref name="container"/> and returns that key. The original file name is
    /// used only to derive a safe extension.
    /// </summary>
    Task<string> SaveAsync(
        string container,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored object for reading, or <c>null</c> if the key does not resolve.</summary>
    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored object if it exists. Used only for hard cleanup paths.</summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
