using System.Text;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Subscriptions;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Data.Seed;

/// <summary>
/// Deterministic development/demo data set for field validation and portfolio demonstration
/// <para>
/// Every merchant, listing, order and review it creates goes
/// through the <em>same</em> application services and the <em>same</em> production rules a
/// real request would. It never writes aggregates directly, never bypasses moderation,
/// authorization, price integrity or stock concurrency, and never relaxes a validation rule.
/// The only thing that is "demo-only" is <em>when</em> it runs — Development environment,
/// explicitly enabled, password supplied out-of-band (see <see cref="DemoDataOptions"/>).
/// </para>
/// <para>
/// <b>Reliability &amp; query pressure.</b> The scenario is built as one linear pass over a
/// single scope; every lookup is a projected <c>AsNoTracking</c> query (no full table is
/// loaded); the change tracker is cleared before the transactional scenarios; and the
/// context's command timeout is raised to five minutes so a query does not abort under the
/// brief SQL Server LocalDB starvation a full test run can cause.
/// </para>
/// <para>
/// <b>Idempotency &amp; recovery.</b> "Fully seeded" is defined by the final artifact (the
/// buyer's review). If it is present, <see cref="SeedCoreAsync"/> is a no-op. If a previous
/// run was interrupted (some demo accounts exist but the review does not),
/// <see cref="SeedCoreAsync"/> first <em>purges</em> the partial demo data — in
/// foreign-key-safe order — and then rebuilds it from scratch. Restarting the app is enough
/// to recover; a manual <c>ef database drop</c> is not required.
/// </para>
/// </summary>
public static class DemoDataSeeder
{
    // Fixed, obviously-non-production identities.
    public const string AdminEmail = "demo-admin@faed.local";
    public const string MerchantAEmail = "merchant-a@faed.local";
    public const string MerchantBEmail = "merchant-b@faed.local";
    public const string PendingMerchantEmail = "pending-merchant@faed.local";
    public const string BuyerAEmail = "buyer-a@faed.local";
    public const string BuyerBEmail = "buyer-b@faed.local";

    private static readonly string[] DemoEmails =
    [
        AdminEmail, MerchantAEmail, MerchantBEmail, PendingMerchantEmail, BuyerAEmail, BuyerBEmail,
    ];

    private const int ClearanceOpeningQuantity = 4;
    private const int LowStockOpeningQuantity = 3;

    public static async Task SeedAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder).FullName!);

        if (!environment.IsDevelopment())
        {
            return;
        }

        var options = new DemoDataOptions();
        using (var scope = services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IConfiguration>()
                .GetSection(DemoDataOptions.SectionName).Bind(options);
        }

        if (!options.Enabled)
        {
            logger.LogInformation(
                "Demo data seed skipped: set {SectionName}:Enabled=true (Development only) to enable it.",
                DemoDataOptions.SectionName);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "Demo data seed is enabled but no password is set. Set {SectionName}:Password via user secrets " +
                "or the Faed__DemoSeed__Password environment variable.",
                DemoDataOptions.SectionName);
            return;
        }

        if (await SeedCoreAsync(services, options.Password!, cancellationToken))
        {
            logger.LogInformation("Demo data set seeded.");
        }
        else
        {
            logger.LogInformation("Demo data already present; skipping demo seed.");
        }
    }

    /// <summary>
    /// Applies the demo data set without the environment / opt-in / password guards — callers
    /// own those. Returns <c>false</c> when the data set is already complete. If a previous
    /// run was interrupted, the partial data is purged and the set is rebuilt.
    /// </summary>
    public static async Task<bool> SeedCoreAsync(
        IServiceProvider serviceProvider, string password, CancellationToken cancellationToken = default)
    {
        var state = await InspectAsync(serviceProvider, cancellationToken);
        if (state == SeedState.Complete)
        {
            return false;
        }

        if (state == SeedState.Partial)
        {
            await PurgeAsync(serviceProvider, cancellationToken);
        }

        using var scope = serviceProvider.CreateScope();
        await new DemoSeedRun(scope.ServiceProvider, password, cancellationToken).RunAsync();
        return true;
    }

    private enum SeedState { Empty, Partial, Complete }

    private static async Task<SeedState> InspectAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var demoUserIds = await db.Users.AsNoTracking()
            .Where(u => u.Email != null && DemoEmails.Contains(u.Email))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (demoUserIds.Count == 0)
        {
            return SeedState.Empty;
        }

        // The last thing RunAsync does is submit the buyer's 5-star review.
        var complete = await db.Reviews.AsNoTracking()
            .AnyAsync(r => demoUserIds.Contains(r.ReviewerUserId) && r.Rating == 5, cancellationToken);

        return complete ? SeedState.Complete : SeedState.Partial;
    }

    /// <summary>Removes every row the demo seed creates, in foreign-key-safe order.</summary>
    private static async Task PurgeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var userIds = await db.Users.AsNoTracking()
            .Where(u => u.Email != null && DemoEmails.Contains(u.Email))
            .Select(u => u.Id).ToListAsync(cancellationToken);

        var merchantIds = await db.MerchantProfiles
            .Where(p => userIds.Contains(p.UserId)).Select(p => p.Id).ToListAsync(cancellationToken);
        var listingIds = await db.Listings
            .Where(l => merchantIds.Contains(l.MerchantProfileId)).Select(l => l.Id).ToListAsync(cancellationToken);
        var orderIds = await db.Orders
            .Where(o => merchantIds.Contains(o.MerchantProfileId)).Select(o => o.Id).ToListAsync(cancellationToken);

        await DeleteAsync(db, db.Reviews.Where(r => merchantIds.Contains(r.ReviewedMerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.Orders.Where(o => orderIds.Contains(o.Id)), cancellationToken);
        await DeleteAsync(db, db.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.Listings.Where(l => listingIds.Contains(l.Id)), cancellationToken);
        await DeleteAsync(db, db.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)), cancellationToken);

        foreach (var id in userIds)
        {
            var user = await users.FindByIdAsync(id);
            if (user is not null)
            {
                await users.DeleteAsync(user);
            }
        }
    }

    private static async Task DeleteAsync<T>(ApplicationDbContext db, IQueryable<T> rows, CancellationToken cancellationToken)
        where T : class
    {
        var loaded = await rows.ToListAsync(cancellationToken);
        if (loaded.Count == 0)
        {
            return;
        }

        db.Set<T>().RemoveRange(loaded);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One linear build of the demo scenario over a single scope.</summary>
    private sealed class DemoSeedRun
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IMerchantVerificationService _verification;
        private readonly IMerchantListingService _listings;
        private readonly IListingModerationService _moderation;
        private readonly IMerchantStoreService _store;
        private readonly IOrderService _orders;
        private readonly IReviewService _reviews;
        private readonly ISubscriptionService _subscriptions;
        private readonly string _password;
        private readonly CancellationToken _ct;

        public DemoSeedRun(IServiceProvider sp, string password, CancellationToken ct)
        {
            _password = password;
            _ct = ct;
            _db = sp.GetRequiredService<ApplicationDbContext>();
            _users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            _verification = sp.GetRequiredService<IMerchantVerificationService>();
            _listings = sp.GetRequiredService<IMerchantListingService>();
            _moderation = sp.GetRequiredService<IListingModerationService>();
            _store = sp.GetRequiredService<IMerchantStoreService>();
            _orders = sp.GetRequiredService<IOrderService>();
            _reviews = sp.GetRequiredService<IReviewService>();
            _subscriptions = sp.GetRequiredService<ISubscriptionService>();

            // A generous command timeout. The seed does not race anything in a real
            // Development database, but a CI box or a workstation running the whole test
            // suite can leave SQL Server LocalDB briefly starved; without this a routine
            // query can hit the 30s default and abort the seed.
            _db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        }

        public async Task RunAsync()
        {
            var adminId = await CreateUserAsync(AdminEmail, FaedRoles.Admin);
            var buyerAId = await CreateUserAsync(BuyerAEmail, FaedRoles.Buyer);
            var buyerBId = await CreateUserAsync(BuyerBEmail, FaedRoles.Buyer);

            var merchantA = await CreateApprovedMerchantAsync(
                MerchantAEmail, "Amman Kitchen Co.", "hello@amman-kitchen.example", "+962 6 500 0001", adminId, "Standard");
            var merchantB = await CreateApprovedMerchantAsync(
                MerchantBEmail, "Petra Power Tools", "sales@petra-power-tools.example", "+962 6 500 0002", adminId, "Basic");
            await CreatePendingMerchantAsync(
                PendingMerchantEmail, "Rainbow Home Essentials", "info@rainbow-home.example", "+962 6 500 0003");

            await ConfigureFulfillmentAsync(merchantA, "Amman Kitchen Co. — Abdali", "12 Rafiq Al Hariri Ave", "Abdali");
            await ConfigureFulfillmentAsync(merchantB, "Petra Power Tools — Sweifieh", "8 Wakalat St", "Sweifieh");

            var kettle = await CreateKettleListingAsync(merchantA, adminId);
            var vacuum = await CreateVacuumListingAsync(merchantA, adminId);
            await CreateToasterListingAsync(merchantA, adminId);
            await CreateHandMixerListingAsync(merchantA, adminId);
            await CreateIronListingAsync(merchantA, adminId);
            await CreateBlenderListingAsync(merchantA, adminId);

            await CreateDrillListingAsync(merchantB, adminId);
            var clearance = await CreateClearanceVacuumListingAsync(merchantB, adminId);
            var circularSaw = await CreateCircularSawListingAsync(merchantB, adminId);
            await CreateAngleGrinderListingAsync(merchantB, adminId);
            await CreateScrewdriverSetListingAsync(merchantB, adminId);
            await CreateToolBagListingAsync(merchantB, adminId);

            // Drop everything the listing build tracked before the transactional scenarios so
            // the order/negotiation/deal services start against a clean change tracker.
            _db.ChangeTracker.Clear();

            // One active B2C order: placed by Buyer A, confirmed by the merchant.
            var activeOrderId = await PlaceOrderAsync(
                buyerAId, merchantA, [(kettle.VariantIds[0], 1), (kettle.VariantIds[1], 1)], "Buyer A", "+962 79 000 0001");
            Ok(await _orders.ConfirmAsync(merchantA.UserId, activeOrderId, _ct), "confirm active demo order");

            // One completed B2C order: fully fulfilled and confirmed by the buyer.
            var completedOrderId = await PlaceOrderAsync(
                buyerBId, merchantA, [(vacuum.VariantIds[0], 1)], "Buyer B", "+962 79 000 0002");
            Ok(await _orders.ConfirmAsync(merchantA.UserId, completedOrderId, _ct), "confirm completed demo order");
            Ok(await _orders.MarkReadyForPickupAsync(merchantA.UserId, completedOrderId, _ct), "ready completed demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerBId, completedOrderId, _ct), "buyer confirms completed demo order");

            // One sold-out listing for public sold-out behaviour: a buyer clears the last units.
            var clearanceOrderId = await PlaceOrderAsync(
                buyerAId, merchantB, [(clearance.VariantIds[0], ClearanceOpeningQuantity)], "Buyer A", "+962 79 000 0001");
            Ok(await _orders.ConfirmAsync(merchantB.UserId, clearanceOrderId, _ct), "confirm clearance demo order");
            Ok(await _orders.MarkReadyForPickupAsync(merchantB.UserId, clearanceOrderId, _ct), "ready clearance demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerAId, clearanceOrderId, _ct), "buyer confirms clearance demo order");

            // One dispatched delivery order: demonstrates merchant-delivery fulfilment and the
            // OutForDelivery lifecycle state, left short of completion.
            var deliveryOrderId = await PlaceDeliveryOrderAsync(
                buyerBId, [(circularSaw.VariantIds[0], 1)], "Buyer B", "+962 79 000 0002",
                "14 Al Yarmouk St, Sweifieh, Amman");
            Ok(await _orders.ConfirmAsync(merchantB.UserId, deliveryOrderId, _ct), "confirm delivery demo order");
            Ok(await _orders.MarkOutForDeliveryAsync(merchantB.UserId, deliveryOrderId, _ct), "dispatch delivery demo order");

            // One review: the buyer leaves a positive review on the completed B2C order.
            Ok(
                await _reviews.SubmitReviewAsync(buyerBId, new SubmitReviewInput(
                    completedOrderId, 5,
                    "Vacuum was exactly as described, including the disclosed scuff. Smooth pickup."), _ct),
                "submit demo review");
        }

        // ---- Users & merchants -------------------------------------------------------

        private async Task<string> CreateUserAsync(
    string email,
    string? role = null)
        {
            var (firstName, lastName) = email switch
            {
                AdminEmail => ("Demo", "Admin"),
                MerchantAEmail => ("Demo", "Merchant A"),
                MerchantBEmail => ("Demo", "Merchant B"),
                PendingMerchantEmail => ("Pending", "Merchant"),
                BuyerAEmail => ("Demo", "Buyer A"),
                BuyerBEmail => ("Demo", "Buyer B"),
                _ => ("Demo", "User")
            };

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = "+962790000000",
                CreatedAtUtc = DateTime.UtcNow,
            };

            var created = await _users.CreateAsync(user, _password);

            if (!created.Succeeded)
            {
                throw Fail(
                    $"create user '{email}'",
                    created.Errors.Select(e => e.Description));
            }

            if (role is not null)
            {
                var granted =
                    await _users.AddToRoleAsync(user, role);

                if (!granted.Succeeded)
                {
                    throw Fail(
                        $"grant '{role}' to '{email}'",
                        granted.Errors.Select(e => e.Description));
                }
            }

            return user.Id;
        }

        private async Task<DemoMerchant> CreateApprovedMerchantAsync(
            string email, string businessName, string contactEmail, string contactPhone, string adminId, string planCode)
        {
            // No starter role: approving the verification grants the Merchant role, exactly as
            // it would for a real applicant.
            var userId = await CreateUserAsync(email);
            var profileId = OkValue(
                await _verification.SaveDraftAsync(userId, new MerchantApplicationInput(businessName, contactEmail, contactPhone), _ct),
                $"save merchant draft for {businessName}");

            Ok(
                await _verification.AddDocumentAsync(userId, new AddVerificationDocumentInput(
                    MerchantVerificationDocumentType.CommercialRegistration,
                    new MemoryStream(DemoAssets.Pdf), "commercial-registration.pdf", "application/pdf", DemoAssets.Pdf.Length), _ct),
                $"attach verification document for {businessName}");
            Ok(await _verification.SubmitForReviewAsync(userId, _ct), $"submit {businessName} for verification");
            Ok(await _verification.ApproveAsync(adminId, profileId, _ct), $"approve {businessName}");

            // Verification before payment (BUSINESS-MODEL.md §7): the plan is chosen and
            // activated only after approval, exactly like a real merchant's path.
            var plans = await _subscriptions.GetAvailablePlansAsync(_ct);
            var plan = plans.SingleOrDefault(p => p.Code == planCode)
                ?? throw Fail($"choose plan for {businessName}", [$"No active subscription plan with code '{planCode}'."]);
            Ok(await _subscriptions.ChoosePlanAsync(userId, plan.Id, _ct), $"choose {planCode} plan for {businessName}");
            Ok(await _subscriptions.ActivateAsync(adminId, profileId, $"DEMO-{planCode.ToUpperInvariant()}-{businessName}", _ct),
                $"activate {planCode} subscription for {businessName}");

            return new DemoMerchant(userId, profileId, businessName);
        }

        private async Task CreatePendingMerchantAsync(
            string email, string businessName, string contactEmail, string contactPhone)
        {
            var userId = await CreateUserAsync(email);
            OkValue(
                await _verification.SaveDraftAsync(userId, new MerchantApplicationInput(businessName, contactEmail, contactPhone), _ct),
                $"save merchant draft for {businessName}");
            Ok(
                await _verification.AddDocumentAsync(userId, new AddVerificationDocumentInput(
                    MerchantVerificationDocumentType.CommercialRegistration,
                    new MemoryStream(DemoAssets.Pdf), "commercial-registration.pdf", "application/pdf", DemoAssets.Pdf.Length), _ct),
                $"attach verification document for {businessName}");
            Ok(await _verification.SubmitForReviewAsync(userId, _ct), $"submit {businessName} for verification");
            // Deliberately left PendingReview — no admin decision.
        }

        private async Task ConfigureFulfillmentAsync(DemoMerchant merchant, string locationName, string address, string area)
        {
            Ok(
                await _store.AddLocationAsync(merchant.UserId, new MerchantLocationInput(
                    locationName, address, area, "Amman", "Ask for the trade counter.", "Sun–Thu 10:00–18:00"), _ct),
                $"add pickup location for {merchant.BusinessName}");
        }

        // ---- Listings — Amman Kitchen Co. (small kitchen appliances / home & cleaning) ----

        private async Task<DemoListing> CreateKettleListingAsync(DemoMerchant merchant, string adminId)
        {
            // Listing 2 — Kettle, Condition A, Overstock, Capacity 1.5L/1.7L/2L × Colour Black/White.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("small-kitchen-appliances"), await GradeIdAsync("A"),
                "Rapid-Boil Electric Kettle (Overstock)",
                "End-of-run stock of our best-selling rapid-boil kettle. Sealed and unopened; " +
                "the only reason for the discount is that we over-ordered for the season.",
                null, 12.000m,
                "14-day exchange on unopened units.", WarrantyType.ManufacturerWarranty, 12,
                "One kettle, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create kettle listing");
            var capacity = await AddOptionAsync(merchant.UserId, listingId, "Capacity", "1.5L", "1.7L", "2L");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black", "White");
            await AddVariantAsync(merchant.UserId, listingId, "KETTLE-BLK-15L", [capacity["1.5L"], colour["Black"]], 40);
            await AddVariantAsync(merchant.UserId, listingId, "KETTLE-WHT-17L", [capacity["1.7L"], colour["White"]], 25);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "tee-front.png", "Boxed black electric kettle, front view");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("Overstock")] }, _ct),
                "attach kettle discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateVacuumListingAsync(DemoMerchant merchant, string adminId)
        {
            // Listing 3 — Vacuum cleaner, Condition D, Display Item, visible cosmetic-defect photo.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("home-cleaning"), await GradeIdAsync("D"),
                "Bagless Upright Vacuum — Display Unit",
                "Former showroom vacuum. Structurally perfect and fully functional; there is light " +
                "surface scuffing to one bottom corner from the display stand, shown in the defect photo.",
                null, 55.000m,
                "Sold as-is; no exchange on clearance display units.", WarrantyType.ShopWarranty, 3,
                "Vacuum and dust bag.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create vacuum listing");
            await AddVariantAsync(merchant.UserId, listingId, "VAC-UPRIGHT", [], 3);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "tote-front.png", "Upright vacuum cleaner, front view");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Defect, "tote-corner-scuff.png", "Close-up of light scuffing on the bottom corner");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("DisplayItem")] }, _ct),
                "attach vacuum discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateToasterListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("small-kitchen-appliances"), await GradeIdAsync("B"),
                "Classic 2-Slice Toaster (Superseded Model)",
                "Last year's colourway of our best-selling 2-slice toaster. Sealed and unopened; the " +
                "retail box was opened for a photo shoot, which is why it is being cleared at a discount.",
                null, 28.000m,
                "14-day exchange on unopened units.", WarrantyType.ManufacturerWarranty, 24,
                "One toaster, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create toaster listing");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black", "Silver", "Red");
            await AddVariantAsync(merchant.UserId, listingId, "TOAST-BLK", [colour["Black"]], 10);
            await AddVariantAsync(merchant.UserId, listingId, "TOAST-SLV", [colour["Silver"]], 18);
            await AddVariantAsync(merchant.UserId, listingId, "TOAST-RED", [colour["Red"]], 12);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "denim-jacket-front.png", "Black 2-slice toaster, front view");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "denim-jacket-detail.png", "Control dial detail");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Packaging, "denim-jacket-box.png", "Retail box opened for a photo shoot");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("SupersededModel")] }, _ct),
                "attach toaster discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateHandMixerListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("small-kitchen-appliances"), await GradeIdAsync("A"),
                "5-Speed Hand Mixer — Final Units",
                "Compact hand mixer from our overstock run. Sealed and unopened; only a handful of " +
                "units are left after our promotion.",
                null, 9.500m,
                "7-day exchange while stock lasts.", WarrantyType.None, null, "One hand mixer, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create hand mixer listing");
            await AddVariantAsync(merchant.UserId, listingId, "MIXER-STD", [], LowStockOpeningQuantity);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "wool-scarf.png", "5-speed hand mixer, flat lay");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("Overstock")] }, _ct),
                "attach hand mixer discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateIronListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("home-cleaning"), await GradeIdAsync("C"),
                "Steam Iron — Customer Return",
                "Steam iron returned unused within our exchange window. Inspected, re-boxed " +
                "and in full working order; sold at a discount because it can no longer be sold as new.",
                null, 14.000m,
                "Sold as-is; no further exchange on returned units.", WarrantyType.ManufacturerWarranty, 6,
                "One iron, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create iron listing");
            await AddVariantAsync(merchant.UserId, listingId, "IRON-STD", [], 15);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "leather-belt.png", "Steam iron, front view");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("CustomerReturn")] }, _ct),
                "attach iron discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateBlenderListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("small-kitchen-appliances"), await GradeIdAsync("B"),
                "Heavy-Duty Blender (Packaging Damage)",
                "Durable countertop blender with a shatterproof jug. Sealed and unused; some " +
                "retail boxes arrived crushed from the freight pallet, which is why these are discounted.",
                null, 24.000m,
                "14-day exchange on unused items.", WarrantyType.ManufacturerWarranty, 12,
                "One blender; box condition varies.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create blender listing");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black", "Red");
            await AddVariantAsync(merchant.UserId, listingId, "BLEND-BLK", [colour["Black"]], 20);
            await AddVariantAsync(merchant.UserId, listingId, "BLEND-RED", [colour["Red"]], 15);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "canvas-backpack.png", "Red countertop blender, front view");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Packaging, "canvas-backpack-box.png", "Example of a crushed retail box");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("PackagingDamage")] }, _ct),
                "attach blender discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        // ---- Listings — Petra Power Tools (power tools / home & cleaning) -------------

        private async Task<DemoListing> CreateDrillListingAsync(DemoMerchant merchant, string adminId)
        {
            // Listing 1 — Drill, Condition B, Superseded Model + Packaging Damage,
            // Voltage 12V/18V/20V × Colour Black.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("power-tools"), await GradeIdAsync("B"),
                "Cordless Drill Driver (Superseded Model)",
                "Last year's colourway of our cordless drill driver. Sealed and unused; some boxes " +
                "are crushed or missing lids from warehouse handling, which is why they are discounted.",
                null, 45.000m,
                "14-day exchange on unused units in any condition of box.", WarrantyType.ManufacturerWarranty, 24,
                "One drill; box condition varies.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create drill listing");
            var voltage = await AddOptionAsync(merchant.UserId, listingId, "Voltage", "12V", "18V", "20V");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black");
            await AddVariantAsync(merchant.UserId, listingId, "DRILL-BLK-12V", [voltage["12V"], colour["Black"]], 30);
            await AddVariantAsync(merchant.UserId, listingId, "DRILL-BLK-18V", [voltage["18V"], colour["Black"]], 30);
            await AddVariantAsync(merchant.UserId, listingId, "DRILL-BLK-20V", [voltage["20V"], colour["Black"]], 20);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "court-low-pair.png", "Cordless drill driver with battery");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Packaging, "court-low-box.png", "Example of a crushed retail box");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("SupersededModel"), await ReasonIdAsync("PackagingDamage")] }, _ct),
                "attach drill discount reasons");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateClearanceVacuumListingAsync(DemoMerchant merchant, string adminId)
        {
            // Listing 4 — a listing that ends up sold out, for public
            // sold-out behaviour. Opens with a small stock a demo buyer then clears.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("home-cleaning"), await GradeIdAsync("C"),
                "Handheld Vacuum — Final Units",
                "Customer-returned but unused handheld vacuums from our winter range. Inspected and " +
                "re-boxed. Only a handful of units left.",
                null, 38.000m,
                "14-day exchange while stock lasts.", WarrantyType.None, null, "One handheld vacuum, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create clearance vacuum listing");
            await AddVariantAsync(merchant.UserId, listingId, "VAC-HANDHELD", [], ClearanceOpeningQuantity);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "merino-half-zip.png", "Grey handheld vacuum, flat lay");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("CustomerReturn")] }, _ct),
                "attach clearance discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateCircularSawListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("power-tools"), await GradeIdAsync("A"),
                "TrailHead Circular Saw — Overstock",
                "A model we simply over-ordered for the season. Sealed, unused and boxed; nothing wrong " +
                "with it, just more stock than we can sell at full price.",
                null, 42.000m,
                "14-day exchange on unused units.", WarrantyType.ManufacturerWarranty, 24, "One saw, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create circular saw listing");
            var bladeSize = await AddOptionAsync(merchant.UserId, listingId, "Blade size", "150mm", "165mm", "185mm");
            await AddVariantAsync(merchant.UserId, listingId, "SAW-150", [bladeSize["150mm"]], 25);
            await AddVariantAsync(merchant.UserId, listingId, "SAW-165", [bladeSize["165mm"]], 25);
            await AddVariantAsync(merchant.UserId, listingId, "SAW-185", [bladeSize["185mm"]], 20);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "running-shoes-pair.png", "TrailHead circular saw");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("Overstock")] }, _ct),
                "attach circular saw discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateAngleGrinderListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("power-tools"), await GradeIdAsync("D"),
                "Angle Grinder — Display Unit",
                "Former showroom angle grinder. Structurally sound and fully functional; there is a light " +
                "mark on the housing from the display stand, shown in the defect photo.",
                null, 19.000m,
                "Sold as-is; no exchange on clearance display units.", WarrantyType.None, null, "One grinder, no box.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create angle grinder listing");
            await AddVariantAsync(merchant.UserId, listingId, "GRINDER-STD", [], 6);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "leather-sandals-front.png", "Angle grinder, front view");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Defect, "leather-sandals-scuff.png", "Close-up of a light mark on the housing");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("DisplayItem")] }, _ct),
                "attach angle grinder discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateScrewdriverSetListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("power-tools"), await GradeIdAsync("A"),
                "Precision Screwdriver Set — Final Units",
                "20-piece precision screwdriver set from our overstock run. Sealed and unopened; only a " +
                "few sets are left.",
                null, 6.500m,
                "7-day exchange while stock lasts.", WarrantyType.None, null, "One set, boxed.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create screwdriver set listing");
            await AddVariantAsync(merchant.UserId, listingId, "SCREWDRIVER-SET", [], LowStockOpeningQuantity + 2);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "sports-socks.png", "Precision screwdriver set, flat lay");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("Overstock")] }, _ct),
                "attach screwdriver set discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateToolBagListingAsync(DemoMerchant merchant, string adminId)
        {
            var details = new ListingDetailsInput(
                await CategoryIdAsync("power-tools"), await GradeIdAsync("C"),
                "Canvas Tool Bag — Cosmetic Defect",
                "Heavy-duty canvas tool bag with reinforced base. New and unused; the " +
                "printed logo is slightly off-centre, which does not affect use.",
                null, 11.000m,
                "7-day exchange on unused units.", WarrantyType.None, null, "One tool bag.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create tool bag listing");
            await AddVariantAsync(merchant.UserId, listingId, "TOOLBAG-STD", [], 12);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "shoe-bag-set.png", "Canvas tool bag, flat lay");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Defect, "shoe-bag-set-logo.png", "Close-up of the off-centre printed logo");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("CosmeticDefect")] }, _ct),
                "attach tool bag discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        // ---- Listing build helpers ----------------------------------------------------

        private async Task<Dictionary<string, Guid>> AddOptionAsync(
            string userId, Guid listingId, string name, params string[] values)
        {
            Ok(await _listings.AddOptionAsync(userId, listingId, name, _ct), $"add option {name}");
            var optionId = await _db.Set<ListingOption>().AsNoTracking()
                .Where(o => o.ListingId == listingId && o.Name == name)
                .Select(o => o.Id).SingleAsync(_ct);

            foreach (var value in values)
            {
                Ok(await _listings.AddOptionValueAsync(userId, listingId, optionId, value, _ct), $"add option value {name}={value}");
            }

            return await _db.Set<ListingOptionValue>().AsNoTracking()
                .Where(v => v.ListingOptionId == optionId)
                .ToDictionaryAsync(v => v.Value, v => v.Id, _ct);
        }

        private async Task AddVariantAsync(
            string userId, Guid listingId, string sku, IReadOnlyList<Guid> optionValueIds, int quantity) =>
            Ok(
                await _listings.AddVariantAsync(userId, listingId, new AddVariantInput(sku, optionValueIds, quantity), _ct),
                $"add variant {sku}");

        private async Task AddImageAsync(
            string userId, Guid listingId, ListingMediaType type, string fileName, string altText)
        {
            var bytes = DemoAssets.LoadImage(fileName);
            Ok(
                await _listings.AddImageAsync(userId, listingId, new AddListingImageInput(
                    type, new MemoryStream(bytes), fileName, "image/png", bytes.Length, altText), _ct),
                $"add {type} image {fileName}");
        }

        private async Task PublishAsync(string userId, string adminId, Guid listingId)
        {
            Ok(await _listings.SubmitForReviewAsync(userId, listingId, _ct), "submit listing for review");
            Ok(await _moderation.ApproveAsync(adminId, listingId, "Demo data: approved.", _ct), "approve listing");
        }

        private async Task<DemoListing> DescribeListingAsync(Guid listingId)
        {
            var slug = await _db.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync(_ct);
            var variantIds = await _db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).OrderBy(v => v.Sku).Select(v => v.Id).ToListAsync(_ct);
            return new DemoListing(listingId, slug, variantIds);
        }

        // ---- Transaction helpers -------------------------------------------------------

        private async Task<Guid> PlaceOrderAsync(
            string buyerId, DemoMerchant merchant,
            IReadOnlyList<(Guid VariantId, int Quantity)> lines, string contactName, string contactPhone)
        {
            var settings = await _store.GetSettingsAsync(merchant.UserId, _ct);
            var locationId = settings.Locations.First(l => l.IsActive).Id;

            return OkValue(
                await _orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
                    [.. lines.Select(l => new OrderLineInput(l.VariantId, l.Quantity))],
                    OrderFulfillmentType.Pickup, locationId, null, contactName, contactPhone, null), _ct),
                "place demo order");
        }

        private async Task<Guid> PlaceDeliveryOrderAsync(
            string buyerId,
            IReadOnlyList<(Guid VariantId, int Quantity)> lines, string contactName, string contactPhone,
            string deliveryAddress)
        {
            return OkValue(
                await _orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
                    [.. lines.Select(l => new OrderLineInput(l.VariantId, l.Quantity))],
                    OrderFulfillmentType.MerchantDelivery, null, deliveryAddress, contactName, contactPhone, null), _ct),
                "place demo delivery order");
        }

        // ---- Reference-data lookups ---------------------------------------------------

        private Task<Guid> CategoryIdAsync(string slug) =>
            _db.Categories.AsNoTracking().Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync(_ct);

        private Task<Guid> GradeIdAsync(string code) =>
            _db.ConditionGrades.AsNoTracking().Where(g => g.Code == code).Select(g => g.Id).SingleAsync(_ct);

        private Task<Guid> ReasonIdAsync(string code) =>
            _db.DiscountReasons.AsNoTracking().Where(r => r.Code == code).Select(r => r.Id).SingleAsync(_ct);

        // ---- Result guards ----------------------------------------------------------

        private static void Ok(Result result, string what)
        {
            if (result.Failed)
            {
                throw new InvalidOperationException($"Demo seed step failed ({what}): {result.ErrorKind} — {result.Error}");
            }
        }

        private static T OkValue<T>(Result<T> result, string what)
        {
            Ok(result, what);
            return result.Value;
        }

        private static InvalidOperationException Fail(string what, IEnumerable<string> errors) =>
            new($"Demo seed could not {what}: {string.Join(", ", errors)}");
    }

    private readonly record struct DemoMerchant(string UserId, Guid ProfileId, string BusinessName);

    private readonly record struct DemoListing(Guid Id, string Slug, IReadOnlyList<Guid> VariantIds);

    /// <summary>
    /// Media fixtures for the Development-only demo seed. Product photography is a set of
    /// small, original flat-illustration PNGs generated locally by
    /// <c>tools/demo-images/generate-demo-images.ps1</c> (System.Drawing/GDI+) — nothing is
    /// downloaded or hotlinked, so there is no licensing concern. Each file lives under
    /// <c>Data/Seed/Assets/Images</c> and is copied next to the built application (see the
    /// <c>Content</c> item in Faed.Web.csproj), so it is reachable from disk at seed time
    /// whether the app is run with <c>dotnet run</c> or from a built <c>bin</c> output.
    /// The verification-document PDF stays a tiny generated fixture: it is never shown to
    /// buyers, so it does not need to look realistic
    /// </summary>
    private static class DemoAssets
    {
        private static readonly string ImagesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "Assets", "Images");

        private static readonly Dictionary<string, byte[]> ImageCache = new(StringComparer.OrdinalIgnoreCase);

        public static byte[] LoadImage(string fileName)
        {
            if (ImageCache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            var path = Path.Combine(ImagesDirectory, fileName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Demo seed image '{fileName}' was not found at '{path}'. Regenerate the demo image set " +
                    "with tools/demo-images/generate-demo-images.ps1 before enabling the demo seed.");
            }

            var bytes = File.ReadAllBytes(path);
            ImageCache[fileName] = bytes;
            return bytes;
        }

        public static byte[] Pdf { get; } = BuildMinimalPdf();

        private static byte[] BuildMinimalPdf()
        {
            using var pdf = new MemoryStream();
            void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));

            Write("%PDF-1.7\n");
            var catalogOffset = checked((int)pdf.Position);
            Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            var pagesOffset = checked((int)pdf.Position);
            Write("2 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\n");
            var xrefOffset = checked((int)pdf.Position);
            Write("xref\n0 3\n");
            Write("0000000000 65535 f \n");
            Write($"{catalogOffset:D10} 00000 n \n");
            Write($"{pagesOffset:D10} 00000 n \n");
            Write($"trailer\n<< /Size 3 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
            return pdf.ToArray();
        }
    }
}
