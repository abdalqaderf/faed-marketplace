# Domain Model

This is the conceptual entity/relationship model behind the EF Core schema in
`src/Faed.Web/Models/Entities` and `src/Faed.Web/Data/Configurations`. There is no separate
ERD diagram file in this repository; this document is the authoritative entity reference.

---

# 1. Identity and merchant verification

## ApplicationUser
Extends `IdentityUser`.

Fields:
- `Id`
- `Email`
- `PhoneNumber`
- `CreatedAtUtc`
- `IsActive`

Roles use ASP.NET Core Identity:
- `Buyer`
- `Merchant`
- `Admin`

Do not duplicate role as a hand-edited string property.

## MerchantProfile
1:1 with `ApplicationUser`.

Fields:
- `Id`
- `UserId`
- `BusinessName`
- `PublicSlug`
- `VerificationStatus`
- `SubmittedAtUtc`
- `ReviewedAtUtc`
- `ReviewedByAdminId`
- `RejectionReason`
- `CreatedAtUtc`
- `UpdatedAtUtc`

## MerchantVerificationDocument
- `Id`
- `MerchantProfileId`
- `DocumentType`
- `StorageObjectKey`
- `OriginalFileName`
- `ContentType`
- `SizeBytes`
- `UploadedAtUtc`
- `IsActive`

Do not store a public document URL.

## MerchantLocation
- `Id`
- `MerchantProfileId`
- `Name`
- `AddressLine`
- `Area`
- `City`
- `Latitude` nullable
- `Longitude` nullable
- `PickupInstructions`
- `PickupHoursText`
- `IsActive`

## MerchantDeliveryZone
- `Id`
- `MerchantProfileId`
- `Name`
- `DeliveryFee` decimal(18,3)
- `MinimumOrderValue` decimal(18,3) nullable
- `EstimatedDeliveryText`
- `IsActive`

---

# 2. Catalog and taxonomy

## Category
Hierarchical.

Fields:
- `Id`
- `ParentCategoryId` nullable
- `Name`
- `Slug`
- `IsActive`
- `SortOrder`

Seed hierarchy example:

```text
Fashion Overstock
├── Clothing
├── Shoes
└── Bags & Accessories
```

Lower-level categories can be added as data.

## ConditionGrade
Reference table:
- `Id`
- `Code` (`A`..`D`)
- `Name`
- `Description`
- `SortOrder`
- `IsActive`

## DiscountReason
Reference table:
- `Id`
- `Code`
- `Name`
- `Description`
- `IsActive`

## Brand
Optional controlled entity:
- `Id`
- `Name`
- `Slug`
- `IsActive`

Brand is optional in MVP unless category rules require it.

---

# 3. Listing aggregate

## Listing
Fields:
- `Id`
- `MerchantProfileId`
- `CategoryId`
- `BrandId` nullable
- `Title`
- `Slug`
- `Description`
- `ConditionGradeId`
- `ReferencePrice` decimal(18,3) nullable
- `RetailPrice` decimal(18,3) nullable
- `WholesaleIndicativeUnitPrice` decimal(18,3) nullable
- `WholesaleMinQuantity` nullable
- `AllowB2C`
- `AllowB2B`
- `AllowMixedVariantB2B`
- `ReturnPolicyText`
- `WarrantyText` nullable
- `IncludedItemsText` nullable
- `MissingItemsText` nullable
- `Status`
- `SubmittedAtUtc` nullable
- `PublishedAtUtc` nullable
- `CreatedAtUtc`
- `UpdatedAtUtc`

Do not keep stock totals as authoritative listing fields.

Listing-level totals may be calculated from variants.

## ListingDiscountReason
Many-to-many:
- `ListingId`
- `DiscountReasonId`

## ListingMedia
- `Id`
- `ListingId`
- `StorageObjectKey`
- `MediaType`
  - `Product`
  - `Defect`
  - `Packaging`
- `SortOrder`
- `AltText`
- `CreatedAtUtc`

## ListingReferencePriceEvidence
- `Id`
- `ListingId`
- `EvidenceType`
- `ReferenceUrl` nullable
- `StorageObjectKey` nullable
- `Note` nullable
- `CreatedAtUtc`

---

# 4. Generic listing options and variants

Use a Shopify-like option/variant model instead of hard-coded `Size` and `Color` columns.

## ListingOption
Example: `Size`, `Color`.

- `Id`
- `ListingId`
- `Name`
- `SortOrder`

## ListingOptionValue
Examples: `M`, `L`, `Black`, `White`.

- `Id`
- `ListingOptionId`
- `Value`
- `SortOrder`

## ListingVariant
This is the sellable SKU and authoritative stock record.

- `Id`
- `ListingId`
- `Sku`
- `InitialQuantity`
- `AvailableQuantity`
- `ReservedQuantity`
- `SoldQuantity`
- `IsActive`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `RowVersion` `[Timestamp]`

Recommended check constraints:
- quantities >= 0.

## ListingVariantOptionValue
Join:
- `ListingVariantId`
- `ListingOptionValueId`

Unique constraints must prevent duplicate option combinations for one listing.

## InventoryAdjustment
Audit stock corrections:
- `Id`
- `ListingVariantId`
- `ChangedByUserId`
- `AdjustmentType`
- `QuantityDelta`
- `Reason`
- `CreatedAtUtc`

---

# 5. Listing moderation

## ListingModeration
Preserve each review action/version context.

Fields:
- `Id`
- `ListingId`
- `SubmittedByMerchantId`
- `Status`
  - `Pending`
  - `Approved`
  - `Rejected`
- `ReviewedByAdminId`
- `ReviewNote`
- `SubmittedAtUtc`
- `ReviewedAtUtc`

The implementation may use listing timestamps/version hashes to know whether a material edit requires new moderation.

Do not lose rejection history.

---

# 6. B2C ordering

## Order
- `Id`
- `BuyerUserId`
- `MerchantProfileId`
- `Status`
- `FulfillmentType`
- `MerchantLocationId` nullable
- `DeliveryZoneId` nullable
- `DeliveryFeeSnapshot` decimal(18,3)
- `Subtotal` decimal(18,3)
- `Total` decimal(18,3)
- `ReservationExpiresAtUtc` nullable
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `CompletedAtUtc` nullable
- `RowVersion`

## OrderItem
- `Id`
- `OrderId`
- `ListingId`
- `ListingVariantId`
- `Quantity`
- `UnitPriceSnapshot` decimal(18,3)
- `LineTotalSnapshot` decimal(18,3)
- `ListingTitleSnapshot`
- `VariantSnapshot`
- `ConditionGradeSnapshot`
- `DiscountReasonSnapshot`

All OrderItems must belong to the order's merchant.

---

# 7. B2B negotiation

## B2BNegotiation
- `Id`
- `ListingId`
- `SellingMerchantProfileId`
- `BuyingMerchantProfileId`
- `Status`
- `CurrentRevisionNumber`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `RowVersion`

## B2BOfferRevision
Immutable proposal revision:
- `Id`
- `B2BNegotiationId`
- `RevisionNumber`
- `ProposedByMerchantProfileId`
- `ProposedUnitPrice` decimal(18,3)
- `ProposedTotal` decimal(18,3)
- `Message`
- `OfferExpiresAtUtc`
- `CreatedAtUtc`

Unique:
- negotiation + revision number.

## B2BOfferLine
- `Id`
- `B2BOfferRevisionId`
- `ListingVariantId`
- `Quantity`

The revision total quantity is the sum of lines.

---

# 8. B2B accepted deal

## B2BDeal
Created only after accepted proposal.

- `Id`
- `B2BNegotiationId`
- `AcceptedRevisionId`
- `SellingMerchantProfileId`
- `BuyingMerchantProfileId`
- `Status`
- `AcceptedUnitPriceSnapshot`
- `SubtotalSnapshot`
- `ShippingCostSnapshot` nullable
- `TotalSnapshot`
- `FulfillmentType`
- `ShipmentReference` nullable
- `ReservationExpiresAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `CompletedAtUtc` nullable
- `RowVersion`

## B2BDealLine
- `Id`
- `B2BDealId`
- `ListingVariantId`
- `Quantity`
- `UnitPriceSnapshot`
- `LineTotalSnapshot`
- `VariantSnapshot`

---

# 9. Disputes and reviews

## Dispute
- `Id`
- `OrderId` nullable
- `B2BDealId` nullable
- `RaisedByUserId`
- `ReasonCode`
- `Description`
- `Status`
- `AdminResolution`
- `ResolvedByAdminId` nullable
- `CreatedAtUtc`
- `ResolvedAtUtc` nullable

Constraint:
exactly one of `OrderId` or `B2BDealId` must be set.

## DisputeEvidence
- `Id`
- `DisputeId`
- `StorageObjectKey`
- `ContentType`
- `CreatedAtUtc`

## Review
- `Id`
- `ReviewedMerchantProfileId`
- `ReviewerUserId`
- `OrderId` nullable
- `B2BDealId` nullable
- `Rating`
- `Comment`
- `CreatedAtUtc`

Constraints:
- rating 1..5;
- exactly one transaction reference;
- one allowed review per reviewer/transaction.

---

# 10. Admin audit

## AdminActionLog
- `Id`
- `AdminUserId`
- `ActionType`
- `TargetType`
- `TargetId`
- `Notes`
- `CreatedAtUtc`

Audit at least:
- merchant approve/reject/suspend;
- listing approve/reject/hide;
- dispute resolution;
- account moderation.

---

# 11. Important indexes

Plan indexes for:
- `Category(Slug)` unique;
- `Listing(Slug)` unique;
- `Listing(Status, CategoryId, PublishedAtUtc)`;
- `Listing(MerchantProfileId, Status)`;
- `ListingVariant(ListingId, IsActive)`;
- `B2BNegotiation(SellingMerchantProfileId, Status)`;
- `B2BNegotiation(BuyingMerchantProfileId, Status)`;
- `Order(BuyerUserId, CreatedAtUtc)`;
- `Order(MerchantProfileId, Status)`;
- moderation/status queues;
- merchant public slug.

---

# 12. Delete behavior

Prefer preservation over cascading deletion for transactional history.

- Do not cascade-delete completed Orders, Deals, Reviews, Disputes, or audit logs.
- Archive/deactivate merchants/listings instead of physically deleting business history.
- Carefully configure FK delete behavior.

---

# 13. Explicitly out of scope

The current schema deliberately does not model:
- payment transactions;
- escrow wallets;
- shipping-provider entities;
- warehouses;
- auction bids;
- subscriptions;
- commission invoices;
- ERP sync tables.

These are out of scope for the MVP (see the README's "Known scope limitations").
