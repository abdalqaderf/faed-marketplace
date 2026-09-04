using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Ordering;

/// <summary>
/// Periodically releases the reserved stock of B2C orders whose reservation window elapsed
/// before the merchant confirmed them. Each sweep runs in its own DI scope; the release itself is
/// idempotent, so a sweep that overlaps a merchant confirmation, or that runs twice, cannot
/// double-release stock.
/// </summary>
public sealed class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderingOptions> options,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    private readonly OrderingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.ExpirySweepInterval > TimeSpan.Zero
            ? _options.ExpirySweepInterval
            : TimeSpan.FromMinutes(5);

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
                // A failing sweep must not take the host down; the next tick tries again
                logger.LogError(ex, "Reservation-expiry sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var released = await orders.ReleaseExpiredReservationsAsync(cancellationToken);
        if (released > 0)
        {
            logger.LogInformation("Reservation-expiry sweep released {Count} order(s)", released);
        }
    }
}
