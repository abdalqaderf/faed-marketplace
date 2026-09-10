using System.Text;
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
/// Deterministic development/demo data for field validation and portfolio demonstration.
/// <para>
/// Every account, listing, order and review it creates goes through the <em>same</em>
/// application services, and the <em>same</em> production rules, a real request would: a
/// merchant is verified before it is subscribed and subscribed before it can publish; a
/// listing is submitted through the one-page form and approved by an admin before it is
/// public; an order moves state only through <see cref="IOrderService"/>. The seeder never
/// writes an aggregate directly and never relaxes a validation rule — the only thing that is
/// "demo-only" is <em>when</em> it runs (Development, explicitly enabled, password supplied
/// out-of-band; see <see cref="DemoDataOptions"/>). The only direct database use is read-only:
/// resolving reference-data ids and reading back the slug and variant id of a listing the
/// service just created.
/// </para>
/// <para>
/// <b>Idempotency &amp; recovery.</b> "Fully seeded" is defined by the final artifact (the
/// buyer's five-star review). If it is present, <see cref="SeedCoreAsync"/> is a no-op. If a
/// previous run was interrupted, the partial demo data is purged in foreign-key-safe order and
/// the set is rebuilt from scratch — restarting the app is enough to recover.
/// </para>
/// </summary>
public static class DemoDataSeeder
{
    // Fixed, obviously-non-production identities.
    public const string AdminEmail = "demo-admin@faed.local";
    public const string MerchantAEmail = "merchant-a@faed.local";
    public const string MerchantBEmail = "merchant-b@faed.local";
    public const string UnsubscribedMerchantEmail = "unsubscribed-merchant@faed.local";
    public const string PendingMerchantEmail = "pending-merchant@faed.local";
    public const string BuyerAEmail = "buyer-a@faed.local";
    public const string BuyerBEmail = "buyer-b@faed.local";

    private static readonly string[] DemoEmails =
    [
        AdminEmail, MerchantAEmail, MerchantBEmail, UnsubscribedMerchantEmail, PendingMerchantEmail,
        BuyerAEmail, BuyerBEmail,
    ];

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

        // The last thing RunAsync does is submit the buyer's five-star review.
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

        // Reviews reference orders; orders reference listings and variants; every one of those
        // FKs is Restrict, so the dependent rows go first. OrderItem, ListingModeration,
        // ListingMedia, ListingVariant and MerchantVerificationDocument all cascade from their
        // aggregate root and need no explicit pass.
        await DeleteAsync(db, db.Reviews.Where(r => merchantIds.Contains(r.ReviewedMerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.Orders.Where(o => orderIds.Contains(o.Id)), cancellationToken);
        await DeleteAsync(db, db.Listings.Where(l => listingIds.Contains(l.Id)), cancellationToken);
        await DeleteAsync(db, db.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.MerchantSubscriptions.Where(s => merchantIds.Contains(s.MerchantProfileId)), cancellationToken);
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
        private const string BuyerAName = "Layla Haddad";
        private const string BuyerAPhone = "+962 79 000 0001";
        private const string BuyerBName = "Omar Nasser";
        private const string BuyerBPhone = "+962 79 000 0002";

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

            // A generous command timeout: a CI box or a workstation running the whole test
            // suite can leave SQL Server LocalDB briefly starved, and without this a routine
            // query can hit the 30s default and abort the seed.
            _db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        }

        public async Task RunAsync()
        {
            var adminId = await CreateUserAsync(AdminEmail, "Demo", "Admin", FaedRoles.Admin);
            var buyerAId = await CreateUserAsync(BuyerAEmail, "Layla", "Haddad", FaedRoles.Buyer);
            var buyerBId = await CreateUserAsync(BuyerBEmail, "Omar", "Nasser", FaedRoles.Buyer);

            // Two merchants a buyer can actually shop from: verified and subscribed.
            var kitchenCo = await CreateApprovedMerchantAsync(
                MerchantAEmail, "Amman Kitchen Co.", "hello@amman-kitchen.example", "+962 6 500 0001", adminId, "Standard");
            var petraTools = await CreateApprovedMerchantAsync(
                MerchantBEmail, "Petra Power Tools", "sales@petra-power-tools.example", "+962 6 500 0002", adminId, "Basic");

            // The two valid states that are neither "shopping now": approved but not yet paying,
            // and applied but not yet reviewed.
            var unsubscribed = await CreateApprovedMerchantAsync(
                UnsubscribedMerchantEmail, "Sahara Appliance Outlet", "team@sahara-outlet.example",
                "+962 6 500 0004", adminId, planCode: null);
            await CreatePendingMerchantAsync(
                PendingMerchantEmail, "Rainbow Home Essentials", "info@rainbow-home.example", "+962 6 500 0003");

            await ConfigureFulfillmentAsync(kitchenCo, "Amman Kitchen Co. — Abdali", "12 Rafiq Al Hariri Ave", "Abdali");
            await ConfigureFulfillmentAsync(petraTools, "Petra Power Tools — Sweifieh", "8 Wakalat St", "Sweifieh");

            // A catalogue across all three categories that uses every condition card.
            var kettle = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "small-kitchen-appliances", ConditionChoice.Sealed, "Rapid-Boil Electric Kettle",
                "1.7 L stainless kettle, sealed. We over-ordered for the season — that is the only reason it is discounted.",
                WarrantyType.ManufacturerWarranty, 12, 12.000m, null, 6, "kettle.png", null));
            var handMixer = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "small-kitchen-appliances", ConditionChoice.Sealed, "5-Speed Hand Mixer",
                "Compact hand mixer from the same overstock run. Sealed box, full manufacturer warranty.",
                WarrantyType.ManufacturerWarranty, 12, 9.500m, null, 3, "hand-mixer.png", null));
            var toaster = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "small-kitchen-appliances", ConditionChoice.BoxOpenedOrDamaged, "2-Slice Toaster",
                "The toaster is new and unused; the retail box was opened and re-taped in the warehouse. Photo of the box included.",
                WarrantyType.ManufacturerWarranty, 24, 14.000m, null, 4, "toaster.png", "toaster-defect.png"));
            var blender = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "small-kitchen-appliances", ConditionChoice.BoxOpenedOrDamaged, "Countertop Blender",
                "Shatterproof 1.5 L jug. Unused, but the outer carton was crushed in transit — shown in the photo.",
                WarrantyType.ManufacturerWarranty, 12, 24.000m, null, 4, "blender.png", "blender-defect.png"));
            var steamIron = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "home-cleaning", ConditionChoice.CustomerReturn, "Steam Iron",
                "Returned within the exchange window and never used. Inspected and re-boxed; sold at a discount because it cannot be listed as new.",
                WarrantyType.ManufacturerWarranty, 6, 13.000m, null, 5, "steam-iron.png", null));
            var uprightVacuum = await CreateListingAsync(kitchenCo, adminId, new ListingSpec(
                "home-cleaning", ConditionChoice.ExDisplay, "Bagless Upright Vacuum",
                "Former showroom unit. Fully working; there is light scuffing on one bottom corner from the display stand, shown in the defect photo.",
                WarrantyType.ShopWarranty, 3, 45.000m, 79.000m, 2, "upright-vacuum.png", "upright-vacuum-defect.png"));

            var handheldVacuum = await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "home-cleaning", ConditionChoice.CustomerReturn, "Handheld Vacuum",
                "Customer-returned but unused handheld vacuums from the winter range. Inspected and re-boxed. Only a few left.",
                WarrantyType.None, null, 22.000m, null, 4, "handheld-vacuum.png", null));
            await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "power-tools", ConditionChoice.BoxOpenedOrDamaged, "Cordless Drill Driver",
                "18 V drill driver with one battery. Unused; several boxes lost their lids in the warehouse — photo included.",
                WarrantyType.ManufacturerWarranty, 24, 38.000m, null, 5, "cordless-drill.png", "cordless-drill-defect.png"));
            await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "power-tools", ConditionChoice.Sealed, "Circular Saw",
                "165 mm circular saw, sealed. A model we simply over-ordered — nothing wrong with it.",
                WarrantyType.ManufacturerWarranty, 24, 55.000m, 95.000m, 6, "circular-saw.png", null));
            await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "power-tools", ConditionChoice.ExDisplay, "Angle Grinder",
                "Ex-display grinder. Sound and functional; there is a light mark on the housing from the stand, shown in the photo.",
                WarrantyType.None, null, 19.000m, null, 3, "angle-grinder.png", "angle-grinder-defect.png"));
            await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "power-tools", ConditionChoice.Sealed, "Precision Screwdriver Set",
                "20-piece precision set from the overstock run. Sealed.",
                WarrantyType.None, null, 6.500m, null, 5, "screwdriver-set.png", null));
            await CreateListingAsync(petraTools, adminId, new ListingSpec(
                "power-tools", ConditionChoice.CustomerReturn, "Canvas Tool Bag",
                "Reinforced-base canvas tool bag, returned unused. The printed logo sits slightly off-centre, which does not affect use.",
                WarrantyType.None, null, 11.000m, null, 6, "tool-bag.png", null));

            // The approved-but-unsubscribed merchant has a draft they cannot publish yet — it is
            // the plan chooser, not an error, that stands between them and going live.
            await CreateDraftListingAsync(unsubscribed, new ListingSpec(
                "small-kitchen-appliances", ConditionChoice.Sealed, "Espresso Machine",
                "Sealed overstock. Ready to publish once the shop's subscription is active.",
                WarrantyType.ManufacturerWarranty, 12, 60.000m, null, 2, "kettle.png", null));

            // The listing build tracked a graph per listing; start the order scenarios clean.
            _db.ChangeTracker.Clear();

            // One order left in each lifecycle state, all pickup (every Phase-1 order is).
            //
            // Pending: reserved, waiting on the shop.
            await PlaceOrderAsync(buyerAId, kitchenCo, kettle, 1, BuyerAName, BuyerAPhone);

            // Confirmed: the shop accepted it; contact details are now exchanged.
            var confirmedOrder = await PlaceOrderAsync(buyerBId, kitchenCo, toaster, 1, BuyerBName, BuyerBPhone);
            Ok(await _orders.ConfirmAsync(kitchenCo.UserId, confirmedOrder, _ct), "confirm the confirmed demo order");

            // Ready for pickup: prepared, waiting for the buyer to collect.
            var readyOrder = await PlaceOrderAsync(buyerAId, kitchenCo, handMixer, 1, BuyerAName, BuyerAPhone);
            Ok(await _orders.ConfirmAsync(kitchenCo.UserId, readyOrder, _ct), "confirm the ready demo order");
            Ok(await _orders.MarkReadyForPickupAsync(kitchenCo.UserId, readyOrder, _ct), "ready the ready demo order");

            // Completed: collected and confirmed by the buyer — this is the order the review hangs on.
            var completedOrder = await PlaceOrderAsync(buyerBId, kitchenCo, uprightVacuum, 1, BuyerBName, BuyerBPhone);
            Ok(await _orders.ConfirmAsync(kitchenCo.UserId, completedOrder, _ct), "confirm the completed demo order");
            Ok(await _orders.MarkReadyForPickupAsync(kitchenCo.UserId, completedOrder, _ct), "ready the completed demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerBId, completedOrder, _ct), "buyer confirms receipt of the completed demo order");

            // Cancelled: the buyer changed their mind before the shop confirmed.
            var cancelledOrder = await PlaceOrderAsync(buyerAId, kitchenCo, steamIron, 1, BuyerAName, BuyerAPhone);
            Ok(await _orders.CancelMyOrderAsync(buyerAId, cancelledOrder, "Found the same iron locally.", _ct),
                "cancel the cancelled demo order");

            // No-show: prepared, then the buyer never collected and the shop recorded it.
            var noShowOrder = await PlaceOrderAsync(buyerBId, kitchenCo, blender, 1, BuyerBName, BuyerBPhone);
            Ok(await _orders.ConfirmAsync(kitchenCo.UserId, noShowOrder, _ct), "confirm the no-show demo order");
            Ok(await _orders.MarkReadyForPickupAsync(kitchenCo.UserId, noShowOrder, _ct), "ready the no-show demo order");
            Ok(await _orders.MarkNoShowAsync(kitchenCo.UserId, noShowOrder, "Buyer did not collect within the window.", _ct),
                "mark the no-show demo order");

            // A sold-out listing on the other shop: a buyer clears the last units.
            var soldOutOrder = await PlaceOrderAsync(buyerAId, petraTools, handheldVacuum, 4, BuyerAName, BuyerAPhone);
            Ok(await _orders.ConfirmAsync(petraTools.UserId, soldOutOrder, _ct), "confirm the sold-out demo order");
            Ok(await _orders.MarkReadyForPickupAsync(petraTools.UserId, soldOutOrder, _ct), "ready the sold-out demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerAId, soldOutOrder, _ct), "buyer confirms receipt of the sold-out demo order");

            // The review — the artifact InspectAsync keys "fully seeded" on.
            Ok(
                await _reviews.SubmitReviewAsync(buyerBId, new SubmitReviewInput(
                    completedOrder, 5,
                    "Vacuum was exactly as described, including the disclosed scuff. Quick, friendly pickup."), _ct),
                "submit the demo review");
        }

        // ---- Users & merchants -------------------------------------------------------

        private async Task<string> CreateUserAsync(string email, string firstName, string lastName, string? role = null)
        {
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
                throw Fail($"create user '{email}'", created.Errors.Select(e => e.Description));
            }

            if (role is not null)
            {
                var granted = await _users.AddToRoleAsync(user, role);
                if (!granted.Succeeded)
                {
                    throw Fail($"grant '{role}' to '{email}'", granted.Errors.Select(e => e.Description));
                }
            }

            return user.Id;
        }

        private async Task<DemoMerchant> CreateApprovedMerchantAsync(
            string email, string businessName, string contactEmail, string contactPhone, string adminId, string? planCode)
        {
            // No starter role: approving the verification grants the Merchant role, exactly as
            // it would for a real applicant.
            var (first, last) = SplitBusinessName(businessName);
            var userId = await CreateUserAsync(email, first, last);

            var profileId = OkValue(
                await _verification.SaveDraftAsync(
                    userId, new MerchantApplicationInput(businessName, contactEmail, contactPhone), _ct),
                $"save merchant draft for {businessName}");

            Ok(
                await _verification.AddDocumentAsync(userId, new AddVerificationDocumentInput(
                    MerchantVerificationDocumentType.CommercialRegistration,
                    new MemoryStream(DemoAssets.Pdf), "commercial-registration.pdf", "application/pdf", DemoAssets.Pdf.Length), _ct),
                $"attach verification document for {businessName}");
            Ok(await _verification.SubmitForReviewAsync(userId, _ct), $"submit {businessName} for verification");
            Ok(await _verification.ApproveAsync(adminId, profileId, _ct), $"approve {businessName}");

            if (planCode is not null)
            {
                // Verify first, then pay (BUSINESS-MODEL.md §7): the plan is chosen and activated
                // only after approval, exactly like a real merchant's path.
                var plans = await _subscriptions.GetAvailablePlansAsync(_ct);
                var plan = plans.SingleOrDefault(p => p.Code == planCode)
                    ?? throw Fail($"choose a plan for {businessName}", [$"No active subscription plan with code '{planCode}'."]);
                Ok(await _subscriptions.ChoosePlanAsync(userId, plan.Id, _ct), $"choose the {planCode} plan for {businessName}");
                Ok(
                    await _subscriptions.ActivateAsync(adminId, profileId, $"DEMO-{planCode.ToUpperInvariant()}", _ct),
                    $"activate the {planCode} subscription for {businessName}");
            }

            return new DemoMerchant(userId, profileId, businessName);
        }

        private async Task CreatePendingMerchantAsync(
            string email, string businessName, string contactEmail, string contactPhone)
        {
            var (first, last) = SplitBusinessName(businessName);
            var userId = await CreateUserAsync(email, first, last);
            OkValue(
                await _verification.SaveDraftAsync(
                    userId, new MerchantApplicationInput(businessName, contactEmail, contactPhone), _ct),
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
                $"add a pickup location for {merchant.BusinessName}");
        }

        // ---- Listings ----------------------------------------------------------------

        /// <summary>
        /// Publishes one listing through the merchant's one-page form (<see cref="IMerchantListingService.SaveListingAsync"/>)
        /// and approves it in the admin moderation queue — the same two steps a real listing goes
        /// through before it is public.
        /// </summary>
        private async Task<DemoListing> CreateListingAsync(DemoMerchant merchant, string adminId, ListingSpec spec)
        {
            var outcome = OkValue(
                await _listings.SaveListingAsync(merchant.UserId, null, await BuildSubmissionAsync(spec), _ct),
                $"save listing '{spec.Title}' for {merchant.BusinessName}");

            if (!outcome.Published)
            {
                var why = outcome.GateMessage
                    ?? string.Join("; ", outcome.Blockers.Select(b => $"{b.Field}: {b.Message}"));
                throw new InvalidOperationException(
                    $"Demo seed listing '{spec.Title}' for {merchant.BusinessName} did not publish: {why}");
            }

            Ok(
                await _moderation.ApproveAsync(adminId, outcome.ListingId, "Demo data: approved.", _ct),
                $"approve listing '{spec.Title}'");

            return await DescribeListingAsync(outcome.ListingId);
        }

        /// <summary>Saves a listing that is expected to stay a draft because the publish gate blocks it.</summary>
        private async Task CreateDraftListingAsync(DemoMerchant merchant, ListingSpec spec)
        {
            var outcome = OkValue(
                await _listings.SaveListingAsync(merchant.UserId, null, await BuildSubmissionAsync(spec), _ct),
                $"save draft listing '{spec.Title}' for {merchant.BusinessName}");

            if (outcome.Published)
            {
                throw new InvalidOperationException(
                    $"Demo seed expected '{spec.Title}' for {merchant.BusinessName} to stay a draft, but it published.");
            }
        }

        private async Task<ListingFormSubmission> BuildSubmissionAsync(ListingSpec spec)
        {
            var categoryId = await CategoryIdAsync(spec.CategorySlug);

            var productPhotos = new List<IncomingPhoto> { Photo(spec.ProductImage) };
            var defectPhotos = spec.DefectImage is { } defect
                ? new List<IncomingPhoto> { Photo(defect) }
                : [];

            IncomingEvidence? evidence = spec.OriginalPrice is null
                ? null
                : new IncomingEvidence(
                    ReferencePriceEvidenceType.Link, "https://example.com/retail-reference", null);

            return new ListingFormSubmission(
                spec.Title,
                spec.Description,
                categoryId,
                spec.Condition,
                AdditionalDiscountReasonIds: [],
                spec.WarrantyType,
                spec.WarrantyMonths,
                spec.Price,
                spec.OriginalPrice,
                spec.Quantity,
                productPhotos,
                defectPhotos,
                RemovedPhotoIds: [],
                evidence);
        }

        private static IncomingPhoto Photo(string fileName)
        {
            var bytes = DemoAssets.LoadImage(fileName);
            return new IncomingPhoto(new MemoryStream(bytes), fileName, "image/png", bytes.Length);
        }

        private async Task<DemoListing> DescribeListingAsync(Guid listingId)
        {
            var slug = await _db.Listings.AsNoTracking()
                .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync(_ct);
            var variantIds = await _db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).OrderBy(v => v.Sku).Select(v => v.Id).ToListAsync(_ct);
            return new DemoListing(listingId, slug, variantIds);
        }

        // ---- Orders ----------------------------------------------------------------

        private async Task<Guid> PlaceOrderAsync(
            string buyerId, DemoMerchant merchant, DemoListing listing, int quantity, string contactName, string contactPhone)
        {
            var settings = await _store.GetSettingsAsync(merchant.UserId, _ct);
            var locationId = settings.Locations.First(l => l.IsActive).Id;

            return OkValue(
                await _orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
                    [new OrderLineInput(listing.VariantIds[0], quantity)],
                    OrderFulfillmentType.Pickup, locationId, null, contactName, contactPhone, null), _ct),
                $"place a demo order for '{listing.Slug}'");
        }

        // ---- Reference-data lookups ------------------------------------------------

        private Task<Guid> CategoryIdAsync(string slug) =>
            _db.Categories.AsNoTracking().Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync(_ct);

        // ---- Helpers -------------------------------------------------------------

        /// <summary>
        /// A merchant account needs a person's name for Identity. The demo has no separate
        /// contact person, so the shop name stands in — first token as the first name, the rest
        /// as the surname.
        /// </summary>
        private static (string First, string Last) SplitBusinessName(string businessName)
        {
            var parts = businessName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (businessName, "Merchant");
        }

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

    /// <summary>One listing the demo seed publishes, in the vocabulary of the one-page merchant form.</summary>
    private sealed record ListingSpec(
        string CategorySlug,
        ConditionChoice Condition,
        string Title,
        string? Description,
        WarrantyType WarrantyType,
        int? WarrantyMonths,
        decimal Price,
        decimal? OriginalPrice,
        int Quantity,
        string ProductImage,
        string? DefectImage);

    /// <summary>
    /// Media fixtures for the Development-only demo seed. Product and defect photography is a
    /// set of small, original flat-illustration PNGs generated locally by
    /// <c>tools/demo-images/generate-demo-images.ps1</c> (System.Drawing/GDI+) — nothing is
    /// downloaded or hotlinked, so there is no licensing concern. Each file lives under
    /// <c>Data/Seed/Assets/Images</c> and is copied next to the built application (see the
    /// <c>Content</c> item in <c>Faed.Web.csproj</c>), so it is reachable from disk at seed
    /// time whether the app is run with <c>dotnet run</c> or from a built <c>bin</c> output.
    /// The verification-document PDF stays a tiny generated fixture: it is never shown to
    /// buyers, so it does not need to look realistic.
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
