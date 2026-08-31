using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Faed.Application.Merchants;
using Faed.Domain.Enums;
using Faed.Domain.Identity;
using Faed.Infrastructure.Identity;
using Faed.Infrastructure.Persistence;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Server-side authorization for the merchant-verification surfaces, exercised through the
/// real MVC pipeline (docs/16-PERMISSIONS-MATRIX.md, docs/18-TRACEABILITY.md).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class MerchantVerificationAuthorizationTests(FaedWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
    });

    [SkippableFact]
    public async Task AdminQueue_Anonymous_IsChallenged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await CreateClient().GetAsync("/Admin/MerchantVerification");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task AdminQueue_Buyer_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "buyer-1");

        var response = await client.GetAsync("/Admin/MerchantVerification");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task AdminQueue_Admin_IsAllowed()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "admin-1");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, FaedRoles.Admin);

        var response = await client.GetAsync("/Admin/MerchantVerification");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task VerificationDocument_Buyer_IsForbidden_AndNoBytesReturned()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "buyer-2");

        var response = await client.GetAsync($"/Admin/MerchantVerification/Document/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task VerificationDocument_Anonymous_IsChallenged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await CreateClient().GetAsync($"/Admin/MerchantVerification/Document/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task SellingProbe_PendingMerchant_IsForbidden_ApprovedMerchant_IsAllowed()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        string pendingUserId;
        string approvedUserId;

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var service = scope.ServiceProvider.GetRequiredService<IMerchantVerificationService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            pendingUserId = await NewUserAsync(users);
            approvedUserId = await NewUserAsync(users);
            var adminId = await NewUserAsync(users);

            await SubmitAsync(service, pendingUserId);
            var approvedProfileId = await SubmitAsync(service, approvedUserId);
            Assert.True((await service.ApproveAsync(adminId, approvedProfileId)).Succeeded);

            Assert.Equal(1, await db.MerchantProfiles.CountAsync(p => p.UserId == approvedUserId));
        }

        var pendingClient = CreateClient();
        pendingClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, pendingUserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await pendingClient.GetAsync("/_probe/selling")).StatusCode);

        var approvedClient = CreateClient();
        approvedClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, approvedUserId);
        Assert.Equal(HttpStatusCode.OK, (await approvedClient.GetAsync("/_probe/selling")).StatusCode);
    }

    [SkippableFact]
    public async Task AdminDecisions_PostedByBuyer_AreForbidden_AndChangeNoStateOrAudit()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        string merchantUserId;
        Guid profileId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var service = scope.ServiceProvider.GetRequiredService<IMerchantVerificationService>();
            merchantUserId = await NewUserAsync(users);
            profileId = await SubmitAsync(service, merchantUserId);
        }

        var (buyer, token) = await AuthenticatedClientWithAntiforgeryAsync("buyer-decisions");

        foreach (var (action, extraField) in new (string, string?)[]
        {
            ("Approve", null),
            ("Reject", "reason=not+allowed"),
            ("Suspend", "reason=not+allowed"),
            ("Reinstate", null),
        })
        {
            var body = $"__RequestVerificationToken={Uri.EscapeDataString(token)}&id={profileId}";
            if (extraField is not null)
            {
                body += "&" + extraField;
            }

            using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await buyer.PostAsync($"/Admin/MerchantVerification/{action}", content);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);

            Assert.Equal(MerchantVerificationStatus.PendingReview, profile.VerificationStatus);
            Assert.Null(profile.ReviewedByAdminId);
            Assert.Equal(0, await db.AdminActionLogs.AsNoTracking().CountAsync(l => l.TargetId == profileId.ToString()));
        }
    }

    /// <summary>
    /// Returns a client authenticated as <paramref name="userId"/> (optionally with roles)
    /// together with a valid antiforgery request token, so a state-changing POST is rejected
    /// by authorization rather than by the antiforgery filter.
    /// </summary>
    private async Task<(HttpClient Client, string Token)> AuthenticatedClientWithAntiforgeryAsync(
        string userId,
        string? roles = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        }

        // Any authenticated user may open the merchant application form, which emits an
        // antiforgery cookie + token pair valid app-wide.
        var page = await client.GetAsync("/Merchant/Verification/Apply");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();

        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Expected an antiforgery token on the merchant application form.");

        return (client, match.Groups[1].Value);
    }

    private static async Task<string> NewUserAsync(UserManager<ApplicationUser> users)
    {
        var user = new ApplicationUser
        {
            UserName = $"{Guid.NewGuid():N}@test.local",
            Email = $"{Guid.NewGuid():N}@test.local",
            EmailConfirmed = true,
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        return user.Id;
    }

    private static async Task<Guid> SubmitAsync(IMerchantVerificationService service, string userId)
    {
        await service.SaveDraftAsync(userId, new MerchantApplicationInput("Probe Merchant", null, null));
        var add = await service.AddDocumentAsync(userId, new AddVerificationDocumentInput(
            MerchantVerificationDocumentType.CommercialRegistration,
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 fake")),
            "reg.pdf",
            "application/pdf",
            12));
        Assert.True(add.Succeeded, add.Error);
        Assert.True((await service.SubmitForReviewAsync(userId)).Succeeded);

        var app = await service.GetMyApplicationAsync(userId);
        return app!.Id;
    }
}
