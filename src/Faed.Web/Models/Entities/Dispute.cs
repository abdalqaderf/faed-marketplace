using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A post-transaction complaint raised by a participant against exactly one transaction
/// context — a B2C <see cref="Order"/> or a B2B <see cref="B2BDeal"/>, never both
/// (docs/03-BUSINESS-RULES.md §14, docs/04-DOMAIN-MODEL.md §9,
/// docs/17-DATA-INVARIANTS.md "Dispute"). A database check constraint enforces the
/// exactly-one rule; the raiser's participation is checked by the dispute service before this
/// aggregate is created (docs/08-SECURITY-AND-PRIVACY.md §9).
///
/// The dispute has its own lifecycle
/// (<see cref="DisputeStatus.Open"/> → <see cref="DisputeStatus.UnderReview"/> →
/// <see cref="DisputeStatus.Resolved"/> | <see cref="DisputeStatus.Rejected"/>,
/// docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10) and never touches the order/deal status or
/// its stock — resolution is an administrative record, not a fulfilment transition. An
/// <see cref="DisputeStatus.Open"/> dispute is never closed directly: an administrator must
/// first <see cref="StartReview"/> it, and every such move is written to the admin audit log
/// by the service (docs/17-DATA-INVARIANTS.md "Resolution actor must be Admin", "Dispute
/// resolution is auditable").
///
/// <see cref="ActiveTransactionKey"/> is a filtered-unique key: it holds a per-transaction
/// value while the dispute is active (<see cref="DisputeStatus.Open"/> /
/// <see cref="DisputeStatus.UnderReview"/>) and is cleared when the dispute closes. A unique
/// index on it lets the database — not just an application read — enforce
/// docs/03-BUSINESS-RULES.md §14: at most one active dispute per transaction, even when two
/// filings race (AGENTS.md §7).
/// </summary>
public class Dispute
{
    public const int MaxDescriptionLength = 4000;
    public const int MaxResolutionLength = 4000;
    public const int MaxActiveTransactionKeyLength = 48;

    private readonly List<DisputeEvidence> _evidence = [];

    private Dispute()
    {
    }

    /// <summary>
    /// Opens a dispute against one transaction. Pass exactly one of <paramref name="orderId"/>
    /// or <paramref name="b2bDealId"/>; the other must be <c>null</c>.
    /// </summary>
    public Dispute(
        Guid? orderId,
        Guid? b2bDealId,
        string raisedByUserId,
        DisputeReasonCode reasonCode,
        string description,
        DateTime nowUtc)
    {
        if ((orderId is null) == (b2bDealId is null))
        {
            throw new DomainException("A dispute must reference exactly one transaction — an order or a deal.");
        }

        if (string.IsNullOrWhiteSpace(raisedByUserId))
        {
            throw new DomainException("A dispute needs the user who raised it.");
        }

        if (!Enum.IsDefined(reasonCode))
        {
            throw new DomainException("Choose a valid reason for the dispute.");
        }

        Id = Guid.CreateVersion7();
        OrderId = orderId;
        B2BDealId = b2bDealId;
        RaisedByUserId = raisedByUserId;
        ReasonCode = reasonCode;
        Description = RequireText(description, "description", MaxDescriptionLength);
        Status = DisputeStatus.Open;
        ActiveTransactionKey = orderId is { } o
            ? ActiveKeyFor(TrustTransactionType.B2COrder, o)
            : ActiveKeyFor(TrustTransactionType.B2BDeal, b2bDealId!.Value);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// The value <see cref="ActiveTransactionKey"/> takes for a live dispute on a given
    /// transaction. Disjoint across the two transaction kinds.
    /// </summary>
    public static string ActiveKeyFor(TrustTransactionType type, Guid transactionId) =>
        type == TrustTransactionType.B2COrder ? $"O:{transactionId:N}" : $"D:{transactionId:N}";

    public Guid Id { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? B2BDealId { get; private set; }

    /// <summary>The Identity user id of the participant who raised the dispute.</summary>
    public string RaisedByUserId { get; private set; } = null!;

    public DisputeReasonCode ReasonCode { get; private set; }

    public string Description { get; private set; } = null!;

    public DisputeStatus Status { get; private set; }

    /// <summary>
    /// A per-transaction token while the dispute is active, <c>null</c> once it closes. Backed
    /// by a filtered unique index so the database rejects a second concurrent filing for the
    /// same transaction (docs/03-BUSINESS-RULES.md §14, AGENTS.md §7).
    /// </summary>
    public string? ActiveTransactionKey { get; private set; }

    /// <summary>The administrator's written outcome, set when the dispute is resolved or rejected.</summary>
    public string? AdminResolution { get; private set; }

    public string? ResolvedByAdminId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Guards two administrators acting on the same dispute at once.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<DisputeEvidence> Evidence => _evidence.AsReadOnly();

    public TrustTransactionType TransactionType =>
        OrderId is not null ? TrustTransactionType.B2COrder : TrustTransactionType.B2BDeal;

    public bool IsTerminal => Status is DisputeStatus.Resolved or DisputeStatus.Rejected;

    /// <summary>A participant may still attach evidence while an administrator has not closed the dispute.</summary>
    public bool AcceptsEvidence => !IsTerminal;

    // ---- Building ----------------------------------------------------------------

    /// <summary>
    /// Attaches an evidence file. Evidence is always private: it is streamed only to the
    /// dispute's participants and administrators, never from a public URL
    /// (docs/08-SECURITY-AND-PRIVACY.md §3-4).
    /// </summary>
    public DisputeEvidence AddEvidence(
        string uploadedByUserId,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTime nowUtc)
    {
        if (!AcceptsEvidence)
        {
            throw new DomainException($"A dispute that is {Status} can no longer take new evidence.");
        }

        var evidence = new DisputeEvidence(
            uploadedByUserId, storageObjectKey, originalFileName, contentType, sizeBytes, nowUtc);
        _evidence.Add(evidence);
        Touch(nowUtc);
        return evidence;
    }

    // ---- Lifecycle -------------------------------------------------------------

    /// <summary>An administrator picks the dispute up for review.</summary>
    public void StartReview(string adminUserId, DateTime nowUtc)
    {
        RequireStatus(DisputeStatus.Open, "moved to review");
        RequireAdmin(adminUserId);
        Status = DisputeStatus.UnderReview;
        ResolvedByAdminId = adminUserId;
        Touch(nowUtc);
    }

    /// <summary>
    /// An administrator upholds the dispute and records the outcome. Only a dispute already
    /// <see cref="DisputeStatus.UnderReview"/> can be closed — an <see cref="DisputeStatus.Open"/>
    /// dispute must be picked up with <see cref="StartReview"/> first
    /// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10).
    /// </summary>
    public void Resolve(string adminUserId, string resolution, DateTime nowUtc)
        => Close(DisputeStatus.Resolved, adminUserId, resolution, nowUtc);

    /// <summary>
    /// An administrator dismisses the dispute and records why. Like <see cref="Resolve"/>, only
    /// valid once the dispute is <see cref="DisputeStatus.UnderReview"/>.
    /// </summary>
    public void Reject(string adminUserId, string resolution, DateTime nowUtc)
        => Close(DisputeStatus.Rejected, adminUserId, resolution, nowUtc);

    private void Close(DisputeStatus outcome, string adminUserId, string resolution, DateTime nowUtc)
    {
        if (Status != DisputeStatus.UnderReview)
        {
            throw new DomainException(Status is DisputeStatus.Resolved or DisputeStatus.Rejected
                ? $"A dispute that is {Status} has already been closed."
                : "Start reviewing the dispute before recording an outcome.");
        }

        RequireAdmin(adminUserId);
        Status = outcome;
        AdminResolution = RequireText(resolution, "resolution", MaxResolutionLength);
        ResolvedByAdminId = adminUserId;
        ResolvedAtUtc = nowUtc;
        // The dispute is closed: it no longer counts against the one-active-dispute-per-
        // transaction rule, so release the filtered-unique key.
        ActiveTransactionKey = null;
        Touch(nowUtc);
    }

    private void RequireStatus(DisputeStatus expected, string verb)
    {
        if (Status != expected)
        {
            throw new DomainException($"A dispute that is {Status} cannot be {verb}.");
        }
    }

    private static void RequireAdmin(string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new DomainException("An administrator id is required to act on a dispute.");
        }
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    private static string RequireText(string value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException($"The dispute {field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The dispute {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
