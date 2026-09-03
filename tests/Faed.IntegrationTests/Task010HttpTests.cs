using System.Net;
using Faed.Web.Models.Identity;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Faed.IntegrationTests;

/// <summary>
/// The TASK-010 routes through the real MVC pipeline: merchant analytics and every
/// consolidated admin screen are enforced server-side (docs/08-SECURITY-AND-PRIVACY.md §2,
/// docs/16-PERMISSIONS-MATRIX.md). Authorization is asserted at the HTTP surface, not only
/// at the service layer.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class Task010HttpTests(FaedWebApplicationFactory factory)
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

    public static TheoryData<string> AdminRoutes =>
    [
        "/Admin",
        "/Admin/Transactions/Orders",
        "/Admin/Transactions/Deals",
        "/Admin/Catalog",
        "/Admin/Reviews",
        "/Admin/AuditLog",
    ];

    [SkippableTheory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminScreen_Anonymous_IsChallenged_AndNonAdmin_IsForbidden(string route)
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(Guid.NewGuid().ToString()).GetAsync(route)).StatusCode);
    }

    [SkippableTheory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminScreen_RendersForAnAdministrator(string route)
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        var response = await As(Guid.NewGuid().ToString(), FaedRoles.Admin).GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task AdminCatalogPost_WithoutAnAntiforgeryToken_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await As(Guid.NewGuid().ToString(), FaedRoles.Admin)
            .PostAsync("/Admin/Catalog/CreateBrand", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["name"] = "Nope" }));

        // Every state-changing admin POST is antiforgery-protected (docs/08-SECURITY-AND-PRIVACY.md §5);
        // the server-side admin recheck in AdminCatalogService is covered by AdminCatalogServiceTests.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task MerchantAnalytics_Anonymous_IsChallenged_AndAPlainUser_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/Merchant/Analytics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(Guid.NewGuid().ToString()).GetAsync("/Merchant/Analytics")).StatusCode);
    }

    [SkippableFact]
    public async Task MerchantAnalytics_RendersForAnApprovedMerchant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        var response = await As(merchantUserId).GetAsync("/Merchant/Analytics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Recovery analytics", await response.Content.ReadAsStringAsync());
    }
}
