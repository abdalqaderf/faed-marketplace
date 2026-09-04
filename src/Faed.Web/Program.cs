using Faed.Web;
using Faed.Web.Authorization;
using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Business services, EF Core persistence and supporting infrastructure (single project).
builder.Services.AddFaedPlatform(builder.Configuration, builder.Environment);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Keep the multipart upload ceiling in step with the largest configured per-file limit
// instead of hard-coding it at the controller. There are two
// independent upload paths with two independent caps — merchant verification documents and
// listing photos/evidence — so this must track whichever is larger, or raising just one of
// them in configuration would make its uploads fail at the framework layer with an opaque
// error before either validator ever runs.
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
        Math.Max(Math.Max(maxDocumentBytes, maxImageBytes), maxEvidenceBytes) + 1024 * 1024);

// Identity: Individual Accounts baseline, extended to ApplicationUser and Faed roles.
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Server-side authorization policies.
builder.Services.AddScoped<IAuthorizationHandler, ApprovedMerchantHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(FaedPolicies.AdminOnly, policy =>
        policy.RequireRole(FaedRoles.Admin));

    // Selling authorization: an approved merchant who is not an administrator. An
    // administrator account can never hold a selling merchant identity — moderation stays
    // independent of the merchants being moderated. The
    // service layer repeats this check.
    options.AddPolicy(FaedPolicies.ApprovedMerchant, policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => !context.User.IsInRole(FaedRoles.Admin))
            .AddRequirements(new ApprovedMerchantRequirement()));

    // B2B participation is merchant-only even when an approved merchant profile belongs to
    // an administrator. The service repeats this check.
    options.AddPolicy(FaedPolicies.CanNegotiateB2B, policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => !context.User.IsInRole(FaedRoles.Admin))
            .AddRequirements(new ApprovedMerchantRequirement()));

    // B2C ordering belongs to Buyer accounts and merchants acting as buyers. Administrators
    // remain excluded even if a misconfigured account also carries another role
    options.AddPolicy(FaedPolicies.CanPlaceB2COrder, policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
                !context.User.IsInRole(FaedRoles.Admin)
                && (context.User.IsInRole(FaedRoles.Buyer)
                    || context.User.IsInRole(FaedRoles.Merchant))));
});

builder.Services.AddControllersWithViews(options =>
{
    // Every state-changing MVC POST is antiforgery-protected.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// A guessed/stale public URL (an unknown listing/store slug, a bad shop filter route) gets a
// branded empty state instead of the framework's bare 404.
app.UseStatusCodePagesWithReExecute("/status/{0}");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Seed the fixed Identity roles and the catalog reference data (both idempotent), plus an
// optional development admin. Migrations must already be applied — the app does not migrate
// on startup.
await IdentityDataSeeder.SeedRolesAsync(app.Services);
await CatalogDataSeeder.SeedAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await IdentityDataSeeder.SeedDevelopmentAdminAsync(app.Services);

    // Deterministic demo/field-validation data set. Opt-in and password-gated; never runs
    // outside Development.
    await DemoDataSeeder.SeedAsync(app.Services, app.Environment);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
