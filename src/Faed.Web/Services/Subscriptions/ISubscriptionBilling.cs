using Faed.Web.Services.Common;

namespace Faed.Web.Services.Subscriptions;

/// <summary>
/// Abstraction over collecting a subscription payment (<c>BUSINESS-MODEL.md</c> §6.4). Faed
/// never touches merchant money — the merchant pays by CliQ, bank transfer or cash outside the
/// platform, and an admin records the reference here. This seam exists so a real payment
/// gateway can be plugged in later (charging a card and returning its own transaction id)
/// without any application code changing, the same pattern as <c>IFileStorage</c>.
/// </summary>
public interface ISubscriptionBilling
{
    /// <summary>
    /// Confirms one period's payment and returns the reference to store against the
    /// subscription. The manual implementation performs no charge — a human already collected
    /// the money — it only validates that the admin entered a usable reference.
    /// </summary>
    Task<Result<string>> ConfirmPaymentAsync(
        Guid merchantProfileId, decimal amountJod, string paymentReference, CancellationToken cancellationToken = default);
}
