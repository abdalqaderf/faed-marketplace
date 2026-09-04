using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Admin;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The consolidated admin operational screens against real SQL Server
/// (tasks/TASK-010-ANALYTICS-AND-ADMIN.md "admin can operate all MVP review queues";
/// docs/07-UI-UX-SPEC.md §7). Monitoring is read-only; catalog management is admin-gated and
/// audited (docs/08-SECURITY-AND-PRIVACY.md §2, §13).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class AdminOperationsServiceTests(FaedWebApplicationFactory factory)
{
    private static IAdminOperationsService Operations(TrustScope scope) =>
        scope.Services.GetRequiredService<IAdminOperationsService>();

    [SkippableFact]
    public async Task Dashboard_And_Monitors_ReflectRealMarketplaceState()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var operations = Operations(scope);

        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        var dealId = await scope.CreateCompletedDealAsync(sellerUserId, buyerMerchantUserId);

        // A dispute on the order and a review on the completed deal.
        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, new FileDisputeInput(
            TrustTransactionType.B2COrder, orderId, DisputeReasonCode.ItemNotAsDescribed,
            "Not as described.", []));
        Assert.True(filed.Succeeded, filed.Error);
        var review = await scope.Reviews.SubmitReviewAsync(buyerMerchantUserId, new SubmitReviewInput(
            TrustTransactionType.B2BDeal, dealId, 5, "Great."));
        Assert.True(review.Succeeded, review.Error);

        var dashboard = await operations.GetDashboardAsync();
        Assert.True(dashboard.OpenDisputes >= 1);
        Assert.True(dashboard.OrdersInProgress >= 1);

        var orders = await operations.GetOrdersAsync(AdminOrderFilter.All);
        Assert.Contains(orders.Items, o => o.Id == orderId);
        Assert.True(orders.TotalCount >= 1);

        var orderDetail = await operations.GetOrderAsync(orderId);
        Assert.NotNull(orderDetail);
        Assert.NotEmpty(orderDetail!.Items);
        Assert.Single(orderDetail.Disputes);

        var deals = await operations.GetDealsAsync(AdminDealFilter.All);
        Assert.Contains(deals.Items, d => d.Id == dealId);
        Assert.True(deals.TotalCount >= 1);

        var dealDetail = await operations.GetDealAsync(dealId);
        Assert.NotNull(dealDetail);
        Assert.Equal(B2BDealStatus.Completed, dealDetail!.Status);
        Assert.NotEmpty(dealDetail.Lines);

        var reviews = await operations.GetReviewsAsync();
        Assert.Contains(reviews.Items, r => r.TransactionId == dealId && r.Rating == 5);
        Assert.True(reviews.TotalCount >= 1);

        var audit = await operations.GetAuditLogAsync(AdminAuditLogFilter.All);
        Assert.NotEmpty(audit.Items); // listing approvals during setup are audited
    }

    [SkippableFact]
    public async Task AuditHistory_BeyondTheFormerTwoHundredRowLimit_RemainsReachableByPaging()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var operations = Operations(scope);
        var probeType = $"PagingProbe{Guid.NewGuid():N}";
        var expectedIds = new HashSet<Guid>();
        var newest = new DateTime(9998, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < 205; index++)
        {
            var log = new AdminActionLog(
                "paging-admin",
                AdminActionType.CatalogItemUpdated,
                probeType,
                index.ToString(),
                "Paging regression record.",
                newest.AddSeconds(index));
            expectedIds.Add(log.Id);
            scope.Db.AdminActionLogs.Add(log);
        }

        await scope.Db.SaveChangesAsync();
        try
        {
            var reachedIds = new HashSet<Guid>();
            for (var pageNumber = 1; pageNumber <= 5; pageNumber++)
            {
                var page = await operations.GetAuditLogAsync(AdminAuditLogFilter.All, pageNumber);
                reachedIds.UnionWith(page.Items.Where(x => x.TargetType == probeType).Select(x => x.Id));
            }

            Assert.Equal(205, reachedIds.Count);
            Assert.True(expectedIds.SetEquals(reachedIds));
        }
        finally
        {
            scope.Db.ChangeTracker.Clear();
            await scope.Db.AdminActionLogs.Where(l => l.TargetType == probeType).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task OrderAndReviewHistory_RemainsReachableOnTheSecondPage()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var operations = Operations(scope);
        var (sellerUserId, sellerMerchantId) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, sourceOrderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        await scope.CompleteOrderAsync(sellerUserId, sourceOrderId);
        var sourceOrder = await scope.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == sourceOrderId);
        var sourceItem = await scope.Db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == sourceOrderId);
        var probe = $"PageProbe{Guid.NewGuid():N}";
        var expectedOrderIds = new HashSet<Guid>();
        var expectedReviewIds = new HashSet<Guid>();
        var firstCreatedAtUtc = new DateTime(9997, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index <= Paging.AdminPageSize; index++)
        {
            var createdAtUtc = firstCreatedAtUtc.AddHours(index);
            var order = new Order(
                buyerUserId,
                sellerMerchantId,
                OrderFulfillmentType.Pickup,
                sourceOrder.MerchantLocationId,
                null,
                0m,
                sourceOrder.FulfillmentSnapshot,
                null,
                $"{probe}-{index}",
                sourceOrder.ContactPhone,
                null,
                createdAtUtc.AddHours(1),
                createdAtUtc);
            order.AddItem(
                sourceItem.ListingId,
                sourceItem.ListingVariantId,
                1,
                sourceItem.UnitPriceSnapshot,
                sourceItem.ListingTitleSnapshot,
                sourceItem.VariantSnapshot,
                sourceItem.ConditionGradeSnapshot,
                sourceItem.DiscountReasonSnapshot);
            order.Confirm(createdAtUtc.AddMinutes(1));
            order.MarkReadyForPickup(createdAtUtc.AddMinutes(2));
            order.Complete(createdAtUtc.AddMinutes(3));
            var review = new Review(
                sellerMerchantId,
                buyerUserId,
                order.Id,
                null,
                5,
                $"{probe}-{index}",
                createdAtUtc.AddMinutes(4));

            expectedOrderIds.Add(order.Id);
            expectedReviewIds.Add(review.Id);
            scope.Db.Orders.Add(order);
            scope.Db.Reviews.Add(review);
        }

        await scope.Db.SaveChangesAsync();

        var orderIds = new HashSet<Guid>();
        var reviewIds = new HashSet<Guid>();
        for (var pageNumber = 1; pageNumber <= 2; pageNumber++)
        {
            var orders = await operations.GetOrdersAsync(AdminOrderFilter.All, pageNumber);
            orderIds.UnionWith(orders.Items
                .Where(o => o.BuyerContactName.StartsWith(probe, StringComparison.Ordinal))
                .Select(o => o.Id));

            var reviews = await operations.GetReviewsAsync(pageNumber);
            reviewIds.UnionWith(reviews.Items
                .Where(r => r.Comment?.StartsWith(probe, StringComparison.Ordinal) == true)
                .Select(r => r.Id));
        }

        Assert.True(expectedOrderIds.SetEquals(orderIds));
        Assert.True(expectedReviewIds.SetEquals(reviewIds));
    }

    [SkippableFact]
    public async Task GetOrder_ForAnUnknownId_ReturnsNull()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        Assert.Null(await Operations(scope).GetOrderAsync(Guid.NewGuid()));
        Assert.Null(await Operations(scope).GetDealAsync(Guid.NewGuid()));
    }
}
