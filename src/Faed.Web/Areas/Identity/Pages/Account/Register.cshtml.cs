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
/// The default Identity registration flow with one Faed-specific invariant:
/// every normal account is atomically classified as a Buyer.
/// Merchant remains an additive role granted after verification approval.
/// </summary>
public sealed class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    ILogger<RegisterModel> logger,
    IEmailSender emailSender) : PageModel
{
    private readonly IUserEmailStore<ApplicationUser> _emailStore =
        GetEmailStore(userManager, userStore);

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public sealed class InputModel
    {
        [Required]
        [StringLength(
            ApplicationUser.MaxNameLength,
            ErrorMessage = "First name cannot exceed {1} characters.")]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(
            ApplicationUser.MaxNameLength,
            ErrorMessage = "Last name cannot exceed {1} characters.")]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(32)]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(
            100,
            ErrorMessage =
                "The {0} must be at least {2} and at most {1} characters long.",
            MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(
            nameof(Password),
            ErrorMessage =
                "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        ExternalLogins =
        [
            .. await signInManager.GetExternalAuthenticationSchemesAsync()
        ];
    }

    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        ExternalLogins =
        [
            .. await signInManager.GetExternalAuthenticationSchemesAsync()
        ];

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var cancellationToken = HttpContext.RequestAborted;

        var email = Input.Email.Trim();

        var user = CreateUser();

        await userStore.SetUserNameAsync(
            user,
            email,
            cancellationToken);

        await _emailStore.SetEmailAsync(
            user,
            email,
            cancellationToken);

        await using (
            var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var createResult =
                await userManager.CreateAsync(user, Input.Password);

            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                AddErrors(createResult);

                return Page();
            }

            var roleResult =
                await userManager.AddToRoleAsync(
                    user,
                    FaedRoles.Buyer);

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                logger.LogError(
                    "Registration rolled back because Buyer role assignment failed for {Email}: {Errors}",
                    email,
                    string.Join(
                        ", ",
                        roleResult.Errors.Select(error => error.Code)));

                ModelState.AddModelError(
                    string.Empty,
                    "We could not create your account. Please try again.");

                return Page();
            }

            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation(
            "User created a new Buyer account with password.");

        var userId =
            await userManager.GetUserIdAsync(user);

        var code =
            await userManager.GenerateEmailConfirmationTokenAsync(user);

        code =
            WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(code));

        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new
            {
                area = "Identity",
                userId,
                code,
                returnUrl
            },
            protocol: Request.Scheme)!;

        await emailSender.SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

        if (userManager.Options.SignIn.RequireConfirmedAccount)
        {
            return RedirectToPage(
                "RegisterConfirmation",
                new
                {
                    email,
                    returnUrl
                });
        }

        await signInManager.SignInAsync(
            user,
            isPersistent: false);

        return LocalRedirect(returnUrl);
    }

    private ApplicationUser CreateUser()
    {
        try
        {
            return new ApplicationUser
            {
                FirstName = Input.FirstName.Trim(),
                LastName = Input.LastName.Trim(),
                PhoneNumber = Input.PhoneNumber.Trim()
            };
        }
        catch
        {
            throw new InvalidOperationException(
                $"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                "Ensure it has a parameterless constructor.");
        }
    }

    private static IUserEmailStore<ApplicationUser> GetEmailStore(
        UserManager<ApplicationUser> manager,
        IUserStore<ApplicationUser> store)
    {
        if (!manager.SupportsUserEmail)
        {
            throw new NotSupportedException(
                "The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<ApplicationUser>)store;
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error.Description);
        }
    }
}