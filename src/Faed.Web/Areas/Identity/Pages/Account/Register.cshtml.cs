using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Faed.Web.Data;
using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Faed.Web.Areas.Identity.Pages.Account;

/// <summary>
/// The default Identity registration flow with one Faed-specific invariant: every normal
/// account is atomically classified as a Buyer. Merchant remains an additive role granted
/// after verification approval (docs/04-DOMAIN-MODEL.md §1, docs/16-PERMISSIONS-MATRIX.md).
/// </summary>
public sealed class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    ILogger<RegisterModel> logger,
    IEmailSender emailSender) : PageModel
{
    private readonly IUserEmailStore<ApplicationUser> _emailStore = GetEmailStore(userManager, userStore);

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = default!;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = default!;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = [.. await signInManager.GetExternalAuthenticationSchemesAsync()];
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;
        ExternalLogins = [.. await signInManager.GetExternalAuthenticationSchemesAsync()];

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var cancellationToken = HttpContext.RequestAborted;
        var user = CreateUser();
        await userStore.SetUserNameAsync(user, Input.Email, cancellationToken);
        await _emailStore.SetEmailAsync(user, Input.Email, cancellationToken);

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var createResult = await userManager.CreateAsync(user, Input.Password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                AddErrors(createResult);
                return Page();
            }

            var roleResult = await userManager.AddToRoleAsync(user, FaedRoles.Buyer);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(
                    "Registration rolled back because Buyer role assignment failed for {Email}: {Errors}",
                    Input.Email,
                    string.Join(", ", roleResult.Errors.Select(error => error.Code)));
                ModelState.AddModelError(string.Empty, "We could not create your account. Please try again.");
                return Page();
            }

            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("User created a new Buyer account with password.");

        var userId = await userManager.GetUserIdAsync(user);
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId, code, returnUrl },
            protocol: Request.Scheme)!;

        await emailSender.SendEmailAsync(
            Input.Email,
            "Confirm your email",
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

        if (userManager.Options.SignIn.RequireConfirmedAccount)
        {
            return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl);
    }

    private static ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException(
                $"Can't create an instance of '{nameof(ApplicationUser)}'. Ensure it has a parameterless constructor.");
        }
    }

    private static IUserEmailStore<ApplicationUser> GetEmailStore(
        UserManager<ApplicationUser> manager,
        IUserStore<ApplicationUser> store)
    {
        if (!manager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<ApplicationUser>)store;
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
