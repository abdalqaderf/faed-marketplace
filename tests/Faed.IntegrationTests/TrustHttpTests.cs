using System.Net;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Trust;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Faed.IntegrationTests;

/// <summary>
/// Trust routes through the real MVC pipeline (tasks/TASK-009-TRUST.md "Exit criteria";
/// docs/16-PERMISSIONS-MATRIX.md "Resolve dispute — Admin only", "File eligible dispute —
/// Admin ❌"). Authorization is asserted at the HTTP surface, not only at the service layer.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class TrustHttpTests(FaedWebApplicationFactory factory)
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

    [SkippableFact]
    public async Task BuyerDisputes_Anonymous_IsChallenged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/Buyer/Disputes")).StatusCode);
    }

    [SkippableFact]
    public async Task BuyerDisputes_ForAnAdministrator_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await As(Guid.NewGuid().ToString(), FaedRoles.Admin).GetAsync("/Buyer/Disputes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task AdminDisputeQueue_Anonymous_IsChallenged_AndNonAdmin_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/Admin/Disputes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(Guid.NewGuid().ToString()).GetAsync("/Admin/Disputes")).StatusCode);
    }

    [SkippableFact]
    public async Task DisputeEvidence_Anonymous_IsChallenged_AndAGuessedId_IsNotRevealed()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Anonymous().GetAsync($"/dispute-evidence/{Guid.NewGuid()}")).StatusCode);

        var signedIn = await As(Guid.NewGuid().ToString()).GetAsync($"/dispute-evidence/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, signedIn.StatusCode);
    }

    [SkippableFact]
    public async Task BuyerDisputeIndex_RendersForASignedInBuyer()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await As(Guid.NewGuid().ToString()).GetAsync("/Buyer/Disputes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("My disputes", await response.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task AdminDisputeQueue_RendersForAnAdministrator()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await As(Guid.NewGuid().ToString(), FaedRoles.Admin).GetAsync("/Admin/Disputes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Disputes", await response.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task MerchantReviewsAndDisputes_RequireAnApprovedMerchant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(Guid.NewGuid().ToString()).GetAsync("/Merchant/Reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(Guid.NewGuid().ToString()).GetAsync("/Merchant/Disputes")).StatusCode);
    }

    [SkippableFact]
    public async Task SellingMerchant_CanReachTheB2COrderDisputeForm_ButAnUnrelatedMerchantCannot()
    {
        // Finding 6: the merchant-side B2C dispute flow exists end to end. The "raise a dispute"
        // form for a B2C order is reachable by the selling merchant and 404s for anyone else —
        // the same participant gate DisputeService enforces (docs/16-PERMISSIONS-MATRIX.md).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (strangerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        var url = $"/Merchant/Disputes/Create?type={TrustTransactionType.B2COrder}&id={orderId}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(strangerUserId).GetAsync(url)).StatusCode);

        var asSeller = await As(merchantUserId).GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, asSeller.StatusCode);
        Assert.Contains("Raise a dispute", await asSeller.Content.ReadAsStringAsync());

        // The order page offers the affordance too.
        var orderPage = await As(merchantUserId).GetAsync($"/Merchant/Orders/Details/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderPage.StatusCode);
        Assert.Contains("/Merchant/Disputes/Create", await orderPage.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task AfterADisputeIsResolved_TheOrderPageOffersANewDispute_AndKeepsTheClosedOneAsHistory()
    {
        // Finding 5: a closed dispute is history — it stays visible but must not suppress a new
        // filing when the authoritative rules allow one.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, new FileDisputeInput(
            TrustTransactionType.B2COrder, orderId, DisputeReasonCode.MissingItems, "Missing the belt.", []));
        Assert.True(filed.Succeeded, filed.Error);

        // While it is open, the buyer's order page must not offer a second dispute.
        var whileOpen = await As(buyerUserId).GetAsync($"/Buyer/Orders/Details/{orderId}");
        Assert.DoesNotContain("Disputes/Create", await whileOpen.Content.ReadAsStringAsync());

        Assert.True((await scope.Disputes.StartReviewAsync(adminUserId, filed.Value)).Succeeded);
        Assert.True((await scope.Disputes.RejectAsync(adminUserId, filed.Value, "Belt was listed as not included.")).Succeeded);

        var afterClose = await As(buyerUserId).GetAsync($"/Buyer/Orders/Details/{orderId}");
        Assert.Equal(HttpStatusCode.OK, afterClose.StatusCode);
        var html = await afterClose.Content.ReadAsStringAsync();
        Assert.Contains("Disputes/Create", html);                             // a new dispute is offered again
        Assert.Contains($"Disputes/Details/{filed.Value}", html);             // the closed one is still linked as history
    }
}
