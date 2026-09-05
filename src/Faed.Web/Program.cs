using Faed.Web;
using Faed.Web.Authorization;
using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Email;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Allow static web assets while running the local Testing environment.
// This is used when testing production-only infrastructure such as R2
// without losing the application's CSS/images locally.
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.UseStaticWebAssets();
}

// Business services, EF Core persistence and supporting infrastructure.
builder.Services.AddFaedPlatform(
    builder.Configuration,
    builder.Environment);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Keep the multipart upload ceiling in step with the largest configured
// per-file limit instead of hard-coding it at the controller.
//
// There are independent upload paths for:
// - Merchant verification documents
// - Listing images
// - Dispute evidence
//
// The framework-level limit must therefore allow the largest configured
// file plus a small multipart overhead.
var maxDocumentBytes = builder.Configuration.GetValue<long?>(
    $"{MerchantVerificationOptions.SectionName}:{nameof(MerchantVerificationOptions.MaxDocumentBytes)}")
    ?? new MerchantVerificationOptions().MaxDocumentBytes;

var maxImageBytes = builder.Configuration.GetValue<long?>(
    $"{ListingOptions.SectionName}:{nameof(ListingOptions.MaxImageBytes)}")
    ?? new ListingOptions().MaxImageBytes;

var maxEvidenceBytes = builder.Configuration.GetValue<long?>(
    $"{TrustOptions.SectionName}:{nameof(TrustOptions.MaxEvidenceBytes)}")
    ?? new TrustOptions().MaxEvidenceBytes;

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit =
        Math.Max(
            Math.Max(maxDocumentBytes, maxImageBytes),
            maxEvidenceBytes)
        + 1024 * 1024);

// ASP.NET Core Identity.
// Normal Faed registrations become Buyer accounts.
// Merchant access is granted separately after merchant verification.
builder.Services
    .AddDefaultIdentity<ApplicationUser>(
        options =>
            options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Brevo transactional email.
//
// Uses Brevo's HTTPS API rather than SMTP so it can work on hosting
// environments that restrict outbound SMTP connections.
builder.Services.AddOptions<BrevoEmailOptions>()
    .Bind(
        builder.Configuration.GetSection(
            BrevoEmailOptions.SectionName));

builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>(
    client =>
    {
        client.BaseAddress =
            new Uri("https://api.brevo.com/");
    });

// Server-side authorization policies.
builder.Services.AddScoped<
    IAuthorizationHandler,
    ApprovedMerchantHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        FaedPolicies.AdminOnly,
        policy =>
            policy.RequireRole(FaedRoles.Admin));

    // Selling authorization:
    // approved merchants only, excluding administrators.
    options.AddPolicy(
        FaedPolicies.ApprovedMerchant,
        policy =>
            policy.RequireAuthenticatedUser()
                .RequireAssertion(
                    context =>
                        !context.User.IsInRole(FaedRoles.Admin))
                .AddRequirements(
                    new ApprovedMerchantRequirement()));

    // B2B participation is restricted to approved merchants.
    options.AddPolicy(
        FaedPolicies.CanNegotiateB2B,
        policy =>
            policy.RequireAuthenticatedUser()
                .RequireAssertion(
                    context =>
                        !context.User.IsInRole(FaedRoles.Admin))
                .AddRequirements(
                    new ApprovedMerchantRequirement()));

    // B2C ordering belongs to Buyer accounts and merchants
    // acting as buyers. Administrators are excluded.
    options.AddPolicy(
        FaedPolicies.CanPlaceB2COrder,
        policy =>
            policy.RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    !context.User.IsInRole(FaedRoles.Admin)
                    && (
                        context.User.IsInRole(FaedRoles.Buyer)
                        || context.User.IsInRole(
                            FaedRoles.Merchant))));
});

builder.Services.AddControllersWithViews(options =>
{
    // Every state-changing MVC POST is protected by antiforgery.
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");

    // Enable HTTP Strict Transport Security outside Development.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Unknown/stale public URLs receive Faed's branded status page
// instead of the framework's bare status response.
app.UseStatusCodePagesWithReExecute("/status/{0}");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "areas",
        pattern:
            "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern:
            "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

// Seed fixed Identity roles and reference catalog data.
//
// Migrations must already have been applied.
// Faed intentionally does not migrate the database automatically
// during application startup.
await IdentityDataSeeder.SeedRolesAsync(app.Services);
await CatalogDataSeeder.SeedAsync(app.Services);

// Optional one-time Production bootstrap administrator.
//
// This only creates/promotes an administrator when:
// Faed:BootstrapAdmin:Enabled = true
//
// After the Production administrator has been created,
// this setting should immediately be disabled and its password removed
// from the hosting environment.
await IdentityDataSeeder.SeedBootstrapAdminAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    // Development-only administrator.
    await IdentityDataSeeder.SeedDevelopmentAdminAsync(
        app.Services);

    // Optional deterministic demo data.
    // Demo seed never runs outside Development.
    await DemoDataSeeder.SeedAsync(
        app.Services,
        app.Environment);
}

app.Run();

/// <summary>
/// Exposed so WebApplicationFactory&lt;Program&gt;
/// can host the application in tests.
/// </summary>
public partial class Program;