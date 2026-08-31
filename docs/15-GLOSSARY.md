# 15 — Domain Glossary

Use these terms consistently in code, UI, tests, and documentation.

## Faed
The marketplace/platform.

## Merchant
A business account capable of selling only after verification approval.

## Individual Buyer
A non-selling consumer account.

## Verified Merchant
Merchant whose `VerificationStatus = Approved`.

## Listing
The merchant's public commercial presentation of one product concept.

Example:
> "Classic Cotton T-Shirt"

A Listing is not the authoritative stock unit.

## Listing Option
A dimension used to differentiate variants.

Examples:
- Size
- Color

## Listing Option Value
A selectable value under an option.

Examples:
- M
- L
- Black
- White

## Listing Variant / SKU
A concrete sellable combination with its own inventory.

Example:
> Black / Size M

## Available Quantity
Units not currently reserved or sold.

## Reserved Quantity
Units temporarily committed to an active B2C order or accepted B2B deal.

## Sold Quantity
Units consumed by completed transactions.

## Condition Grade
The product's physical state: A-D.

## Discount Reason
The commercial/operational reason the merchant is selling through Faed.

Examples:
- Overstock
- Past Season
- Packaging Damage

## Reference Price
A merchant-provided normal/previous/catalog price with provenance metadata. It is not automatically trusted.

## B2C
Merchant-to-individual transaction.

## B2B Negotiation
The offer/counter-offer conversation between two verified merchants.

## Offer Revision
One immutable proposal inside a B2B Negotiation.

## B2B Deal
The fulfillment record created only after an offer revision is accepted and inventory is successfully reserved.

## Offer Expiry
Deadline after which an active proposal can no longer be accepted.

## Reservation Expiry
Deadline after acceptance after which reserved inventory may be released if fulfillment does not progress according to policy.

## Listing Moderation
Admin review required before a listing/version becomes public in the validation MVP.

## Material Listing Edit
A change that can affect buyer understanding or commercial decision and therefore requires re-moderation.

Examples:
- identity;
- category;
- condition;
- discount reason;
- material defect disclosure;
- price/reference price;
- variant structure.

## Recovered Value
Completed sale value generated from inventory through Faed. It is derived from transaction data.


## Canonical code naming

Use these names consistently:

| Product term | Preferred code term |
|---|---|
| Buyer | `Buyer` |
| Merchant | `MerchantProfile` where merchant business profile is meant |
| Listing | `Listing` |
| Variant | `ListingVariant` |
| Condition | `ConditionGrade` |
| Discount reason | `DiscountReason` |
| B2C order | `Order` |
| B2B negotiation | `B2BNegotiation` |
| Offer revision | `B2BOfferRevision` |
| Accepted B2B transaction | `B2BDeal` |
| Moderation record | `ListingModeration` |
| Verification state | `MerchantVerificationStatus` |

Avoid legacy/ambiguous designs such as:
- `IndividualOrder` when the canonical aggregate is `Order`;
- one `B2BOffer` entity that mixes negotiation and fulfillment;
- free-text-only discount reason;
- authoritative listing-level inventory;
- public `BusinessDocumentUrl` for private verification evidence.

Repository/product working name: **Faed**.
