using System.Net;
using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Targeted coverage for the TASK-011 final-review hardening findings
/// (docs/24-DELIVERY-AND-HARDENING.md, docs/16-PERMISSIONS-MATRIX.md).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class Task011HardeningTests(FaedWebApplicationFactory factory)
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient As(string userId, params string[] roles)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    // ---- Finding 4: an administrator cannot hold a selling merchant identity ----------

    [SkippableFact]
    public async Task Administrator_CannotCreateAMerchantApplication_AndCannotBeApprovedIntoOne()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var trust = new TrustScope(factory);
        var verification = trust.Services.GetRequiredService<IMerchantVerificationService>();
        var users = trust.Services.GetRequiredService<UserManager<ApplicationUser>>();

        var adminId = await trust.CreateUserAsync(FaedRoles.Admin);
        var draft = await verification.SaveDraftAsync(
            adminId, new MerchantApplicationInput("Admin's Storefront", "a@b.co", null));
        Assert.Equal(ResultErrorKind.Forbidden, draft.ErrorKind);
        Assert.False(await trust.Db.MerchantProfiles.AnyAsync(p => p.UserId == adminId));

        // A legitimate applicant who is later made an administrator can no longer be approved.
        var userId = await trust.CreateUserAsync();
        var save = await verification.SaveDraftAsync(
            userId, new MerchantApplicationInput("Legit Storefront", "c@d.co", null));
        Assert.True(save.Succeeded, save.Error);
        Assert.True((await verification.AddDocumentAsync(userId, new AddVerificationDocumentInput(
            MerchantVerificationDocumentType.CommercialRegistration,
            TestDocuments.MinimalPdfStream(), "reg.pdf", "application/pdf", TestDocuments.MinimalPdf.Length))).Succeeded);
        Assert.True((await verification.SubmitForReviewAsync(userId)).Succeeded);

        var user = await users.FindByIdAsync(userId);
        Assert.True((await users.AddToRoleAsync(user!, FaedRoles.Admin)).Succeeded);

        var approve = await verification.ApproveAsync(adminId, save.Value);
        Assert.Equal(ResultErrorKind.Forbidden, approve.ErrorKind);
        Assert.Equal(MerchantVerificationStatus.PendingReview,
            (await trust.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId)).VerificationStatus);
    }

    [SkippableFact]
    public async Task ApprovedMerchantRoutes_AreForbiddenToAnAdministrator_EvenWithAnApprovedProfile()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var trust = new TrustScope(factory);
        var (merchantUserId, _) = await trust.CreateApprovedMerchantAsync();

        var asMerchant = As(merchantUserId);
        Assert.Equal(HttpStatusCode.OK, (await asMerchant.GetAsync("/Merchant/Listings")).StatusCode);

        var asAdmin = As(merchantUserId, FaedRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await asAdmin.GetAsync("/Merchant/Listings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asAdmin.GetAsync("/Merchant/Inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asAdmin.GetAsync("/Merchant/StoreSettings")).StatusCode);
    }

    // Finding 7 (private listing media is indistinguishable from a missing record) is
    // covered end to end by PublicMarketplaceHttpTests: a suspended listing's real image id
    // and a bogus id both return 404 to an unauthorized caller.

    // ---- Finding 6: unbounded list surfaces are database-paged -----------------------

    [SkippableFact]
    public async Task BuyerOrderHistory_IsDatabasePaged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var trust = new TrustScope(factory);
        var (sellerUserId, merchantId) = await trust.CreateApprovedMerchantAsync();
        var (buyerUserId, seedOrderId) = await trust.CreateConfirmedOrderAsync(sellerUserId);

        var seedOrder = await trust.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == seedOrderId);
        var seedItem = await trust.Db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == seedOrderId);

        // One confirmed order already exists; add enough to spill onto a second page.
        const int extra = Paging.DefaultPageSize;
        for (var i = 0; i < extra; i++)
        {
            var order = new Order(
                buyerUserId, merchantId, OrderFulfillmentType.Pickup, seedOrder.MerchantLocationId, null,
                0m, seedOrder.FulfillmentSnapshot, null, "Buyer", seedOrder.ContactPhone, null,
                DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddSeconds(i));
            order.AddItem(
                seedItem.ListingId, seedItem.ListingVariantId, 1, seedItem.UnitPriceSnapshot,
                seedItem.ListingTitleSnapshot, seedItem.VariantSnapshot, seedItem.ConditionGradeSnapshot,
                seedItem.DiscountReasonSnapshot);
            trust.Db.Orders.Add(order);
        }

        await trust.Db.SaveChangesAsync();

        var expectedTotal = extra + 1;
        var page1 = await trust.Orders.GetMyOrdersAsync(buyerUserId, page: 1);
        Assert.Equal(expectedTotal, page1.TotalCount);
        Assert.Equal(Paging.DefaultPageSize, page1.Items.Count);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        var page2 = await trust.Orders.GetMyOrdersAsync(buyerUserId, page: 2);
        Assert.Equal(expectedTotal - Paging.DefaultPageSize, page2.Items.Count);
        Assert.True(page2.HasPreviousPage);
        Assert.False(page2.HasNextPage);

        // No row is served on both pages.
        Assert.Empty(page1.Items.Select(o => o.Id).Intersect(page2.Items.Select(o => o.Id)));

        // The list page and its shared _Pagination partial render for both pages.
        var client = As(buyerUserId);
        var pageOne = await client.GetAsync("/Buyer/Orders");
        Assert.Equal(HttpStatusCode.OK, pageOne.StatusCode);
        var pageOneHtml = await pageOne.Content.ReadAsStringAsync();
        Assert.Contains("Page 1 of 2", pageOneHtml);
        Assert.Contains("faed-pagination", pageOneHtml);

        var pageTwo = await client.GetAsync("/Buyer/Orders?page=2");
        Assert.Equal(HttpStatusCode.OK, pageTwo.StatusCode);
        Assert.Contains("Page 2 of 2", await pageTwo.Content.ReadAsStringAsync());
    }

    // ---- Finding 6 support: per-transaction dispute lookup stays bounded and scoped ---

    [SkippableFact]
    public async Task GetDisputesForTransaction_ReturnsOnlyThatTransactionsDisputes()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var trust = new TrustScope(factory);
        var (sellerUserId, _) = await trust.CreateApprovedMerchantAsync();
        var (buyerAUserId, _) = await trust.CreateApprovedMerchantAsync();

        var (orderBuyerId, orderId) = await trust.CreateConfirmedOrderAsync(sellerUserId);
        var dealId = await trust.CreateCompletedDealAsync(sellerUserId, buyerAUserId);

        var orderDispute = await trust.Disputes.FileDisputeAsync(orderBuyerId, new FileDisputeInput(
            TrustTransactionType.B2COrder, orderId, DisputeReasonCode.ItemNotAsDescribed, "Order issue.", []));
        Assert.True(orderDispute.Succeeded, orderDispute.Error);

        var dealDispute = await trust.Disputes.FileDisputeAsync(buyerAUserId, new FileDisputeInput(
            TrustTransactionType.B2BDeal, dealId, DisputeReasonCode.MissingItems, "Deal issue.", []));
        Assert.True(dealDispute.Succeeded, dealDispute.Error);

        var forOrder = await trust.Disputes.GetDisputesForTransactionAsync(
            orderBuyerId, TrustTransactionType.B2COrder, orderId);
        Assert.Equal(orderDispute.Value, Assert.Single(forOrder).Id);

        var forDeal = await trust.Disputes.GetDisputesForTransactionAsync(
            buyerAUserId, TrustTransactionType.B2BDeal, dealId);
        Assert.Equal(dealDispute.Value, Assert.Single(forDeal).Id);
    }
}
