using Faed.Web.Data;
using Faed.Web.Services;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Admin;
using Faed.Web.Services.Analytics;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Marketplace;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Storage;
using Faed.Web.Services.Trust;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Faed.Web;

/// <summary>
/// Composition root helpers for the single-project MVC application (docs/06-ARCHITECTURE.md).
/// Business services, persistence and supporting infrastructure are all registered here;
/// there are no separate Domain/Application/Infrastructure projects.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers use-case services, EF Core persistence and supporting infrastructure.</summary>
    public static IServiceCollection AddFaedPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddPersistence();
        services.AddPrivateFileStorage(configuration, environment);

        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IClock, SystemClock>();

        // Merchant verification use cases (docs/10-IMPLEMENTATION-PLAN.md Phase 1).
        services.AddOptions<MerchantVerificationOptions>()
            .Bind(configuration.GetSection(MerchantVerificationOptions.SectionName));
        services.AddScoped<IMerchantVerificationService, MerchantVerificationService>();

        // Listings, variants, inventory and moderation use cases (docs/10-IMPLEMENTATION-PLAN.md
        // Phase 3, tasks/TASK-004-LISTINGS-AND-INVENTORY.md).
        services.AddOptions<ListingOptions>()
            .Bind(configuration.GetSection(ListingOptions.SectionName));
        services.AddScoped<IMerchantListingService, MerchantListingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IListingModerationService, ListingModerationService>();
        services.AddScoped<IListingMediaService, ListingMediaService>();

        // Anonymous-safe public marketplace browsing (docs/10-IMPLEMENTATION-PLAN.md Phase 4,
        // tasks/TASK-005-PUBLIC-MARKETPLACE.md).
        services.AddScoped<IPublicMarketplaceService, PublicMarketplaceService>();

        // B2C ordering: reservation, fulfilment and the reservation-expiry sweep
        // (docs/10-IMPLEMENTATION-PLAN.md Phase 5, tasks/TASK-006-B2C-ORDERS.md).
        services.AddOptions<OrderingOptions>()
            .Bind(configuration.GetSection(OrderingOptions.SectionName));
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IMerchantStoreService, MerchantStoreService>();

        // The background sweep is not hosted under the "Testing" environment: the web
        // integration tests drive expiry deterministically through IOrderService and a fake
        // clock, and a live timer racing them would make those assertions flaky
        // (docs/09-TEST-STRATEGY.md §1).
        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<ReservationExpiryService>();
        }

        // B2B negotiation: immutable offer/counter-offer history and the offer-expiry sweep
        // (docs/10-IMPLEMENTATION-PLAN.md Phase 6, tasks/TASK-007-B2B-NEGOTIATION.md).
        services.AddOptions<B2BNegotiationOptions>()
            .Bind(configuration.GetSection(B2BNegotiationOptions.SectionName));
        services.AddScoped<IB2BNegotiationService, B2BNegotiationService>();

        if (!environment.IsEnvironment("Testing"))
        {
            // Like the B2C reservation sweep, the offer-expiry timer is not hosted under the
            // test environment: the integration tests drive expiry deterministically through
            // the service and a fake clock (docs/09-TEST-STRATEGY.md §1).
            services.AddHostedService<B2BOfferExpiryService>();
        }

        // B2B accepted deal: atomic reservation on acceptance, the fulfilment state machine
        // and the deal-reservation-expiry sweep (docs/10-IMPLEMENTATION-PLAN.md Phase 7,
        // tasks/TASK-008-B2B-DEALS.md).
        services.AddOptions<B2BDealOptions>()
            .Bind(configuration.GetSection(B2BDealOptions.SectionName));
        services.AddScoped<IB2BDealService, B2BDealService>();

        if (!environment.IsEnvironment("Testing"))
        {
            // Same reasoning as the other two sweeps: the deal-expiry timer is not hosted
            // under the test environment (docs/09-TEST-STRATEGY.md §1).
            services.AddHostedService<B2BDealExpiryService>();
        }

        // Post-transaction trust: disputes + evidence, the admin dispute workflow, and
        // merchant reviews (docs/10-IMPLEMENTATION-PLAN.md Phase 8, tasks/TASK-009-TRUST.md).
        services.AddOptions<TrustOptions>()
            .Bind(configuration.GetSection(TrustOptions.SectionName));
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IReviewService, ReviewService>();

        // Merchant recovery analytics and the consolidated admin operational screens
        // (docs/10-IMPLEMENTATION-PLAN.md Phases 9–10, tasks/TASK-010-ANALYTICS-AND-ADMIN.md).
        // All read-only projections over authoritative data; catalog management is the only
        // write path and it is admin-gated and audited.
        services.AddOptions<AnalyticsOptions>()
            .Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AnalyticsOptions>, AnalyticsOptionsValidator>();
        services.AddScoped<IMerchantAnalyticsService, MerchantAnalyticsService>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services)
    {
        // One application DbContext; Identity shares it. Migrations live in this project
        // under Data/Migrations (docs/06-ARCHITECTURE.md §5).
        //
        // The connection string is resolved from the *built* IConfiguration when the context
        // options are created, never captured here at registration time. Reading it eagerly
        // silently ignored any configuration source added after AddFaedPlatform runs — which
        // is exactly how a test host overrides it — and pointed the integration test host at
        // the application database instead of its disposable one (docs/09-TEST-STRATEGY.md §2).
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseSqlServer(ResolveConnectionString(
                serviceProvider.GetRequiredService<IConfiguration>())));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
    }

    private static string ResolveConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    private static void AddPrivateFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<LocalFileStorageOptions>()
            .Bind(configuration.GetSection(LocalFileStorageOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.LocalRootPath))
                {
                    options.LocalRootPath = Path.Combine(environment.ContentRootPath, "App_Data", "private-storage");
                }

                var resolvedRoot = Path.GetFullPath(options.LocalRootPath);
                var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
                if (IsWithin(resolvedRoot, webRoot) || PathsEqual(resolvedRoot, webRoot))
                {
                    throw new InvalidOperationException(
                        $"FileStorage:LocalRootPath ('{resolvedRoot}') must not be inside the web root. " +
                        "Verification documents are private (docs/08-SECURITY-AND-PRIVACY.md §3).");
                }

                options.LocalRootPath = resolvedRoot;
            });

        if (environment.IsProduction())
        {
            // LocalFileStorage is a development-only convenience. Production must bind a
            // real private object store to IFileStorage (docs/06-ARCHITECTURE.md §8);
            // fail loudly the first time a verification document is stored or read rather
            // than silently writing to ephemeral local disk.
            services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException(
                "No production IFileStorage is configured. LocalFileStorage is development-only; " +
                "register a cloud object storage implementation for this environment."));
            return;
        }

        services.AddSingleton<IFileStorage, LocalFileStorage>();
    }

    private static bool IsWithin(string candidate, string directory)
    {
        var directoryWithSeparator = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

        return candidate.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
