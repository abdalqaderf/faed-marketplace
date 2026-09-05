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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Faed.Web;

/// <summary>
/// Composition root helpers for the single-project MVC application.
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
        services.AddPersistence(environment);
        services.AddPrivateFileStorage(configuration, environment);

        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IClock, SystemClock>();

        // Merchant verification use cases.
        services.AddOptions<MerchantVerificationOptions>()
            .Bind(configuration.GetSection(MerchantVerificationOptions.SectionName));
        services.AddScoped<IMerchantVerificationService, MerchantVerificationService>();

        // Listings, variants, inventory and moderation use cases.
        services.AddOptions<ListingOptions>()
            .Bind(configuration.GetSection(ListingOptions.SectionName));
        services.AddScoped<IMerchantListingService, MerchantListingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IListingModerationService, ListingModerationService>();
        services.AddScoped<IListingMediaService, ListingMediaService>();

        // Anonymous-safe public marketplace browsing.
        services.AddScoped<IPublicMarketplaceService, PublicMarketplaceService>();

        // B2C ordering: reservation, fulfilment and the reservation-expiry sweep
        services.AddOptions<OrderingOptions>()
            .Bind(configuration.GetSection(OrderingOptions.SectionName));
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IMerchantStoreService, MerchantStoreService>();

        // The background sweep is not hosted under the "Testing" environment: the web
        // integration tests drive expiry deterministically through IOrderService and a fake
        // clock, and a live timer racing them would make those assertions flaky
        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<ReservationExpiryService>();
        }

        // B2B negotiation: immutable offer/counter-offer history and the offer-expiry sweep
        services.AddOptions<B2BNegotiationOptions>()
            .Bind(configuration.GetSection(B2BNegotiationOptions.SectionName));
        services.AddScoped<IB2BNegotiationService, B2BNegotiationService>();

        if (!environment.IsEnvironment("Testing"))
        {
            // Like the B2C reservation sweep, the offer-expiry timer is not hosted under the
            // test environment: the integration tests drive expiry deterministically through
            // the service and a fake clock.
            services.AddHostedService<B2BOfferExpiryService>();
        }

        // B2B accepted deal: atomic reservation on acceptance, the fulfilment state machine
        // and the deal-reservation-expiry sweep.
        services.AddOptions<B2BDealOptions>()
            .Bind(configuration.GetSection(B2BDealOptions.SectionName));
        services.AddScoped<IB2BDealService, B2BDealService>();

        if (!environment.IsEnvironment("Testing"))
        {
            // Same reasoning as the other two sweeps: the deal-expiry timer is not hosted
            // under the test environment.
            services.AddHostedService<B2BDealExpiryService>();
        }

        // Post-transaction trust: disputes + evidence, the admin dispute workflow, and
        // merchant reviews.
        services.AddOptions<TrustOptions>()
            .Bind(configuration.GetSection(TrustOptions.SectionName));
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IReviewService, ReviewService>();

        // Merchant recovery analytics and the consolidated admin operational screens
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

    private static void AddPersistence(this IServiceCollection services, IHostEnvironment environment)
    {
        // One application DbContext; Identity shares it. Migrations live in this project
        // under Data/Migrations.
        // The connection string is resolved from the *built* IConfiguration when the context
        // options are created, never captured here at registration time. Reading it eagerly
        // silently ignored any configuration source added after AddFaedPlatform runs — which
        // is exactly how a test host overrides it — and pointed the integration test host at
        // the application database instead of its disposable one.
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseSqlServer(ResolveDatabaseConnectionString(
                serviceProvider.GetRequiredService<IConfiguration>(), environment)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>
    /// Resolves the SQL Server connection string and fails fast when a non-Development
    /// environment has none configured, or is still pointed at the committed local
    /// development database. The development connection string lives only in
    /// <c>appsettings.Development.json</c>; every other environment must supply its own via
    /// <c>ConnectionStrings__DefaultConnection</c>. Exposed for a focused test.
    /// </summary>
    public static string ResolveDatabaseConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // The "Testing" environment is the integration-test host, which injects its own
        // disposable LocalDB catalog and asserts the target separately
        var enforceProductionSafety = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(enforceProductionSafety
                ? $"Connection string 'DefaultConnection' is not configured for the " +
                  $"'{environment.EnvironmentName}' environment. Set the " +
                  "ConnectionStrings__DefaultConnection environment variable to a SQL Server " +
                  "the application login can reach (DEPLOYMENT.md §2)."
                : "Connection string 'DefaultConnection' not found. In Development it is set in " +
                  "appsettings.Development.json; override it with user secrets or " +
                  "ConnectionStrings__DefaultConnection.");
        }

        if (enforceProductionSafety && TargetsLocalDevelopmentDatabase(connectionString))
        {
            throw new InvalidOperationException(
                $"The '{environment.EnvironmentName}' environment is configured with a local " +
                "development database connection string (SQL Server LocalDB). Configure " +
                "ConnectionStrings__DefaultConnection with the real database for this " +
                "environment (DEPLOYMENT.md §2).");
        }

        return connectionString;
    }

    private static bool TargetsLocalDevelopmentDatabase(string connectionString)
    {
        try
        {
            var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource ?? string.Empty;
            return dataSource.Contains("localdb", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // A malformed connection string is not our concern here — UseSqlServer will
            // surface it. Do not mask that with a "looks like LocalDB" message.
            return false;
        }
    }

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
                        "Verification documents are private.");
                }

                options.LocalRootPath = resolvedRoot;
            });

        if (!environment.IsDevelopment())
        {
            services.AddOptions<R2FileStorageOptions>()
                .Bind(configuration.GetSection(R2FileStorageOptions.SectionName));

            services.AddSingleton<IFileStorage, R2FileStorage>();
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
