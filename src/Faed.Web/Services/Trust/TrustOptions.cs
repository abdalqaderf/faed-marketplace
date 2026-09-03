namespace Faed.Web.Services.Trust;

/// <summary>
/// Configurable limits for the post-transaction trust features (disputes and reviews,
/// docs/10-IMPLEMENTATION-PLAN.md Phase 8). Upload ceilings live in configuration, not as
/// domain constants (docs/06-ARCHITECTURE.md §11).
/// </summary>
public sealed class TrustOptions
{
    public const string SectionName = "Trust";

    /// <summary>Maximum evidence files a participant may attach to a single dispute.</summary>
    public int MaxEvidenceFilesPerDispute { get; set; } = 8;

    /// <summary>Maximum accepted size for a single dispute evidence file.</summary>
    public long MaxEvidenceBytes { get; set; } = 10 * 1024 * 1024;
}
