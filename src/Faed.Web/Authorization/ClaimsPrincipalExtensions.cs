using System.Security.Claims;

namespace Faed.Web.Authorization;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Identity user id for the signed-in user, or <c>null</c> when anonymous.</summary>
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string RequireUserId(this ClaimsPrincipal principal) =>
        principal.GetUserId()
        ?? throw new InvalidOperationException("An authenticated user was expected.");
}
