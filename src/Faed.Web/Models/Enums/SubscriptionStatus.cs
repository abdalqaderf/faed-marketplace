namespace Faed.Web.Models.Enums;

/// <summary>
/// A merchant's subscription lifecycle. Publishing requires <see cref="Active"/>; the other
/// three states all mean "cannot publish", for different reasons.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Plan chosen, payment not yet recorded by an admin.</summary>
    PendingActivation = 0,

    /// <summary>Paid and within the period. Can publish up to the plan's quota.</summary>
    Active = 1,

    /// <summary>The period ended without renewal. Listings are hidden, not deleted.</summary>
    Expired = 2,

    /// <summary>Ended early by an admin. Same effect as <see cref="Expired"/>.</summary>
    Cancelled = 3,
}
