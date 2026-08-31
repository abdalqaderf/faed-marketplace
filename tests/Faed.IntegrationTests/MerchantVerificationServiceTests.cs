using System.Text;
using Faed.Application.Common;
using Faed.Application.Merchants;
using Faed.Domain.Enums;
using Faed.Domain.Identity;
using Faed.Infrastructure.Identity;
using Faed.Infrastructure.Persistence;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Merchant-verification use cases against real SQL Server (docs/09-TEST-STRATEGY.md §3
/// "Merchant verification", tasks/TASK-002 exit criteria).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class MerchantVerificationServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task SubmitForReview_WithoutDocuments_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var userId = await scope.CreateUserAsync();

        var save = await scope.Service.SaveDraftAsync(userId, new MerchantApplicationInput("Amman Threads", "a@b.co", null));
        Assert.True(save.Succeeded);

        var submit = await scope.Service.SubmitForReviewAsync(userId);

        Assert.True(submit.Failed);
        Assert.Equal(ResultErrorKind.Validation, submit.ErrorKind);
    }

    [SkippableFact]
    public async Task Approve_MovesToApproved_WritesAudit_AndGrantsMerchantRole()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var merchantUserId = await scope.CreateUserAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var profileId = await scope.SubmitApplicationAsync(merchantUserId);

        var result = await scope.Service.ApproveAsync(adminId, profileId);

        Assert.True(result.Succeeded);

        var profile = await scope.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        Assert.Equal(MerchantVerificationStatus.Approved, profile.VerificationStatus);
        Assert.Equal(adminId, profile.ReviewedByAdminId);

        var audited = await scope.Db.AdminActionLogs.AsNoTracking()
            .AnyAsync(l => l.TargetId == profileId.ToString() && l.ActionType == AdminActionType.MerchantApproved && l.AdminUserId == adminId);
        Assert.True(audited);

        Assert.True(await scope.Service.IsApprovedMerchantAsync(merchantUserId));

        var user = await scope.Users.FindByIdAsync(merchantUserId);
        Assert.True(await scope.Users.IsInRoleAsync(user!, FaedRoles.Merchant));
    }

    [SkippableFact]
    public async Task Decisions_WithoutAnAdminUserId_AreForbidden_AndChangeNothing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var merchantUserId = await scope.CreateUserAsync();
        var profileId = await scope.SubmitApplicationAsync(merchantUserId);

        var approve = await scope.Service.ApproveAsync("  ", profileId);
        var reject = await scope.Service.RejectAsync("", profileId, "no");

        Assert.Equal(ResultErrorKind.Forbidden, approve.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, reject.ErrorKind);

        var profile = await scope.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        Assert.Equal(MerchantVerificationStatus.PendingReview, profile.VerificationStatus);
        Assert.Equal(0, await scope.Db.AdminActionLogs.AsNoTracking().CountAsync(l => l.TargetId == profileId.ToString()));
    }

    [SkippableFact]
    public async Task Approve_WhenNotPending_ReturnsConflict()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var merchantUserId = await scope.CreateUserAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        await scope.Service.SaveDraftAsync(merchantUserId, new MerchantApplicationInput("Draft Co", null, null));
        var profile = await scope.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.UserId == merchantUserId);

        var result = await scope.Service.ApproveAsync(adminId, profile.Id);

        Assert.True(result.Failed);
        Assert.Equal(ResultErrorKind.Conflict, result.ErrorKind);
    }

    [SkippableFact]
    public async Task Reject_RequiresReason_AndRecordsIt()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var merchantUserId = await scope.CreateUserAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var profileId = await scope.SubmitApplicationAsync(merchantUserId);

        var missingReason = await scope.Service.RejectAsync(adminId, profileId, "  ");
        Assert.True(missingReason.Failed);
        Assert.Equal(ResultErrorKind.Validation, missingReason.ErrorKind);

        var overlong = await scope.Service.RejectAsync(adminId, profileId, new string('x', 1001));
        Assert.True(overlong.Failed);
        Assert.Equal(ResultErrorKind.Validation, overlong.ErrorKind);

        var rejected = await scope.Service.RejectAsync(adminId, profileId, "Commercial registration is expired.");
        Assert.True(rejected.Succeeded);

        var profile = await scope.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        Assert.Equal(MerchantVerificationStatus.Rejected, profile.VerificationStatus);
        Assert.Equal("Commercial registration is expired.", profile.RejectionReason);
        Assert.False(await scope.Service.IsApprovedMerchantAsync(merchantUserId));
    }

    [SkippableFact]
    public async Task ConcurrentAdminDecisions_SecondSaveIsRejectedAsConflict()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        await using var seed = new ServiceScopeWrapper(factory);
        var merchantUserId = await seed.CreateUserAsync();
        var adminA = await seed.CreateUserAsync(FaedRoles.Admin);
        var adminB = await seed.CreateUserAsync(FaedRoles.Admin);
        var profileId = await seed.SubmitApplicationAsync(merchantUserId);

        // Admin B's scope loads the pending application (stale rowversion pinned in its context).
        await using var scopeB = new ServiceScopeWrapper(factory);
        _ = await scopeB.Db.MerchantProfiles.SingleAsync(p => p.Id == profileId);

        // Admin A approves first in a separate scope.
        await using (var scopeA = new ServiceScopeWrapper(factory))
        {
            Assert.True((await scopeA.Service.ApproveAsync(adminA, profileId)).Succeeded);
        }

        // Admin B now tries to reject the same application on the stale row.
        var result = await scopeB.Service.RejectAsync(adminB, profileId, "Looks wrong");

        Assert.True(result.Failed);
        Assert.Equal(ResultErrorKind.Conflict, result.ErrorKind);

        var profile = await seed.Db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        Assert.Equal(MerchantVerificationStatus.Approved, profile.VerificationStatus);

        // Only Admin A's approval was audited.
        var rejections = await seed.Db.AdminActionLogs.AsNoTracking()
            .CountAsync(l => l.TargetId == profileId.ToString() && l.ActionType == AdminActionType.MerchantRejected);
        Assert.Equal(0, rejections);
    }

    [SkippableFact]
    public async Task AddDocument_RejectsRenamedNonImageContent()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var userId = await scope.CreateUserAsync();
        await scope.Service.SaveDraftAsync(userId, new MerchantApplicationInput("Polyglot Co", null, null));

        var result = await scope.Service.AddDocumentAsync(userId, new AddVerificationDocumentInput(
            MerchantVerificationDocumentType.CommercialRegistration,
            new MemoryStream(Encoding.UTF8.GetBytes("MZ this is actually an executable")),
            "registration.pdf",
            "application/pdf",
            32));

        Assert.True(result.Failed);
        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [SkippableFact]
    public async Task OpenVerificationDocument_ReturnsBytes_AndAuditsAccess()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var merchantUserId = await scope.CreateUserAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        await scope.Service.SaveDraftAsync(merchantUserId, new MerchantApplicationInput("Docs Co", null, null));
        var addDoc = await scope.Service.AddDocumentAsync(merchantUserId, new AddVerificationDocumentInput(
            MerchantVerificationDocumentType.CommercialRegistration,
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 fake")),
            "registration.pdf",
            "application/pdf",
            12));
        Assert.True(addDoc.Succeeded, addDoc.Error);

        var open = await scope.Service.OpenVerificationDocumentAsync(adminId, addDoc.Value);

        Assert.True(open.Succeeded);
        await using (var content = open.Value.Content)
        {
            using var reader = new StreamReader(content);
            Assert.Contains("PDF", await reader.ReadToEndAsync());
        }

        var audited = await scope.Db.AdminActionLogs.AsNoTracking().AnyAsync(l =>
            l.AdminUserId == adminId && l.ActionType == AdminActionType.MerchantVerificationDocumentAccessed);
        Assert.True(audited);
    }

    [SkippableFact]
    public async Task OpenVerificationDocument_UnknownId_ReturnsNotFound()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ServiceScopeWrapper(factory);
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var open = await scope.Service.OpenVerificationDocumentAsync(adminId, Guid.NewGuid());

        Assert.True(open.Failed);
        Assert.Equal(ResultErrorKind.NotFound, open.ErrorKind);
    }

    private sealed class ServiceScopeWrapper(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();

        public IMerchantVerificationService Service => _scope.ServiceProvider.GetRequiredService<IMerchantVerificationService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        public UserManager<ApplicationUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        public async Task<string> CreateUserAsync(string? role = null)
        {
            var user = new ApplicationUser
            {
                UserName = $"{Guid.NewGuid():N}@test.local",
                Email = $"{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
            };
            var created = await Users.CreateAsync(user);
            Assert.True(created.Succeeded);

            if (role is not null)
            {
                var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }

                await Users.AddToRoleAsync(user, role);
            }

            return user.Id;
        }

        public async Task<Guid> SubmitApplicationAsync(string userId)
        {
            await Service.SaveDraftAsync(userId, new MerchantApplicationInput("Test Merchant", "t@test.local", null));
            var add = await Service.AddDocumentAsync(userId, new AddVerificationDocumentInput(
                MerchantVerificationDocumentType.CommercialRegistration,
                new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 fake")),
                "reg.pdf",
                "application/pdf",
                12));
            Assert.True(add.Succeeded, add.Error);

            var submit = await Service.SubmitForReviewAsync(userId);
            Assert.True(submit.Succeeded);

            return await Db.MerchantProfiles.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await Task.CompletedTask;
        }
    }
}
