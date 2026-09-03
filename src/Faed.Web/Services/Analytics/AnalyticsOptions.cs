namespace Faed.Web.Services.Analytics;

/// <summary>
/// Tuning for merchant recovery analytics (docs/01-PRD.md §14, docs/03-BUSINESS-RULES.md §15,
/// tasks/TASK-010-ANALYTICS-AND-ADMIN.md). The one value here is a threshold, not a rule the
/// analytics invent: how long a still-unsold live listing may sit before it is flagged as
/// stale inventory. It lives in configuration so it can be tuned without a code change
/// (docs/13-OPEN-QUESTIONS.md "Configurable durations should live in configuration").
/// </summary>
public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>
    /// A published listing older than this that has never sold a unit is reported as a stale
    /// listing. Default: 30 days — a safe, reversible starting point; no doc fixes the number
    /// (docs/13-OPEN-QUESTIONS.md).
    /// </summary>
    public TimeSpan StaleListingThreshold { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>Fails startup when the stale-listing duration is not a usable positive value.</summary>
public sealed class AnalyticsOptionsValidator : Microsoft.Extensions.Options.IValidateOptions<AnalyticsOptions>
{
    public Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, AnalyticsOptions options) =>
        options.StaleListingThreshold > TimeSpan.Zero
            ? Microsoft.Extensions.Options.ValidateOptionsResult.Success
            : Microsoft.Extensions.Options.ValidateOptionsResult.Fail(
                "Analytics:StaleListingThreshold must be a positive duration.");
}
