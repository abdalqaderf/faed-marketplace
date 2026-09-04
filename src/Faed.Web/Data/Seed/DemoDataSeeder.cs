using System.Text;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Ordering;
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
/// (docs/12-SEED-DATA.md, tasks/TASK-011-HARDENING-AND-DEMO.md).
///
/// <para>
/// Every merchant, listing, order, negotiation, deal, dispute and review it creates goes
/// through the <em>same</em> application services and the <em>same</em> production rules a
/// real request would. It never writes aggregates directly, never bypasses moderation,
/// authorization, price integrity or stock concurrency, and never relaxes a validation rule.
/// The only thing that is "demo-only" is <em>when</em> it runs — Development environment,
/// explicitly enabled, password supplied out-of-band (see <see cref="DemoDataOptions"/>).
/// </para>
///
/// <para>
/// <b>Reliability &amp; query pressure.</b> The scenario is built as one linear pass over a
/// single scope; every lookup is a projected <c>AsNoTracking</c> query (no full table is
/// loaded); the change tracker is cleared before the transactional scenarios; and the
/// context's command timeout is raised to five minutes so a query does not abort under the
/// brief SQL Server LocalDB starvation a full test run can cause.
/// </para>
///
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
    // Fixed, obviously-non-production identities (docs/12-SEED-DATA.md "Demo users").
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
            .AnyAsync(r => demoUserIds.Contains(r.ReviewerUserId) && r.OrderId != null && r.Rating == 5, cancellationToken);

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
        var variantIds = await db.ListingVariants
            .Where(v => listingIds.Contains(v.ListingId)).Select(v => v.Id).ToListAsync(cancellationToken);
        var orderIds = await db.Orders
            .Where(o => merchantIds.Contains(o.MerchantProfileId)).Select(o => o.Id).ToListAsync(cancellationToken);
        var dealIds = await db.B2BDeals
            .Where(d => merchantIds.Contains(d.SellingMerchantProfileId) || merchantIds.Contains(d.BuyingMerchantProfileId))
            .Select(d => d.Id).ToListAsync(cancellationToken);
        var negotiationIds = await db.B2BNegotiations
            .Where(n => merchantIds.Contains(n.SellingMerchantProfileId) || merchantIds.Contains(n.BuyingMerchantProfileId))
            .Select(n => n.Id).ToListAsync(cancellationToken);
        var disputeIds = await db.Disputes
            .Where(d => (d.OrderId != null && orderIds.Contains(d.OrderId.Value))
                        || (d.B2BDealId != null && dealIds.Contains(d.B2BDealId.Value)))
            .Select(d => d.Id).ToListAsync(cancellationToken);

        await DeleteAsync(db, db.DisputeEvidence.Where(e => disputeIds.Contains(e.DisputeId)), cancellationToken);
        await DeleteAsync(db, db.Disputes.Where(d => disputeIds.Contains(d.Id)), cancellationToken);
        await DeleteAsync(db, db.Reviews.Where(r => merchantIds.Contains(r.ReviewedMerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.B2BDeals.Where(d => dealIds.Contains(d.Id)), cancellationToken);
        await DeleteAsync(db, db.B2BNegotiations.Where(n => negotiationIds.Contains(n.Id)), cancellationToken);
        await DeleteAsync(db, db.Orders.Where(o => orderIds.Contains(o.Id)), cancellationToken);
        await DeleteAsync(db, db.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.MerchantDeliveryZones.Where(z => merchantIds.Contains(z.MerchantProfileId)), cancellationToken);
        await DeleteAsync(db, db.InventoryAdjustments.Where(a => variantIds.Contains(a.ListingVariantId)), cancellationToken);
        await DeleteAsync(db, db.Listings.Where(l => listingIds.Contains(l.Id)), cancellationToken);
        await DeleteAsync(db, db.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)), cancellationToken);
        await DeleteAsync(db, db.AdminActionLogs.Where(a => userIds.Contains(a.AdminUserId)), cancellationToken);

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
        private readonly IB2BNegotiationService _negotiations;
        private readonly IB2BDealService _deals;
        private readonly IDisputeService _disputes;
        private readonly IReviewService _reviews;
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
            _negotiations = sp.GetRequiredService<IB2BNegotiationService>();
            _deals = sp.GetRequiredService<IB2BDealService>();
            _disputes = sp.GetRequiredService<IDisputeService>();
            _reviews = sp.GetRequiredService<IReviewService>();

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
                MerchantAEmail, "Amman Threads", "hello@amman-threads.example", "+962 6 500 0001", adminId);
            var merchantB = await CreateApprovedMerchantAsync(
                MerchantBEmail, "Petra Footwear", "sales@petra-footwear.example", "+962 6 500 0002", adminId);
            await CreatePendingMerchantAsync(
                PendingMerchantEmail, "Rainbow Kids Wear", "info@rainbow-kids.example", "+962 6 500 0003");

            await ConfigureFulfillmentAsync(merchantA, "Amman Threads — Abdali", "12 Rafiq Al Hariri Ave", "Abdali");
            await ConfigureFulfillmentAsync(merchantB, "Petra Footwear — Sweifieh", "8 Wakalat St", "Sweifieh");

            var tshirt = await CreateTshirtListingAsync(merchantA, adminId);
            var handbag = await CreateHandbagListingAsync(merchantA, adminId);
            var sneakers = await CreateSneakersListingAsync(merchantB, adminId);
            var clearance = await CreateClearanceListingAsync(merchantB, adminId);

            // Drop everything the listing build tracked before the transactional scenarios so
            // the order/negotiation/deal services start against a clean change tracker.
            _db.ChangeTracker.Clear();

            // One active B2C order: placed by Buyer A, confirmed by the merchant.
            var activeOrderId = await PlaceOrderAsync(
                buyerAId, merchantA, [(tshirt.VariantIds[0], 1), (tshirt.VariantIds[1], 1)], "Buyer A", "+962 79 000 0001");
            Ok(await _orders.ConfirmAsync(merchantA.UserId, activeOrderId, _ct), "confirm active demo order");

            // One completed B2C order: fully fulfilled and confirmed by the buyer.
            var completedOrderId = await PlaceOrderAsync(
                buyerBId, merchantA, [(handbag.VariantIds[0], 1)], "Buyer B", "+962 79 000 0002");
            Ok(await _orders.ConfirmAsync(merchantA.UserId, completedOrderId, _ct), "confirm completed demo order");
            Ok(await _orders.MarkReadyForPickupAsync(merchantA.UserId, completedOrderId, _ct), "ready completed demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerBId, completedOrderId, _ct), "buyer confirms completed demo order");

            // One sold-out listing for public sold-out behaviour: a buyer clears the last units.
            var clearanceOrderId = await PlaceOrderAsync(
                buyerAId, merchantB, [(clearance.VariantIds[0], ClearanceOpeningQuantity)], "Buyer A", "+962 79 000 0001");
            Ok(await _orders.ConfirmAsync(merchantB.UserId, clearanceOrderId, _ct), "confirm clearance demo order");
            Ok(await _orders.MarkReadyForPickupAsync(merchantB.UserId, clearanceOrderId, _ct), "ready clearance demo order");
            Ok(await _orders.ConfirmReceiptAsync(buyerAId, clearanceOrderId, _ct), "buyer confirms clearance demo order");

            // One open B2B negotiation: Petra Footwear enquires about Amman Threads' wholesale tees.
            await StartNegotiationAsync(
                merchantB.UserId, tshirt.Slug, [(tshirt.VariantIds[0], 12)], 6.500m,
                "Interested in a wholesale lot of the black medium tees for our outlet.");

            // One counter-offer chain: Amman Threads offers on Petra's sneakers, Petra counters.
            var counterNegotiationId = await StartNegotiationAsync(
                merchantA.UserId, sneakers.Slug, [(sneakers.VariantIds[0], 12)], 16.000m,
                "Opening offer for a mixed pallet of the past-season sneakers.");
            Ok(
                await _negotiations.CounterOfferAsync(merchantB.UserId, counterNegotiationId, new CounterOfferInput(
                    [new B2BOfferLineInput(sneakers.VariantIds[0], 12)], 21.000m,
                    "We can do 21 JOD a pair at that quantity.", null), _ct),
                "counter demo negotiation");

            // One completed B2B deal: Amman Threads buys a lot of Petra's sneakers end to end.
            var dealNegotiationId = await StartNegotiationAsync(
                merchantA.UserId, sneakers.Slug, [(sneakers.VariantIds[1], 15)], 19.000m,
                "Firm order for 15 pairs, size 42, for our summer sale.");
            var dealId = OkValue(
                await _deals.AcceptOfferAsync(merchantB.UserId, dealNegotiationId, new AcceptOfferInput(B2BFulfillmentType.Pickup), _ct),
                "accept demo deal");
            Ok(await _deals.MarkReadyForPickupAsync(merchantB.UserId, dealId, _ct), "ready demo deal");
            Ok(await _deals.MarkDeliveredAsync(merchantB.UserId, dealId, _ct), "deliver demo deal");
            Ok(await _deals.CompleteAsync(merchantA.UserId, dealId, _ct), "complete demo deal");

            // One dispute: the buying merchant raises an issue on the completed deal; an admin
            // takes it under review (a full audited lifecycle example, still visible in the queue).
            var disputeId = OkValue(
                await _disputes.FileDisputeAsync(merchantA.UserId, new FileDisputeInput(
                    TrustTransactionType.B2BDeal, dealId, DisputeReasonCode.MissingItems,
                    "Two pairs were missing from the collected lot. Requesting a partial refund or replacement.",
                    []), _ct),
                "file demo dispute");
            Ok(await _disputes.StartReviewAsync(adminId, disputeId, _ct), "start review of demo dispute");

            // One review: the buyer leaves a positive review on the completed B2C order.
            Ok(
                await _reviews.SubmitReviewAsync(buyerBId, new SubmitReviewInput(
                    TrustTransactionType.B2COrder, completedOrderId, 5,
                    "Bag was exactly as described, including the disclosed corner scuff. Smooth pickup."), _ct),
                "submit demo review");
        }

        // ---- Users & merchants -------------------------------------------------------

        private async Task<string> CreateUserAsync(string email, string? role = null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
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
            string email, string businessName, string contactEmail, string contactPhone, string adminId)
        {
            // No starter role: approving the verification grants the Merchant role, exactly as
            // it would for a real applicant (an administrator cannot hold a merchant identity,
            // so these accounts get no other role — docs/16-PERMISSIONS-MATRIX.md).
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
            Ok(
                await _store.AddDeliveryZoneAsync(merchant.UserId, new MerchantDeliveryZoneInput(
                    "Amman — inside the ring road", 2.500m, 10.000m, "1–3 working days"), _ct),
                $"add delivery zone for {merchant.BusinessName}");
        }

        // ---- Listings ---------------------------------------------------------------

        private async Task<DemoListing> CreateTshirtListingAsync(DemoMerchant merchant, string adminId)
        {
            // docs/12-SEED-DATA.md Listing 2 — T-Shirt, Condition A, Overstock, Size M/L/XL ×
            // Colour Black/White, B2C + B2B.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("clothing"), null, await GradeIdAsync("A"),
                "Everyday Cotton Crew Tee (Overstock)",
                "End-of-run stock of our best-selling 180gsm combed-cotton crew tee. Brand-new with tags; " +
                "the only reason for the discount is that we over-ordered for the season.",
                null, 12.000m, 7.000m, 10, AllowB2C: true, AllowB2B: true, AllowMixedVariantB2B: true,
                "14-day size-exchange on unworn items.", null, "One tee, folded with tag.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create tee listing");
            var size = await AddOptionAsync(merchant.UserId, listingId, "Size", "M", "L", "XL");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black", "White");
            await AddVariantAsync(merchant.UserId, listingId, "TEE-BLK-M", [size["M"], colour["Black"]], 40);
            await AddVariantAsync(merchant.UserId, listingId, "TEE-WHT-L", [size["L"], colour["White"]], 25);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "tee-front.png", "Folded black crew tee, front view");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("Overstock")] }, _ct),
                "attach tee discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateHandbagListingAsync(DemoMerchant merchant, string adminId)
        {
            // docs/12-SEED-DATA.md Listing 3 — Handbag, Condition D, Display Item, visible
            // cosmetic-defect photo, B2C only (B2B disabled).
            var details = new ListingDetailsInput(
                await CategoryIdAsync("bags-accessories"), null, await GradeIdAsync("D"),
                "Structured Leather Tote — Display Unit",
                "Former window-display tote in full-grain leather. Structurally perfect; there is light " +
                "surface scuffing to one bottom corner from the display stand, shown in the defect photo.",
                null, 55.000m, null, null, AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
                "Sold as-is; no size exchange on clearance display units.", null, "Tote and dust bag.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create handbag listing");
            await AddVariantAsync(merchant.UserId, listingId, "TOTE-COGNAC", [], 3);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "tote-front.png", "Cognac leather tote, front view");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Defect, "tote-corner-scuff.png", "Close-up of light scuffing on the bottom corner");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("DisplayItem")] }, _ct),
                "attach handbag discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateSneakersListingAsync(DemoMerchant merchant, string adminId)
        {
            // docs/12-SEED-DATA.md Listing 1 — Sneakers, Condition B, Past Season + Packaging
            // Damage, Size 41/42/43 × Colour Black, B2C + B2B, MOQ 10.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("shoes"), null, await GradeIdAsync("B"),
                "Court Low Sneakers (Past Season)",
                "Last season's colourway of our court low. The shoes are brand-new and unworn; some boxes " +
                "are crushed or missing lids from warehouse handling, which is why they are discounted.",
                null, 45.000m, 22.000m, 10, AllowB2C: true, AllowB2B: true, AllowMixedVariantB2B: true,
                "14-day exchange on unworn pairs in any condition of box.", null, "One pair; box condition varies.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create sneakers listing");
            var size = await AddOptionAsync(merchant.UserId, listingId, "Size", "41", "42", "43");
            var colour = await AddOptionAsync(merchant.UserId, listingId, "Colour", "Black");
            await AddVariantAsync(merchant.UserId, listingId, "COURT-BLK-41", [size["41"], colour["Black"]], 30);
            await AddVariantAsync(merchant.UserId, listingId, "COURT-BLK-42", [size["42"], colour["Black"]], 30);
            await AddVariantAsync(merchant.UserId, listingId, "COURT-BLK-43", [size["43"], colour["Black"]], 20);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "court-low-pair.png", "Pair of black court low sneakers");
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Packaging, "court-low-box.png", "Example of a crushed shoe box");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("PastSeason"), await ReasonIdAsync("PackagingDamage")] }, _ct),
                "attach sneakers discount reasons");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

        private async Task<DemoListing> CreateClearanceListingAsync(DemoMerchant merchant, string adminId)
        {
            // docs/12-SEED-DATA.md Listing 4 — a listing that ends up sold out, for public
            // sold-out behaviour. Opens with a small stock a demo buyer then clears.
            var details = new ListingDetailsInput(
                await CategoryIdAsync("clothing"), null, await GradeIdAsync("C"),
                "Merino Half-Zip — Final Units",
                "Customer-returned but unworn merino half-zips from our winter range. Inspected and " +
                "re-tagged. Only a handful of units left.",
                null, 38.000m, null, null, AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
                "14-day size-exchange while stock lasts.", null, "One half-zip with tag.", null, []);

            var listingId = OkValue(await _listings.CreateAsync(merchant.UserId, details, _ct), "create clearance listing");
            await AddVariantAsync(merchant.UserId, listingId, "MERINO-HZ-M", [], ClearanceOpeningQuantity);
            await AddImageAsync(merchant.UserId, listingId, ListingMediaType.Product, "merino-half-zip.png", "Grey merino half-zip, flat lay");
            Ok(
                await _listings.UpdateDetailsAsync(merchant.UserId, listingId,
                    details with { DiscountReasonIds = [await ReasonIdAsync("CustomerReturn")] }, _ct),
                "attach clearance discount reason");
            await PublishAsync(merchant.UserId, adminId, listingId);

            return await DescribeListingAsync(listingId);
        }

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
            string userId, Guid listingId, ListingMediaType type, string fileName, string altText) =>
            Ok(
                await _listings.AddImageAsync(userId, listingId, new AddListingImageInput(
                    type, new MemoryStream(DemoAssets.Png), fileName, "image/png", DemoAssets.Png.Length, altText), _ct),
                $"add {type} image {fileName}");

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
                    OrderFulfillmentType.Pickup, locationId, null, null, contactName, contactPhone, null), _ct),
                "place demo order");
        }

        private async Task<Guid> StartNegotiationAsync(
            string buyingMerchantUserId, string listingSlug,
            IReadOnlyList<(Guid VariantId, int Quantity)> lines, decimal unitPrice, string message) =>
            OkValue(
                await _negotiations.StartNegotiationAsync(buyingMerchantUserId, new StartNegotiationInput(
                    listingSlug, [.. lines.Select(l => new B2BOfferLineInput(l.VariantId, l.Quantity))], unitPrice, message, null), _ct),
                "start demo negotiation");

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
    /// Tiny, inert media fixtures embedded for the Development-only demo seed. The PNG is a
    /// genuine 1×1 image and the PDF is a complete minimal document, so both pass the same
    /// fail-closed upload inspector a real merchant upload would
    /// (docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md). No secrets, no machine-specific paths.
    /// </summary>
    private static class DemoAssets
    {
        public static byte[] Png { get; } = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

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
