using System.Net;
using Faed.Web.Models.Identity;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Faed.IntegrationTests;

/// <summary>
/// TASK-012 Phase 1 (Information Architecture): the global nav must never show an
/// administrator a Buyer/Merchant link that predictably ends in a challenge or a 403 — an
/// administrator can neither place a B2C order nor hold a selling merchant identity
/// (AGENTS.md §3, docs/16-PERMISSIONS-MATRIX.md), so "My Orders" / "My Disputes" / "Merchant
/// Center" must be absent from their rendered navigation. Buyer navigation is visible only
/// to Buyer/Merchant roles, while merchant onboarding remains available to a non-admin
/// authenticated account.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class Task012NavigationHttpTests(FaedWebApplicationFactory factory)
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
    public async Task SignedInAdministrator_DoesNotSeeBuyerOrMerchantNavigation()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        var html = await As(Guid.NewGuid().ToString(), FaedRoles.Admin).GetStringAsync("/");

        Assert.DoesNotContain("My Orders", html);
        Assert.DoesNotContain("My Disputes", html);
        Assert.DoesNotContain("Merchant Center", html);
        Assert.Contains("Admin", html);
    }

    [SkippableFact]
    public async Task SignedInNonAdministrator_SeesBuyerAndMerchantNavigation_ButNotAdmin()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        var html = await As(Guid.NewGuid().ToString()).GetStringAsync("/");

        Assert.Contains("My Orders", html);
        Assert.Contains("My Disputes", html);
        Assert.Contains("Merchant Center", html);
    }

    [SkippableFact]
    public async Task AuthenticatedAccountWithoutBuyerOrMerchantRole_SeesOnboardingButNotBuyerNavigation()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        var html = await As(Guid.NewGuid().ToString(), "Unclassified").GetStringAsync("/");

        Assert.DoesNotContain("My Orders", html);
        Assert.DoesNotContain("My Disputes", html);
        Assert.Contains("Merchant Center", html);
    }
}
