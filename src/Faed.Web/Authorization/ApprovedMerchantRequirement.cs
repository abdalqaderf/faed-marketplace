using System.Security.Claims;
using Faed.Application.Merchants;
using Microsoft.AspNetCore.Authorization;

namespace Faed.Web.Authorization;

/// <summary>
/// Requires an authenticated user whose merchant profile is Approved. Verification is a
/// domain state, so this is checked against the database on every request rather than from
/// a role or claim alone (AGENTS.md §3, docs/08-SECURITY-AND-PRIVACY.md §1-2).
/// </summary>
public sealed class ApprovedMerchantRequirement : IAuthorizationRequirement;

public sealed class ApprovedMerchantHandler(IMerchantVerificationService verification)
    : AuthorizationHandler<ApprovedMerchantRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovedMerchantRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        if (await verification.IsApprovedMerchantAsync(userId))
        {
            context.Succeed(requirement);
        }
    }
}
