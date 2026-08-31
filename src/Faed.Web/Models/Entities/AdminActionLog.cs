using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// Append-only record of a security-sensitive admin action (docs/04-DOMAIN-MODEL.md §10,
/// docs/08-SECURITY-AND-PRIVACY.md §13). Rows are never updated or deleted.
/// </summary>
public class AdminActionLog
{
    private AdminActionLog()
    {
    }

    public AdminActionLog(
        string adminUserId,
        AdminActionType actionType,
        string targetType,
        string targetId,
        string? notes,
        DateTime createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        AdminUserId = adminUserId;
        ActionType = actionType;
        TargetType = targetType;
        TargetId = targetId;
        Notes = notes;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string AdminUserId { get; private set; } = null!;

    public AdminActionType ActionType { get; private set; }

    public string TargetType { get; private set; } = null!;

    public string TargetId { get; private set; } = null!;

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
