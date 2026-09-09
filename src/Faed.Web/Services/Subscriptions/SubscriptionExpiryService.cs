using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Subscriptions;

/// <summary>
/// Periodically moves subscriptions past their <c>ExpiresAtUtc</c> to
/// <see cref="Faed.Web.Models.Enums.SubscriptionStatus.Expired"/> and hides that merchant's
/// live listings. Nothing waits on a human forever
/// (<c>BUSINESS-MODEL.md</c> §8.1): a lapsed subscription resolves itself on schedule instead
/// of staying wrongly Active until an admin happens to notice. Each sweep runs in its own DI
/// scope; expiry itself is idempotent, so an overlapping run or a renewal racing the sweep
/// cannot double-expire a subscription.
/// </summary>
public sealed class SubscriptionExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<SubscriptionOptions> options,
    ILogger<SubscriptionExpiryService> logger) : BackgroundService
{
    private readonly SubscriptionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.ExpirySweepInterval > TimeSpan.Zero
            ? _options.ExpirySweepInterval
            : TimeSpan.FromHours(1);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failing sweep must not take the host down; the next tick tries again.
                logger.LogError(ex, "Subscription-expiry sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var expired = await subscriptions.ExpireDueSubscriptionsAsync(cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("Subscription-expiry sweep expired {Count} subscription(s)", expired);
        }
    }
}
