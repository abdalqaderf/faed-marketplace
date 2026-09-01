using Faed.Web;
using Faed.Web.Authorization;
using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Business services, EF Core persistence and supporting infrastructure (single project).
builder.Services.AddFaedPlatform(builder.Configuration, builder.Environment);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Keep the multipart upload ceiling in step with the largest configured per-file limit
// (docs/06-ARCHITECTURE.md §11) instead of hard-coding it at the controller. There are two
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
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = Math.Max(maxDocumentBytes, maxImageBytes) + 1024 * 1024);

// Identity: Individual Accounts baseline, extended to ApplicationUser and Faed roles.
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Server-side authorization policies (docs/08-SECURITY-AND-PRIVACY.md §2).
builder.Services.AddScoped<IAuthorizationHandler, ApprovedMerchantHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(FaedPolicies.AdminOnly, policy =>
        policy.RequireRole(FaedRoles.Admin));

    options.AddPolicy(FaedPolicies.ApprovedMerchant, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new ApprovedMerchantRequirement()));
});

builder.Services.AddControllersWithViews(options =>
{
    // Every state-changing MVC POST is antiforgery-protected (docs/08-SECURITY-AND-PRIVACY.md §5).
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
// on startup (docs/20-DEVELOPMENT-WORKFLOW.md "Database workflow").
await IdentityDataSeeder.SeedRolesAsync(app.Services);
await CatalogDataSeeder.SeedAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await IdentityDataSeeder.SeedDevelopmentAdminAsync(app.Services);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
