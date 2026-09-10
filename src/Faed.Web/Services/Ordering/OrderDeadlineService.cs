using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Ordering;

/// <summary>
/// Runs the three order deadlines that keep the system from waiting on a person forever
/// (docs/BUSINESS-MODEL.md §8.2): an unconfirmed reservation is cancelled after 12 hours, a
/// confirmed order the merchant never prepares becomes a no-show after 48, and an order left
/// ready for pickup with no collection is closed after 72. Each sweep runs in its own DI
/// scope and is idempotent, so a sweep that overlaps a merchant action, or that runs twice,
/// cannot double-move stock.
/// </summary>
public sealed class OrderDeadlineService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderingOptions> options,
    ILogger<OrderDeadlineService> logger) : BackgroundService
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
                // A failing sweep must not take the host down; the next tick tries again.
                logger.LogError(ex, "Order-deadline sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var expired = await orders.ReleaseExpiredReservationsAsync(cancellationToken);
        var noShow = await orders.ExpireUnhandledConfirmedOrdersAsync(cancellationToken);
        var closed = await orders.CloseStaleReadyOrdersAsync(cancellationToken);

        if (expired + noShow + closed > 0)
        {
            logger.LogInformation(
                "Order-deadline sweep: {Expired} reservation(s) expired, {NoShow} no-show(s), {Closed} auto-closed",
                expired, noShow, closed);
        }
    }
}
