using Faed.Application;
using Faed.Domain.Authorization;
using Faed.Domain.Identity;
using Faed.Infrastructure;
using Faed.Infrastructure.Identity;
using Faed.Infrastructure.Persistence;
using Faed.Application.Merchants;
using Faed.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Application use cases + persistence/supporting infrastructure.
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Keep the multipart upload ceiling in step with the configured document size limit
// (docs/06-ARCHITECTURE.md §11) instead of hard-coding it at the controller.
var maxDocumentBytes = builder.Configuration.GetValue<long?>(
    $"{MerchantVerificationOptions.SectionName}:{nameof(MerchantVerificationOptions.MaxDocumentBytes)}")
    ?? new MerchantVerificationOptions().MaxDocumentBytes;
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maxDocumentBytes + 1024 * 1024);

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

// Seed the fixed Identity roles (idempotent), plus an optional development admin.
await IdentityDataSeeder.SeedRolesAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await IdentityDataSeeder.SeedDevelopmentAdminAsync(app.Services);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
