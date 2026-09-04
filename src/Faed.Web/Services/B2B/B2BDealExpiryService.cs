using Microsoft.Extensions.Options;

namespace Faed.Web.Services.B2B;

/// <summary>
/// Periodically releases the reserved stock of accepted B2B deals whose reservation window
/// elapsed before the selling merchant started fulfilling them.
/// Each sweep
/// runs in its own DI scope; the release is idempotent, so a sweep that overlaps a merchant
/// action, or that runs twice, cannot double-release stock
/// </summary>
public sealed class B2BDealExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<B2BDealOptions> options,
    ILogger<B2BDealExpiryService> logger) : BackgroundService
{
    private readonly B2BDealOptions _options = options.Value;

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
                logger.LogError(ex, "B2B deal-expiry sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var deals = scope.ServiceProvider.GetRequiredService<IB2BDealService>();
        var released = await deals.ReleaseExpiredDealReservationsAsync(cancellationToken);
        if (released > 0)
        {
            logger.LogInformation("B2B deal-expiry sweep released {Count} deal(s)", released);
        }
    }
}
