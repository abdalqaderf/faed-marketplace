# Domain Model

The entity/relationship reference for the EF Core schema in `src/Faed.Web/Models/Entities`
(plus `ApplicationUser` in `src/Faed.Web/Models/Identity`) and its configuration in
`src/Faed.Web/Data/Configurations`. There is no separate ERD file; this document is the
authoritative entity reference. It is regenerated from the code, not patched — last
regenerated in Phase 14 against migration `20260909112228_InitialCreate`.

**Count:** **17 aggregate roots** (16 `DbSet<>` properties on `ApplicationDbContext` plus
`ApplicationUser`) and **21 mapped EF types** — the 17 roots plus four owned/join types that
are configured but not exposed as a `DbSet`: `ListingDiscountReason`, `ListingOption`,
`ListingOptionValue`, `ListingVariantOptionValue`. Twenty files live in `Models/Entities/`;
`ApplicationUser` is the twenty-first mapped type.

Conventions: every key is a `Guid` `Id` unless stated (`ApplicationUser.Id` is the Identity
string key). Money is `decimal(18,3)` — JOD has three decimal places. Timestamps are UTC and
named `...AtUtc`. Rich entities keep collections private behind `IReadOnly` views and expose
behaviour methods; services orchestrate, they do not hold the rules.

---

## 1. Identity and merchant

### ApplicationUser  *(aggregate root — Identity)*
Extends `IdentityUser`. One role per user via ASP.NET Core Identity roles: `Buyer`,
`Merchant`, `Admin`. Role is never a hand-edited string property.

- `Id` (string) · `Email` · `PhoneNumber` (Identity)
- `FirstName` · `LastName` · `FullName` (computed)
- `CreatedAtUtc` · `IsActive` (default `true`)

### MerchantProfile  *(aggregate root)*
1:1 with `ApplicationUser` (`UserId`, unique, `OnDelete: Restrict`). Rich aggregate owning its
verification documents.

- `Id` · `UserId` · `BusinessName` · `PublicSlug` (unique)
- `ContactEmail?` · `ContactPhone?`
- `VerificationStatus` (`MerchantVerificationStatus`, indexed)
- `SubmittedAtUtc?` · `ReviewedAtUtc?` · `ReviewedByAdminId?` · `RejectionReason?`
- `CreatedAtUtc` · `UpdatedAtUtc` · `RowVersion` (`[Timestamp]` — two admins deciding at once)
- Navigation: `Documents` (owned, private `_documents`)
- Behaviour: `CanSell` (= approved), `IsEditable`, verification decision methods

### MerchantVerificationDocument  *(aggregate root)*
Uploaded business document. `MerchantProfileId` FK, `OnDelete: Cascade` from the profile.
No public URL is stored — only the private storage key.

- `Id` · `MerchantProfileId` · `DocumentType` (`MerchantVerificationDocumentType`)
- `StorageObjectKey` · `OriginalFileName` · `ContentType` · `SizeBytes`
- `UploadedAtUtc` · `IsActive`

### MerchantLocation  *(aggregate root)*
A merchant's pickup point. `MerchantProfileId` FK, `OnDelete: Cascade`.

- `Id` · `MerchantProfileId` · `Name` · `AddressLine` · `Area` · `City`
- `Latitude?` · `Longitude?` (double) · `PickupInstructions?` · `PickupHoursText?`
- `IsActive` · `CreatedAtUtc` · `UpdatedAtUtc`

---

## 2. Subscriptions

### SubscriptionPlan  *(aggregate root — seeded reference data)*
Admin-editable. Seeded: Basic 35/15, Standard 50/40, Pro 80/120.

- `Id` · `Code` (unique) · `Name` · `MonthlyPriceJod` `decimal(18,3)`
- `ActiveListingQuota` · `HasFeaturedPlacement` · `SortOrder` · `IsActive`

### MerchantSubscription  *(aggregate root)*
1:1 with `MerchantProfile` (`MerchantProfileId`, unique, `OnDelete: Restrict`). Plan FK
`OnDelete: Restrict`. Manual billing: an admin records a payment reference and activates
(`BUSINESS-MODEL.md` §6.4).

- `Id` · `MerchantProfileId` · `SubscriptionPlanId`
- `Status` (`SubscriptionStatus`) · `StartsAtUtc?` · `ExpiresAtUtc?`
- `PaymentReference?` · `ActivatedByAdminId?`
- `CreatedAtUtc` · `UpdatedAtUtc` · `RowVersion` (`[Timestamp]`)
- Behaviour: `CanPublish` (= `Active`), activate / renew (+1 month) / expire / cancel

---

## 3. Catalog and taxonomy

All three are seeded reference data, admin-editable, never physically deleted — deactivate to
hide from new listings while existing listings keep working.

### Category  *(aggregate root)*
Hierarchical (`ParentCategoryId?` self-FK, `OnDelete: Restrict`). Seeded root
`open-box-ex-display` with launch children `small-kitchen-appliances`, `home-cleaning`,
`power-tools`.

- `Id` · `ParentCategoryId?` · `Name` · `Slug` (unique) · `SortOrder` · `IsActive`
- Navigation: `Parent`, `Children` (private `_children`) · `IsRoot`

### ConditionGrade  *(aggregate root — reference table)*
Codes `A`–`D` (unique); names/descriptions are appliance copy (`CORE.md` §3.3).

- `Id` · `Code` · `Name` · `Description` · `SortOrder` · `IsActive`

### DiscountReason  *(aggregate root — reference table)*
Eight seeded reasons; the four used by the condition cards are `Overstock`,
`PackagingDamage`, `CustomerReturn`, `DisplayItem`.

- `Id` · `Code` (unique) · `Name` · `Description?` · `IsActive`

---

## 4. Listing aggregate

### Listing  *(aggregate root)*
Rich domain model. `MerchantProfileId`, `CategoryId`, `ConditionGradeId` FKs, all
`OnDelete: Restrict` (a listing referenced by an order is never physically deleted). Stock
totals are **not** authoritative fields — they are summed from variants.

- `Id` · `MerchantProfileId` · `CategoryId` · `ConditionGradeId`
- `Title` · `Slug` (unique) · `Description?` (nullable — optional last field on the form)
- `ReferencePrice?` · `RetailPrice?` `decimal(18,3)`
- `WarrantyType` (`WarrantyType`) · `WarrantyMonths?` (1–120)
- `ReturnPolicyText?` · `IncludedItemsText?` · `MissingItemsText?`
- `Status` (`ListingStatus`) · `SubmittedAtUtc?` · `PublishedAtUtc?`
- `HiddenByAdmin` · `HiddenBySubscriptionLapse` — two independent hide flags, both distinct
  from a merchant's own Pause, so renewal restores lapse-hidden listings automatically
- `CreatedAtUtc` · `UpdatedAtUtc` · `RowVersion` (`[Timestamp]`)
- Navigation (all private-backed `IReadOnly`): `Options`, `Variants`, `Media`,
  `DiscountReasons`, `ReferencePriceEvidence`, `Moderations`
- Behaviour: `SubmitForReview`, `Approve`, `Reject`, `DescribeSubmissionBlockers`,
  `SetVariantStock` (quantity-only edit never reopens moderation), `OpenModeration`,
  `AvailableUnits`, `PendingModeration`, `LatestModeration`

### ListingDiscountReason  *(join type — not a DbSet)*
Composite key `(ListingId, DiscountReasonId)`. `Listing` side `OnDelete: Cascade`,
`DiscountReason` side `OnDelete: Restrict`.

### ListingMedia  *(aggregate root)*
`ListingId` FK, `OnDelete: Cascade`.

- `Id` · `ListingId` · `MediaType` (`ListingMediaType`: `Product` · `Defect` · `Packaging`)
- `StorageObjectKey` · `OriginalFileName` · `ContentType` · `SizeBytes`
- `AltText?` · `SortOrder` · `CreatedAtUtc`

### ListingReferencePriceEvidence  *(aggregate root)*
Evidence for an original price. `ListingId` FK, `OnDelete: Cascade`.

- `Id` · `ListingId` · `EvidenceType` (`ReferencePriceEvidenceType`: `Photo` · `Link`)
- `ReferenceUrl?` · `StorageObjectKey?` · `OriginalFileName?` · `ContentType?` · `Note?`
- `CreatedAtUtc`

### ListingModeration  *(aggregate root — append-only)*
Every review decision is a new row; a resolved row is never updated or deleted. `ListingId`
FK, `OnDelete: Cascade`.

- `Id` · `ListingId` · `SubmittedByMerchantProfileId`
- `ReasonForReview` · `Status` (`ListingModerationStatus`: `Pending` · `Approved` · `Rejected`)
- `ReviewedByAdminId?` · `ReviewNote?` · `SubmittedAtUtc` · `ReviewedAtUtc?`
- `IsPending` · `Resolve(...)` on the open row only

---

## 5. Listing options and variants

Kept in the schema, hidden from every UI (`CORE.md` §7). Because an item is one physical
unit, the listing form creates exactly one variant with a generated SKU; merchants never see
the words "option", "variant" or "SKU".

### ListingOption  *(entity — not a DbSet)*
`ListingId` FK `OnDelete: Cascade`. Unique `(ListingId, Name)`.
- `Id` · `ListingId` · `Name` · `SortOrder` · `Values` (private `_values`)

### ListingOptionValue  *(entity — not a DbSet)*
`ListingOptionId` FK `OnDelete: Cascade`. Unique `(ListingOptionId, Value)`.
- `Id` · `ListingOptionId` · `Value` · `SortOrder`

### ListingVariant  *(aggregate root — the sellable SKU and the one authoritative stock record)*
`ListingId` FK `OnDelete: Cascade`. Unique `(ListingId, Sku)` and
`(ListingId, OptionCombinationKey)`.

- `Id` · `ListingId` · `Sku` · `OptionCombinationKey`
- `InitialQuantity` · `AvailableQuantity` · `ReservedQuantity` · `SoldQuantity` (all `>= 0`)
- `IsActive` · `CreatedAtUtc` · `UpdatedAtUtc`
- **`RowVersion` (`[Timestamp]`) — the core concurrency token: two reservations must never
  oversell one unit** (`CLAUDE.md` invariant 1)
- `IsSellable` (= active and `AvailableQuantity > 0`) · `OptionValues` (private)

### ListingVariantOptionValue  *(join type — not a DbSet)*
Composite key `(ListingVariantId, ListingOptionValueId)`. Variant side `OnDelete: Cascade`,
option-value side `OnDelete: NoAction` (breaks the multi-cascade path SQL Server rejects).

---

## 6. Transactions

### Order  *(aggregate root)*
Rich aggregate owning its items. `BuyerUserId` and `MerchantProfileId` FKs `OnDelete:
Restrict`; `MerchantLocationId?` FK `OnDelete: Restrict`. Terminal states deactivate — an
order is never physically deleted.

- `Id` · `Reference` (6 chars from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, unique)
- `BuyerUserId` · `MerchantProfileId`
- `Status` (`OrderStatus`) · `FulfillmentType` (`OrderFulfillmentType`) · `MerchantLocationId?`
- `FulfillmentSnapshot` · `DeliveryAddressText?` · `ContactName` · `ContactPhone` · `BuyerNote?`
- `Subtotal` · `Total` `decimal(18,3)`
- `ReservationExpiresAtUtc?` · `StatusReason?`
- `CreatedAtUtc` · `UpdatedAtUtc` · `ConfirmedAtUtc?` · `CancelledAtUtc?` · `CompletedAtUtc?`
- `RowVersion` (`[Timestamp]`)
- `Items` (private `_items`) · `HoldsReservation` · `IsTerminal` · `BuyerCanCancel` ·
  `MerchantCanCancel` · `TotalUnits`
- Every Phase-1 order is created as `Pickup`; the platform models no delivery fee, zone or
  address of its own.

### OrderItem  *(aggregate root — snapshot record)*
`OrderId` FK `OnDelete: Cascade`; `ListingId` and `ListingVariantId` FKs `OnDelete: Restrict`.
Unique `(OrderId, ListingVariantId)`. Every snapshot field is `private set` and captured at
construction — editing a listing never changes a past order (`CLAUDE.md` invariant 2).

- `Id` · `OrderId` · `ListingId` · `ListingVariantId` · `Quantity`
- `UnitPriceSnapshot` · `LineTotalSnapshot` `decimal(18,3)`
- `ListingTitleSnapshot` · `VariantSnapshot` · `ConditionGradeSnapshot` · `DiscountReasonSnapshot?`

### Review  *(aggregate root)*
One per completed order (`OrderId` unique). `ReviewedMerchantProfileId`, `OrderId`,
`ReviewerUserId` FKs all `OnDelete: Restrict`. No `B2BDealId` and no "exactly one reference"
constraint — a review is always tied to exactly one order.

- `Id` · `ReviewedMerchantProfileId` · `ReviewerUserId` · `OrderId`
- `Rating` (1–5) · `Comment?` · `CreatedAtUtc`

**Merchant response rate has no entity** — it is computed from `Order` statuses and
timestamps over a rolling 30-day window (`BUSINESS-MODEL.md` §8.4).

---

## 7. Enums

| Enum | Values |
|---|---|
| `MerchantVerificationStatus` | `Draft` · `PendingReview` · `Approved` · `Rejected` · `Suspended` |
| `MerchantVerificationDocumentType` | `CommercialRegistration` · `TaxRegistration` · `Other` |
| `SubscriptionStatus` | `PendingActivation` · `Active` · `Expired` · `Cancelled` |
| `ListingStatus` | `Draft` · `PendingReview` · `Live` · `Rejected` · `Hidden` · `SoldOut` · `Archived` |
| `ListingModerationStatus` | `Pending` · `Approved` · `Rejected` |
| `ListingMediaType` | `Product` · `Defect` · `Packaging` |
| `WarrantyType` | `None` · `ManufacturerWarranty` · `ShopWarranty` |
| `ReferencePriceEvidenceType` | `Photo` · `Link` |
| `OrderStatus` | `Pending` · `Confirmed` · `ReadyForPickup` · `OutForDelivery` · `Completed` · `Cancelled` · `NoShow` |
| `OrderFulfillmentType` | `Pickup` · `MerchantDelivery` |

---

## 8. Delete behaviour

Preservation over cascade for transactional history (`CLAUDE.md` invariant 3).

- **`Restrict`** — `Listing → MerchantProfile / Category / ConditionGrade`;
  `Order → Buyer / MerchantProfile / MerchantLocation`;
  `OrderItem → Listing / ListingVariant`; `Review → MerchantProfile / Order / Reviewer`;
  `MerchantProfile → ApplicationUser`; `MerchantSubscription → MerchantProfile / SubscriptionPlan`;
  `Category → parent Category`; `ListingDiscountReason → DiscountReason`.
- **`Cascade`** — within an aggregate only: `Listing → Media / DiscountReasons /
  ReferencePriceEvidence / Moderations / Options / Variants`; `ListingOption → Values`;
  `ListingVariant → OptionValues`; `Order → Items`; `MerchantProfile → Documents / Locations`.
- **`NoAction`** — `ListingVariantOptionValue → ListingOptionValue`, to break the second
  cascade path into option values (SQL Server rejects multiple cascade paths).

---

## 9. Indexes

- Unique: `Category(Slug)`, `ConditionGrade(Code)`, `DiscountReason(Code)`,
  `SubscriptionPlan(Code)`, `Listing(Slug)`, `ListingOption(ListingId, Name)`,
  `ListingOptionValue(ListingOptionId, Value)`, `ListingVariant(ListingId, Sku)`,
  `ListingVariant(ListingId, OptionCombinationKey)`, `MerchantProfile(UserId)`,
  `MerchantProfile(PublicSlug)`, `MerchantSubscription(MerchantProfileId)`,
  `Order(Reference)`, `OrderItem(OrderId, ListingVariantId)`, `Review(OrderId)`.
- Query: `Listing(Status, CategoryId, PublishedAtUtc)`, `Listing(MerchantProfileId, Status)`,
  `ListingVariant(ListingId, IsActive)`, `ListingMedia(ListingId, MediaType, SortOrder)`,
  `ListingModeration(Status, SubmittedAtUtc)`, `ListingModeration(ListingId)`,
  `ListingReferencePriceEvidence(ListingId)`, `Category(ParentCategoryId, SortOrder)`,
  `MerchantProfile(VerificationStatus)`, `MerchantLocation(MerchantProfileId, IsActive)`,
  `MerchantVerificationDocument(MerchantProfileId)`,
  `MerchantSubscription(Status, ExpiresAtUtc)`, `Order(BuyerUserId, CreatedAtUtc)`,
  `Order(MerchantProfileId, Status)`, `Order(Status, ReservationExpiresAtUtc)`,
  `Review(ReviewedMerchantProfileId, CreatedAtUtc)`.

---

## 10. Explicitly out of scope

The schema deliberately does not model payment transactions, escrow or wallets, balances or
commission invoices, shipping-provider entities, warehouses, auction bids, disputes,
B2B negotiations or deals, an admin action log, inventory adjustments, delivery zones, or
brands. Several of these existed before `faed-core` and were removed in Phases 2–6; the B2B
design is archived in `docs/B2B-DESIGN.md`.
