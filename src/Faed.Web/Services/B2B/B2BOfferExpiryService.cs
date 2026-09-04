using Microsoft.Extensions.Options;

namespace Faed.Web.Services.B2B;

/// <summary>
/// Periodically closes open negotiations whose current offer has lapsed.
/// Each sweep runs in its own DI scope; the transition is idempotent, so a sweep
/// that overlaps a merchant response, or that runs twice, cannot mis-close a negotiation
/// </summary>
public sealed class B2BOfferExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<B2BNegotiationOptions> options,
    ILogger<B2BOfferExpiryService> logger) : BackgroundService
{
    private readonly B2BNegotiationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.ExpirySweepInterval > TimeSpan.Zero
            ? _options.ExpirySweepInterval
            : TimeSpan.FromMinutes(15);

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
                logger.LogError(ex, "B2B offer-expiry sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var negotiations = scope.ServiceProvider.GetRequiredService<IB2BNegotiationService>();
        var expired = await negotiations.ExpireLapsedNegotiationsAsync(cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("B2B offer-expiry sweep expired {Count} negotiation(s)", expired);
        }
    }
}
