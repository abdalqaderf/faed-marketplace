using System.Security.Claims;
using Faed.Web.Services.Merchants;
using Microsoft.AspNetCore.Authorization;

namespace Faed.Web.Authorization;

/// <summary>
/// Requires an authenticated user with a merchant profile that has not been suspended. Unlike
/// <see cref="ApprovedMerchantRequirement"/> this admits Draft, PendingReview and Rejected
/// profiles too — it is what lets a merchant build listing drafts while their verification
/// application is still awaiting approval.
/// </summary>
public sealed class RegisteredMerchantRequirement : IAuthorizationRequirement;

public sealed class RegisteredMerchantHandler(IMerchantVerificationService verification)
    : AuthorizationHandler<RegisteredMerchantRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RegisteredMerchantRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        if (await verification.IsRegisteredMerchantAsync(userId))
        {
            context.Succeed(requirement);
        }
    }
}
