using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Faed.IntegrationTests.Support;
using Faed.Web.Authorization;
using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// TASK-015 regression coverage for the real Identity registration page and the resulting
/// Buyer authorization identity.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class Task015IdentityTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task Register_CreatesBuyerWhoSatisfiesTheB2CPolicyAndCanOpenBuyerOrders()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var email = $"task015-{Guid.NewGuid():N}@test.local";

        var page = await client.GetAsync("/Identity/Account/Register");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success, "Expected an antiforgery token on the registration form.");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenMatch.Groups[1].Value,
            ["Input.Email"] = email,
            ["Input.Password"] = "Task015!Buyer9a",
            ["Input.ConfirmPassword"] = "Task015!Buyer9a",
        });

        var response = await client.PostAsync("/Identity/Account/Register", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("RegisterConfirmation", response.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);

        try
        {
            Assert.True(await users.IsInRoleAsync(user, FaedRoles.Buyer));
            Assert.False(await users.IsInRoleAsync(user, FaedRoles.Admin));

            var principalFactory = scope.ServiceProvider
                .GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
            ClaimsPrincipal principal = await principalFactory.CreateAsync(user);
            var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var policyResult = await authorization.AuthorizeAsync(
                principal,
                resource: null,
                FaedPolicies.CanPlaceB2COrder);
            Assert.True(policyResult.Succeeded);

            var roles = await users.GetRolesAsync(user);
            var buyerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
            buyerClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id);
            buyerClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));

            Assert.Equal(HttpStatusCode.OK, (await buyerClient.GetAsync("/Buyer/Orders")).StatusCode);
        }
        finally
        {
            Assert.True((await users.DeleteAsync(user)).Succeeded);
        }
    }
}
