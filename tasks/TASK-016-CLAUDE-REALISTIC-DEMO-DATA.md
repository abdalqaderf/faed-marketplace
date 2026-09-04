# TASK-016 — Realistic Demo Data & Media

**Agent:** Claude Code
**Mode:** IMPLEMENTATION

---

## Objective

Prepare the Faed application with a clean, realistic, presentation-ready Development database and product media.

Use the existing `DemoDataSeeder` and the current application services as the primary implementation path.

The goal is to make the application look like a real marketplace during demonstration while preserving the actual business rules, authorization rules, database schema, and application architecture.

Do not redesign the application or bypass its normal workflows just to populate data.

---

# 1. Read Progress and Prerequisites First

Before making any changes:

1. Read:

   `tasks/FINALIZATION_PROGRESS.md`

2. Confirm that `TASK-015` is completed or completed with non-blocking notes.

3. Read:

   `FINAL_RUNTIME_FIX_REPORT.md`

4. Read this task completely.

5. Inspect the current implementation of:

   * `DemoDataSeeder`
   * `ApplicationDbContext`
   * seed configuration
   * application startup
   * catalog seeders
   * Identity seeders
   * listing services
   * inventory services
   * order services
   * B2B services
   * dispute services
   * review services
   * file/media storage
   * upload validation

6. Treat the current repository and database schema as the source of truth.

Do not rely on previous chat history.

---

# 2. Database Safety Gate — Mandatory

Before deleting or resetting any data, identify exactly which SQL Server database the application is connected to.

The final demo-data rebuild is allowed **only for a confirmed local Development/demo database**.

## Never delete or reset:

* Production databases.
* Staging databases.
* Shared databases.
* Remote databases whose purpose is unclear.
* Integration-test databases unrelated to the Development demo.
* Any database that cannot be positively identified as disposable Development/demo data.

Before performing a destructive reset, verify:

1. `ASPNETCORE_ENVIRONMENT` is `Development`.
2. The active connection string is the intended Development connection.
3. The database name and SQL Server instance are known.
4. There is no evidence that the database contains production/shared data.

If any of these checks are uncertain:

**STOP and report the blocker. Do not delete data.**

---

# 3. Preserve Existing Data If Necessary

Before resetting the Development database, inspect whether it contains any manually entered data that may still be useful.

If useful data exists:

* preserve it with a local backup/export if practical;
* or clearly report what will be removed before proceeding.

Do not commit database backups, credentials, or private user data to Git.

If the database contains only disposable development/demo data, a backup is not required.

---

# 4. Build the Final Demo Database From a Clean State

The final demo should not be a mixture of old random data and new seed data.

Once the database has been confirmed as disposable Development/demo data, rebuild it cleanly.

Prefer this order:

1. Stop the running application if required.
2. Reset only the confirmed Development/demo database using the safest mechanism already supported by the project.
3. Recreate/apply the complete migration chain.
4. Run required base seeders:

   * Identity roles/admin setup;
   * catalog/reference data;
   * other required system seed data.
5. Run the improved `DemoDataSeeder`.
6. Start the application and verify the final data.

Prefer existing EF Core/project mechanisms instead of ad-hoc SQL deletion scripts.

Do not use uncontrolled `DELETE` statements against many tables unless the current project architecture genuinely requires it.

If the current `DemoDataSeeder` already contains safe rebuild/reset behavior, reuse and improve it rather than creating a second competing mechanism.

---

# 5. Preserve the Existing Demo Seeder Architecture

The project already contains a useful `DemoDataSeeder`.

It currently creates scenarios such as:

* Admin account.
* Approved merchants.
* Pending merchant.
* Buyer accounts.
* Listings.
* Listing variants.
* Inventory.
* B2C orders.
* B2B negotiations.
* B2B deal.
* Dispute.
* Review.

Preserve this general architecture.

Do not replace it with direct database inserts that bypass application rules.

Where practical, continue using the real application services so seeded data follows the same rules as normal user actions.

---

# 6. Idempotency

The final demo seeding mechanism must remain safe to rerun.

Running the seeder more than once must not create:

* duplicate users;
* duplicate merchants;
* duplicate listings;
* duplicate SKUs;
* duplicate orders;
* duplicate negotiations;
* duplicate reviews;
* inconsistent inventory.

Use deterministic identifiers, marker records, existing completion mechanisms, or another simple reliable approach already compatible with the project.

Also preserve recovery behavior if a previous demo seed was interrupted halfway through.

---

# 7. Development Only

Demo seeding must remain restricted to Development.

It must not automatically populate a Production database.

Keep or improve the existing safeguards that prevent demo seeding in non-Development environments.

If a configuration flag controls demo seeding, document it clearly.

---

# 8. Final Demo Accounts

Prepare a small and understandable set of demo users.

At minimum include:

### Admin

One Admin account that can demonstrate:

* merchant verification;
* listing moderation;
* disputes;
* catalog;
* order monitoring;
* B2B deal monitoring;
* reviews;
* audit logs.

### Approved Merchants

At least **2 approved merchants**.

They should have distinct identities and realistic store information.

Each approved merchant should have:

* business name;
* contact information;
* store/public slug;
* pickup location;
* delivery settings where supported;
* listings;
* inventory.

### Pending Merchant

At least **1 merchant awaiting verification** so the Admin verification workflow can be demonstrated.

### Buyers

At least **2 normal Buyer/User accounts**.

They should support demonstration of:

* checkout;
* orders;
* receipt confirmation;
* reviews;
* disputes.

Approved merchants may also act as buyers if that matches the current authorization model.

---

# 9. Merchant Store Data

Make the approved merchant profiles coherent rather than random.

Example expectations:

* realistic business names;
* realistic Jordanian contact/location data;
* consistent store descriptions if the current model supports them;
* pickup locations;
* delivery zones;
* reasonable delivery fees;
* reasonable minimum order values;
* understandable pickup/delivery instructions.

Do not add fields that do not exist in the current model.

---

# 10. Final Product Catalog

Prepare approximately **10–16 useful visible listings**.

This is enough to make:

* the homepage/store look populated;
* search meaningful;
* filters meaningful;
* pagination/filter behavior demonstrable;
* multiple merchants visible.

Do not create hundreds of unnecessary records.

Use only categories and business concepts actually supported by the current project.

---

# 11. Listing Quality

Each product should have realistic and internally consistent data.

Include:

* title;
* description;
* merchant;
* category;
* brand when applicable;
* condition grade;
* discount reason;
* reference price where appropriate;
* retail price;
* B2C/B2B availability;
* variants;
* SKU;
* quantities;
* included/missing items where relevant;
* reasonable policy text where the current fields support it.

Avoid obviously generated placeholder text such as:

* `Product 1`
* `Test Listing`
* `Lorem ipsum`
* `Sample description`

The content should look suitable for a student marketplace demo.

---

# 12. Product Variety

Create enough variety to demonstrate the implemented system.

Where supported, include examples such as:

* different condition grades;
* different discount reasons;
* different brands;
* different sizes;
* different colors;
* different price ranges;
* different merchants;
* B2C-only products;
* B2C + B2B products;
* multiple-variant products;
* simple single-variant products.

Include at least:

* one low-stock item;
* one sold-out item;
* one listing with multiple variants;
* one listing suitable for B2B negotiation.

Do not invent unsupported product behavior.

---

# 13. Product Images — Required

The current tiny `1x1 PNG` seed fixtures are not suitable for the final presentation.

Replace them with proper local product media.

## Requirements

* Images must render correctly in the current application.
* Use formats accepted by the real upload/media validation.
* Keep images reasonably optimized.
* Avoid extremely large files.
* Do not use broken URLs.
* Do not hotlink remote images.
* Store the demo images in a reproducible location compatible with the existing file-storage implementation.
* Use meaningful filenames.

Where useful, provide more than one image for selected listings so media-gallery behavior can be demonstrated.

---

# 14. Image Source Rules

Use one of the following approaches, in this order of preference:

1. User-provided product images.
2. Images generated specifically for the demo, if image-generation capability is available.
3. Properly licensed/openly usable images downloaded and stored locally, if web access is available.

If external images are used:

* do not hotlink;
* save them locally;
* keep a small record of their source/license if required.

Do not use copyrighted commercial product photography with unclear usage rights when a safer alternative is available.

If the environment cannot obtain suitable images, do not fake success.

Instead:

* prepare the complete seed-data implementation;
* create the expected local asset structure;
* document exactly which image files remain required.

---

# 15. Listing Moderation State

Seed listings in useful states where appropriate.

The final public catalog should contain approved/live listings so the public storefront is populated.

Also keep at least one moderation scenario if it helps demonstrate the Admin workflow, for example:

* Submitted/Pending listing.

Do not expose an unapproved listing publicly if the application normally prevents that.

---

# 16. Inventory

Inventory must remain consistent with the application’s implemented rules.

Do not manually fake values such as:

* available quantity;
* reserved quantity;
* sold quantity.

Use the real inventory/service behavior where possible.

Create useful examples of:

* available stock;
* low stock;
* sold-out stock;
* inventory adjustments.

Ensure seeded order/deal scenarios do not leave impossible stock values.

---

# 17. B2C Demo Scenarios

Preserve or create enough B2C scenarios to demonstrate the system.

At minimum include:

### Scenario A — Active Order

An order that is currently in an active fulfillment state.

### Scenario B — Completed Order

A completed B2C order that can support:

* review history;
* completed transaction display.

### Scenario C — Another useful lifecycle state

If supported cleanly, include one of:

* cancelled;
* ready for pickup;
* out for delivery.

Do not seed every possible state just for quantity.

---

# 18. B2B Demo Scenarios

Preserve or create meaningful B2B scenarios.

At minimum include:

### Open Negotiation

A merchant-to-merchant negotiation that can still be viewed or acted upon.

### Revision History

At least one negotiation should demonstrate:

* original offer;
* counter-offer/revision.

### Completed/Accepted Deal

At least one B2B deal should exist with:

* accepted revision;
* deal lines;
* realistic quantities;
* realistic price;
* fulfillment information.

Use the existing application services and B2B business rules.

---

# 19. Review Scenario

Include at least one valid Review tied to a completed eligible transaction.

It should contain:

* reasonable rating;
* short realistic comment;
* correct reviewer;
* correct reviewed merchant.

Do not create duplicate reviews for the same transaction.

---

# 20. Dispute Scenario

Include at least one realistic Dispute that demonstrates the trust workflow.

It should be tied to exactly one valid transaction type according to the current database constraints:

* B2C Order

or

* B2B Deal.

Use a believable reason and description.

If evidence files are included, use safe local demo files accepted by the current validation.

---

# 21. Admin Audit Data

Where the application naturally creates `AdminActionLogs`, allow useful seed/demo workflows to create realistic audit entries.

Do not manually fabricate a huge audit history.

A few useful records are sufficient to demonstrate the feature.

---

# 22. Do Not Bypass Business Rules

This is mandatory.

Do not populate the database in a way that creates records impossible to produce through the application.

Prefer:

```text
Seeder
  ↓
Application Service
  ↓
Domain Rules
  ↓
EF Core
  ↓
SQL Server
```

rather than:

```text
Seeder
  ↓
Direct arbitrary SQL inserts
```

Direct EF setup may still be appropriate for reference/bootstrap data where that is already how the project works.

---

# 23. Do Not Change the Database Schema Unless Necessary

This task is about demo data and media.

Do not introduce a migration just to make demo data easier.

If the current schema cannot represent a requested demo scenario, use a scenario the schema actually supports.

If a genuine schema defect is discovered, stop and document it rather than silently expanding TASK-016.

---

# 24. Performance

The demo seed should complete in a reasonable amount of time.

TASK-015 already addressed expensive aggregate-loading paths.

While implementing this task:

* avoid accidentally introducing N+1 query behavior;
* avoid repeatedly loading the full catalog for every listing;
* reuse resolved reference data when appropriate;
* keep the number of demo records reasonable.

Do not perform broad performance refactoring unless a new blocker directly prevents successful seeding.

---

# 25. Demo Seed Configuration

Make the Development setup simple and explicit.

Document:

* environment required;
* configuration flag required, if any;
* database connection requirements;
* how to apply migrations;
* how to run demo seeding;
* how to rebuild/reset the demo database safely.

Do not store a real password in source control.

---

# 26. Verification — Mandatory

After implementation, verify the final result.

## Database

Confirm:

* the Development/demo database is recreated cleanly;
* all migrations apply;
* base seed data exists;
* final demo data exists;
* no obvious duplicates exist;
* the seeder can be run again safely.

## Application Startup

Start the application in Development.

Confirm startup finishes without unhandled exceptions.

## Public Experience

Verify:

* homepage loads;
* shop loads;
* listings appear;
* product images render;
* listing details load;
* merchant storefronts load;
* search/filter data is meaningful.

## Buyer

Verify a Buyer can access the expected pages and that demo orders exist.

## Merchant

Verify:

* approved merchant dashboard/pages contain useful data;
* listings and inventory exist;
* B2C orders are visible;
* B2B scenarios are visible;
* reviews/analytics have meaningful demo data where supported.

## Admin

Verify:

* merchant verification has a useful pending case;
* listing moderation has useful data if intentionally seeded;
* Orders/Deals/Disputes/Reviews contain demonstrable records.

---

# 27. Idempotency Verification

Run the demo seed at least twice.

The second run must not create duplicate demo content or corrupt existing seeded state.

If the implemented design intentionally rebuilds the entire demo dataset instead of performing a no-op second run, verify that the resulting dataset remains deterministic and consistent.

Document which behavior is implemented.

---

# 28. Do Not Run Unnecessary Long Verification

Do not repeatedly rerun the entire test suite unless the implementation changes production behavior that requires it.

Use focused verification for the demo-data changes.

A Release build and the required runtime/database checks are mandatory.

If existing targeted seed/integration tests cover the changed seeder, run them.

Avoid wasting time on repeated full-suite executions after an already validated TASK-015 unless a regression indicates they are necessary.

---

# 29. Required Report

Create:

`DEMO_DATA_REPORT.md`

The report must contain:

## A. Database Reset

* database/environment used;
* how the Development database was safely reset;
* whether any backup was required;
* migration result.

Do not include passwords.

## B. Demo Accounts

List:

* account role;
* email/username;
* purpose.

If all demo accounts share a Development-only password, document how the user can obtain/configure it without committing a secret.

## C. Merchants

List the demo merchants and their states:

* Approved.
* Pending.

## D. Listings

Provide a concise catalog summary:

* total demo listings;
* merchants represented;
* categories;
* B2C/B2B mix;
* important variants/states.

## E. Images

Document:

* where image assets are stored;
* approximate number of images;
* whether all listing images render;
* source/license notes where relevant.

## F. Transaction Scenarios

Document the main:

* B2C scenarios;
* B2B scenarios;
* dispute;
* review.

## G. Verification Results

Record:

* build;
* migrations;
* startup;
* seeder first run;
* seeder second run/idempotency;
* key pages/routes checked.

## H. Remaining Notes

Only include genuine remaining non-blocking limitations.

---

# 30. Update the Progress Tracker

Before finishing, update:

`tasks/FINALIZATION_PROGRESS.md`

Set the TASK-016 completion record with:

* Date.
* Result.
* Summary.
* Demo users/roles prepared.
* Listings/media prepared.
* Transactions/scenarios prepared.
* Verification.
* Files created/changed.
* Blockers/notes.

If TASK-016 is successfully completed, advance the tracker to:

```text
Current Task: TASK-017
Next Agent: Codex
```

Set the next action to review the populated application and demo data.

Do not start TASK-017.

---

# Definition of Done

TASK-016 is complete only when:

1. The correct Development/demo database was positively identified before reset.
2. No Production/shared database was touched.
3. The Development demo database was rebuilt from a clean state.
4. All migrations applied successfully.
5. The existing DemoDataSeeder architecture was preserved/improved.
6. Demo seeding remains Development-only.
7. Demo seeding is deterministic/idempotent or safely rebuildable.
8. Demo accounts are present.
9. At least two approved merchants and one pending merchant are available.
10. Buyers are available.
11. The storefront contains approximately 10–16 realistic listings.
12. Products include meaningful variants, prices, stock, conditions, and discount reasons.
13. Real local product images replace the current 1×1 fixtures.
14. B2C scenarios are present.
15. B2B negotiation/deal scenarios are present.
16. A review scenario is present.
17. A dispute scenario is present.
18. Product images render correctly.
19. The application starts successfully.
20. Key public/Buyer/Merchant/Admin pages have useful demo data.
21. `DEMO_DATA_REPORT.md` is complete.
22. `tasks/FINALIZATION_PROGRESS.md` is updated.
23. The tracker is handed off to `TASK-017 / Codex`.