namespace Faed.Web.Services.Subscriptions;

/// <summary>Configurable subscription policy.</summary>
public sealed class SubscriptionOptions
{
    public const string SectionName = "Subscriptions";

    /// <summary>How often the background sweep looks for expired subscriptions. Default one hour.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromHours(1);
}
