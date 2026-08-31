namespace Faed.Web.Services.Storage;

/// <summary>
/// Configuration for the development <see cref="LocalFileStorage"/>. In production this
/// abstraction is backed by a cloud object store instead (docs/06-ARCHITECTURE.md §8).
/// </summary>
public sealed class LocalFileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Absolute path to the private storage root. Must be outside <c>wwwroot</c>. When left
    /// empty the web host sets it to <c>{ContentRoot}/App_Data/private-storage</c>.
    /// </summary>
    public string LocalRootPath { get; set; } = string.Empty;
}
