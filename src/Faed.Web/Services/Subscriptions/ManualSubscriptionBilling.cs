using Faed.Web.Services.Common;

namespace Faed.Web.Services.Subscriptions;

/// <inheritdoc />
/// <summary>
/// Manual collection (<c>BUSINESS-MODEL.md</c> §6.4): the merchant transfers the amount
/// outside the platform and an admin types in what they were given — a CliQ or bank reference,
/// or a receipt number for cash. There is nothing to charge here.
/// </summary>
public sealed class ManualSubscriptionBilling : ISubscriptionBilling
{
    public Task<Result<string>> ConfirmPaymentAsync(
        Guid merchantProfileId, decimal amountJod, string paymentReference, CancellationToken cancellationToken = default)
    {
        var trimmed = (paymentReference ?? string.Empty).Trim();
        return Task.FromResult(trimmed.Length is 0 or > 200
            ? Result<string>.Validation("Enter the payment reference (CliQ, bank transfer or cash receipt), up to 200 characters.")
            : Result<string>.Success(trimmed));
    }
}
