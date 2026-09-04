using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Faed.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Overrides the default <c>Microsoft.AspNetCore.Identity.UI</c> page at the same route
/// (<c>/Identity/Account/AccessDenied</c>) purely to restyle it into the Faed shell, since
/// every reachable screen must visually fit Faed rather than the framework's default
/// scaffold. No authentication/authorization behaviour changes: the framework's cookie
/// authentication still redirects here whenever a signed-in user fails a policy check, and
/// this page still does nothing but display that fact — it carries no logic of its own.
/// </summary>
public sealed class AccessDeniedModel : PageModel
{
    public void OnGet()
    {
    }
}
