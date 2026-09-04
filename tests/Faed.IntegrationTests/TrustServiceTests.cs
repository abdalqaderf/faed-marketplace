using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Faed.IntegrationTests;

/// <summary>
/// Post-transaction trust: disputes, the admin dispute workflow, evidence privacy and merchant
/// reviews against real SQL Server (tasks/TASK-009-TRUST.md "Exit criteria";
/// docs/09-TEST-STRATEGY.md §3 "Review" / "Dispute").
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class TrustServiceTests(FaedWebApplicationFactory factory)
{
    // ---- Disputes: participation ----------------------------------------------

    [SkippableFact]
    public async Task FileDispute_ByTheBuyer_Succeeds_ButByANonParticipant_RevealsNothing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var strangerUserId = await scope.CreateUserAsync();

        var stranger = await scope.Disputes.FileDisputeAsync(strangerUserId, OrderDispute(orderId));
        Assert.Equal(ResultErrorKind.NotFound, stranger.ErrorKind);
        Assert.Empty(await scope.Db.Disputes.AsNoTracking().Where(d => d.OrderId == orderId).ToListAsync());

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(filed.Succeeded, filed.Error);

        var dispute = await scope.Db.Disputes.AsNoTracking().SingleAsync(d => d.Id == filed.Value);
        Assert.Equal(DisputeStatus.Open, dispute.Status);
        Assert.Equal(orderId, dispute.OrderId);
        Assert.Null(dispute.B2BDealId);
        Assert.Equal(buyerUserId, dispute.RaisedByUserId);
        Assert.Equal(Dispute.ActiveKeyFor(TrustTransactionType.B2COrder, orderId), dispute.ActiveTransactionKey);
    }

    [SkippableFact]
    public async Task FileDispute_ByTheSellingMerchant_Succeeds()
    {
        // Finding 6: an eligible selling merchant is a participant in a B2C order and can file
        // a dispute — the same server-side checks apply (docs/16-PERMISSIONS-MATRIX.md).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        var filed = await scope.Disputes.FileDisputeAsync(merchantUserId, OrderDispute(orderId));

        Assert.True(filed.Succeeded, filed.Error);
        var dispute = await scope.Db.Disputes.AsNoTracking().SingleAsync(d => d.Id == filed.Value);
        Assert.Equal(merchantUserId, dispute.RaisedByUserId);
    }

    [SkippableFact]
    public async Task FileDispute_ByAnAdministrator_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var result = await scope.Disputes.FileDisputeAsync(adminUserId, OrderDispute(orderId));

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
    }

    [SkippableFact]
    public async Task FileDispute_OnAPendingOrder_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.PlacePendingOrderAsync(merchantUserId);

        var result = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
        Assert.Empty(await scope.Db.Disputes.AsNoTracking().Where(d => d.OrderId == orderId).ToListAsync());
    }

    [SkippableFact]
    public async Task OnlyOneActiveDispute_PerTransaction()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        Assert.True((await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId))).Succeeded);
        var second = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));

        Assert.Equal(ResultErrorKind.Conflict, second.ErrorKind);
    }

    [SkippableFact]
    public async Task TwoConcurrentFilings_ForTheSameOrder_OnlyOneSucceeds()
    {
        // Finding 1: the one-active-dispute-per-transaction invariant is concurrency-safe. Two
        // filings that both pass the application pre-check are serialized by the filtered
        // unique index on Dispute.ActiveTransactionKey; the loser gets a clean conflict
        // (docs/03-BUSINESS-RULES.md §14, AGENTS.md §7).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        await using var otherScope = new TrustScope(factory);
        Result<Guid> second = null!;
        var gated = scope.NewGatedDisputeService(async ct =>
            second = await otherScope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId), ct));

        // The gated call reads the same "no active dispute" state, then pauses immediately
        // before its INSERT while the competing call runs to completion and commits.
        var first = await gated.FileDisputeAsync(buyerUserId, OrderDispute(orderId));

        Assert.True(second.Succeeded, second.Error);
        Assert.True(first.Failed);
        Assert.Equal(ResultErrorKind.Conflict, first.ErrorKind);
        Assert.Equal(1, await scope.Db.Disputes.AsNoTracking().CountAsync(d => d.OrderId == orderId));
    }

    [SkippableFact]
    public async Task AfterADisputeIsClosed_AFreshDisputeMayBeFiledForTheSameOrder()
    {
        // Finding 5 (root cause): a closed dispute releases the active key, so the authoritative
        // rules allow another filing — it must not be blocked by history.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var first = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(first.Succeeded, first.Error);
        Assert.True((await scope.Disputes.StartReviewAsync(adminUserId, first.Value)).Succeeded);
        Assert.True((await scope.Disputes.RejectAsync(adminUserId, first.Value, "No evidence of a defect.")).Succeeded);

        var closed = await scope.Db.Disputes.AsNoTracking().SingleAsync(d => d.Id == first.Value);
        Assert.Null(closed.ActiveTransactionKey);

        var second = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(2, await scope.Db.Disputes.AsNoTracking().CountAsync(d => d.OrderId == orderId));
    }

    // ---- Disputes: state machine --------------------------------------------

    [SkippableFact]
    public async Task AnOpenDispute_CannotBeResolvedOrRejectedDirectly()
    {
        // Finding 2: Open -> UnderReview -> Resolved|Rejected. An Open dispute is never closed
        // in one step (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(filed.Succeeded, filed.Error);

        var resolveOpen = await scope.Disputes.ResolveAsync(adminUserId, filed.Value, "Upheld.");
        Assert.Equal(ResultErrorKind.Conflict, resolveOpen.ErrorKind);
        var rejectOpen = await scope.Disputes.RejectAsync(adminUserId, filed.Value, "Dismissed.");
        Assert.Equal(ResultErrorKind.Conflict, rejectOpen.ErrorKind);

        var stillOpen = await scope.Db.Disputes.AsNoTracking().SingleAsync(d => d.Id == filed.Value);
        Assert.Equal(DisputeStatus.Open, stillOpen.Status);
        Assert.Null(stillOpen.AdminResolution);
        Assert.False(await scope.Db.AdminActionLogs.AsNoTracking().AnyAsync(l =>
            l.TargetId == filed.Value.ToString()
            && (l.ActionType == AdminActionType.DisputeResolved || l.ActionType == AdminActionType.DisputeRejected)));

        // The documented path works.
        Assert.True((await scope.Disputes.StartReviewAsync(adminUserId, filed.Value)).Succeeded);
        Assert.True((await scope.Disputes.ResolveAsync(adminUserId, filed.Value, "Upheld after review.")).Succeeded);
        Assert.Equal(DisputeStatus.Resolved,
            await scope.Db.Disputes.AsNoTracking().Where(d => d.Id == filed.Value).Select(d => d.Status).SingleAsync());
    }

    // ---- Disputes: admin workflow + audit --------------------------------------

    [SkippableFact]
    public async Task AdminResolution_MovesTheDisputeToResolved_AndIsWrittenToTheAuditLog()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(filed.Succeeded, filed.Error);

        Assert.True((await scope.Disputes.StartReviewAsync(adminUserId, filed.Value)).Succeeded);
        Assert.True((await scope.Disputes.ResolveAsync(adminUserId, filed.Value, "Merchant refunded the buyer.")).Succeeded);

        var dispute = await scope.Db.Disputes.AsNoTracking().SingleAsync(d => d.Id == filed.Value);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        Assert.Equal("Merchant refunded the buyer.", dispute.AdminResolution);
        Assert.Equal(adminUserId, dispute.ResolvedByAdminId);
        Assert.NotNull(dispute.ResolvedAtUtc);

        var audit = await scope.Db.AdminActionLogs.AsNoTracking()
            .Where(l => l.TargetType == nameof(Dispute) && l.TargetId == filed.Value.ToString())
            .Select(l => l.ActionType)
            .ToListAsync();
        Assert.Contains(AdminActionType.DisputeReviewStarted, audit);
        Assert.Contains(AdminActionType.DisputeResolved, audit);
    }

    [SkippableFact]
    public async Task AResolutionAtTheDocumentedMaxLength_PersistsWithItsCompleteAuditEntry()
    {
        // Finding 3: no silent truncation. A full-length resolution (Dispute.MaxResolutionLength)
        // is stored on the dispute AND recorded complete on the AdminActionLog, in one
        // transaction (docs/17-DATA-INVARIANTS.md "Dispute resolution is auditable").
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True((await scope.Disputes.StartReviewAsync(adminUserId, filed.Value)).Succeeded);

        var longOutcome = new string('x', Dispute.MaxResolutionLength);
        Assert.True((await scope.Disputes.ResolveAsync(adminUserId, filed.Value, longOutcome)).Succeeded);

        var storedResolution = await scope.Db.Disputes.AsNoTracking()
            .Where(d => d.Id == filed.Value).Select(d => d.AdminResolution).SingleAsync();
        Assert.Equal(longOutcome, storedResolution);

        var auditNotes = await scope.Db.AdminActionLogs.AsNoTracking()
            .Where(l => l.TargetId == filed.Value.ToString() && l.ActionType == AdminActionType.DisputeResolved)
            .Select(l => l.Notes)
            .SingleAsync();
        Assert.Equal(longOutcome, auditNotes);
    }

    [SkippableFact]
    public async Task DisputeDecisions_ByANonAdministrator_AreForbidden_EvenAtTheServiceLayer()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, OrderDispute(orderId));
        Assert.True(filed.Succeeded, filed.Error);

        Assert.Equal(ResultErrorKind.Forbidden,
            (await scope.Disputes.StartReviewAsync(buyerUserId, filed.Value)).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden,
            (await scope.Disputes.ResolveAsync(merchantUserId, filed.Value, "not my call")).ErrorKind);

        var status = await scope.Db.Disputes.AsNoTracking().Where(d => d.Id == filed.Value).Select(d => d.Status).SingleAsync();
        Assert.Equal(DisputeStatus.Open, status);
    }

    // ---- Disputes: evidence privacy ------------------------------------------

    [SkippableFact]
    public async Task DisputeEvidence_IsPrivate_ToParticipantsAndAdministrators()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        var strangerUserId = await scope.CreateUserAsync();
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        var filed = await scope.Disputes.FileDisputeAsync(buyerUserId, new FileDisputeInput(
            TrustTransactionType.B2COrder, orderId, DisputeReasonCode.UndisclosedDefect,
            "Torn lining.",
            [new DisputeEvidenceUpload(TestImages.MinimalPngStream(), "proof.png", "image/png", TestImages.MinimalPng.Length)]));
        Assert.True(filed.Succeeded, filed.Error);

        var evidenceId = await scope.Db.DisputeEvidence.AsNoTracking()
            .Where(e => e.DisputeId == filed.Value).Select(e => e.Id).SingleAsync();

        // Finding 4: a non-participant gets the SAME result a non-existent id gets — "not
        // found" — so guessing ids never confirms which evidence exists.
        var strangerHit = await scope.Disputes.OpenEvidenceAsync(strangerUserId, evidenceId);
        var strangerMiss = await scope.Disputes.OpenEvidenceAsync(strangerUserId, Guid.NewGuid());
        Assert.Equal(ResultErrorKind.NotFound, strangerHit.ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, strangerMiss.ErrorKind);

        var asBuyer = await scope.Disputes.OpenEvidenceAsync(buyerUserId, evidenceId);
        Assert.True(asBuyer.Succeeded, asBuyer.Error);
        var asMerchant = await scope.Disputes.OpenEvidenceAsync(merchantUserId, evidenceId);
        Assert.True(asMerchant.Succeeded, asMerchant.Error);
        var asAdmin = await scope.Disputes.OpenEvidenceAsync(adminUserId, evidenceId);
        Assert.True(asAdmin.Succeeded, asAdmin.Error);

        // The administrator's access is audited (docs/08-SECURITY-AND-PRIVACY.md §13).
        Assert.True(await scope.Db.AdminActionLogs.AsNoTracking().AnyAsync(l =>
            l.ActionType == AdminActionType.DisputeEvidenceAccessed && l.TargetId == evidenceId.ToString()));
    }

    // ---- Reviews ------------------------------------------------------------

    [SkippableFact]
    public async Task Review_RequiresACompletedTransaction()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);

        var tooEarly = await scope.Reviews.SubmitReviewAsync(buyerUserId, ReviewFor(orderId, 5));
        Assert.Equal(ResultErrorKind.Validation, tooEarly.ErrorKind);

        await scope.CompleteOrderAsync(merchantUserId, orderId);

        var afterCompletion = await scope.Reviews.SubmitReviewAsync(buyerUserId, ReviewFor(orderId, 5));
        Assert.True(afterCompletion.Succeeded, afterCompletion.Error);
    }

    [SkippableFact]
    public async Task Review_ByANonParticipant_RevealsNothing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        await scope.CompleteOrderAsync(merchantUserId, orderId);
        var strangerUserId = await scope.CreateUserAsync();

        var result = await scope.Reviews.SubmitReviewAsync(strangerUserId, ReviewFor(orderId, 5));

        Assert.Equal(ResultErrorKind.NotFound, result.ErrorKind);
        Assert.Empty(await scope.Db.Reviews.AsNoTracking().Where(r => r.OrderId == orderId).ToListAsync());
    }

    [SkippableFact]
    public async Task DuplicateReview_IsBlocked()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, orderId) = await scope.CreateConfirmedOrderAsync(merchantUserId);
        await scope.CompleteOrderAsync(merchantUserId, orderId);

        Assert.True((await scope.Reviews.SubmitReviewAsync(buyerUserId, ReviewFor(orderId, 5))).Succeeded);
        var second = await scope.Reviews.SubmitReviewAsync(buyerUserId, ReviewFor(orderId, 1));

        Assert.Equal(ResultErrorKind.Conflict, second.ErrorKind);
        Assert.Equal(1, await scope.Db.Reviews.AsNoTracking().CountAsync(r => r.OrderId == orderId));
    }

    [SkippableFact]
    public async Task B2BDealReview_IsByTheBuyingMerchant_AfterCompletion_NotTheSeller()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, sellerProfileId) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var dealId = await scope.CreateCompletedDealAsync(sellerUserId, buyerUserId);

        var bySeller = await scope.Reviews.SubmitReviewAsync(sellerUserId, DealReviewFor(dealId, 5));
        Assert.Equal(ResultErrorKind.NotFound, bySeller.ErrorKind);

        var byBuyer = await scope.Reviews.SubmitReviewAsync(buyerUserId, DealReviewFor(dealId, 4));
        Assert.True(byBuyer.Succeeded, byBuyer.Error);

        var review = await scope.Db.Reviews.AsNoTracking().SingleAsync(r => r.B2BDealId == dealId);
        Assert.Equal(sellerProfileId, review.ReviewedMerchantProfileId);
        Assert.Equal(4, review.Rating);

        var summary = await scope.Reviews.GetMerchantReviewsAsync(sellerProfileId, 10);
        Assert.Equal(1, summary.Summary.ReviewCount);
        Assert.Equal(4, summary.Summary.AverageRating);
    }

    [SkippableFact]
    public async Task MerchantReviewHistory_BeyondOnePage_IsDatabasePagedAndFullyReachable()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var reviewCount = Paging.DefaultPageSize + 1;
        await scope.CreateCompletedReviewHistoryAsync(merchantUserId, reviewCount);

        var first = await scope.Reviews.GetReviewsForOwnerAsync(merchantUserId, page: 1);
        var second = await scope.Reviews.GetReviewsForOwnerAsync(merchantUserId, page: 2);

        Assert.Equal(reviewCount, first.Summary.ReviewCount);
        Assert.Equal(reviewCount, first.Reviews.TotalCount);
        Assert.Equal(2, first.Reviews.TotalPages);
        Assert.Equal(Paging.DefaultPageSize, first.Reviews.Items.Count);
        Assert.Single(second.Reviews.Items);
        Assert.Empty(first.Reviews.Items.Select(review => review.Comment)
            .Intersect(second.Reviews.Items.Select(review => review.Comment)));

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, merchantUserId);

        var html = await client.GetStringAsync("/Merchant/Reviews?page=2");
        Assert.Contains("Page 2 of 2", html);
        Assert.Contains("Showing 26", html);
    }

    // ---- Helpers ----------------------------------------------------------

    private static FileDisputeInput OrderDispute(Guid orderId) => new(
        TrustTransactionType.B2COrder, orderId, DisputeReasonCode.ItemNotAsDescribed,
        "The item did not match the listing.", []);

    private static SubmitReviewInput ReviewFor(Guid orderId, int rating) =>
        new(TrustTransactionType.B2COrder, orderId, rating, "Fine.");

    private static SubmitReviewInput DealReviewFor(Guid dealId, int rating) =>
        new(TrustTransactionType.B2BDeal, dealId, rating, "Smooth.");
}
