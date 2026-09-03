using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;

namespace Faed.Web.Services.Trust;

/// <summary>
/// Post-transaction disputes (docs/03-BUSINESS-RULES.md §14,
/// docs/10-IMPLEMENTATION-PLAN.md Phase 8). Only a participant in the referenced order or
/// deal can open one; only an administrator can move it past <c>Open</c>, and every such
/// move is audited. Evidence files are private — streamed only to participants and admins.
/// </summary>
public interface IDisputeService
{
    // ---- Participant ----------------------------------------------------------

    Task<Result<Guid>> FileDisputeAsync(
        string userId, FileDisputeInput input, CancellationToken cancellationToken = default);

    Task<Result> AddEvidenceAsync(
        string userId, Guid disputeId, IReadOnlyList<DisputeEvidenceUpload> files, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DisputeSummaryView>> GetMyDisputesAsync(
        string userId, CancellationToken cancellationToken = default);

    Task<DisputeDetailView?> GetMyDisputeAsync(
        string userId, Guid disputeId, CancellationToken cancellationToken = default);

    /// <summary>Streams an evidence file to a dispute participant or an administrator. Never public.</summary>
    Task<Result<StoredFileContent>> OpenEvidenceAsync(
        string userId, Guid evidenceId, CancellationToken cancellationToken = default);

    // ---- Administrator ------------------------------------------------------

    Task<IReadOnlyList<DisputeSummaryView>> GetQueueAsync(
        DisputeQueueFilter filter, CancellationToken cancellationToken = default);

    Task<int> GetOpenDisputeCountAsync(CancellationToken cancellationToken = default);

    Task<AdminDisputeDetailView?> GetForReviewAsync(
        Guid disputeId, CancellationToken cancellationToken = default);

    Task<Result> StartReviewAsync(
        string adminUserId, Guid disputeId, CancellationToken cancellationToken = default);

    Task<Result> ResolveAsync(
        string adminUserId, Guid disputeId, string resolution, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(
        string adminUserId, Guid disputeId, string resolution, CancellationToken cancellationToken = default);
}
