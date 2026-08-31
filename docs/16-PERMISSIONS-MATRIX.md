# 16 — Permissions Matrix

Authorization is enforced server-side. UI visibility is not security.

Legend:
- ✅ allowed
- ❌ forbidden
- ⚠ conditional

| Action | Anonymous | Individual Buyer | Pending Merchant | Approved Merchant | Admin |
|---|---:|---:|---:|---:|---:|
| Browse Live listings | ✅ | ✅ | ✅ | ✅ | ✅ |
| View non-Live own listing | ❌ | ❌ | ⚠ own merchant drafts if account owns them | ✅ own | ✅ |
| Create merchant application | ❌ | ✅ | — | — | ❌ |
| Upload verification docs | ❌ | ⚠ during merchant application | ✅ own | ⚠ own re-verification | ✅ admin workflow only |
| Approve/reject merchant | ❌ | ❌ | ❌ | ❌ | ✅ |
| Create listing | ❌ | ❌ | ❌ | ✅ | ⚠ admin support only if explicitly built |
| Edit own listing | ❌ | ❌ | ❌ | ✅ | ⚠ moderation action, not merchant ownership |
| Moderate listing | ❌ | ❌ | ❌ | ❌ | ✅ |
| Adjust own stock | ❌ | ❌ | ❌ | ✅ | ⚠ support/admin action if explicitly enabled |
| Create B2C order | ❌ | ✅ | ⚠ if account also uses consumer buying flow | ⚠ if allowed as a consumer identity | ❌ |
| View another buyer's private order | ❌ | ❌ | ❌ | ❌ except selling merchant's own orders | ✅ |
| Manage selling merchant's B2C order | ❌ | ❌ | ❌ | ✅ own merchant only | ✅ monitoring/support |
| Submit B2B offer | ❌ | ❌ | ❌ | ✅ verified buyer merchant | ❌ |
| Counter/accept seller B2B offer | ❌ | ❌ | ❌ | ✅ selling merchant only | ❌ |
| View unrelated B2B negotiation | ❌ | ❌ | ❌ | ❌ | ✅ monitoring/support |
| File eligible dispute | ❌ | ✅ participant | ❌ | ✅ participant | ❌ |
| Resolve dispute | ❌ | ❌ | ❌ | ❌ | ✅ |
| Leave review | ❌ | ⚠ completed participant | ❌ | ⚠ completed participant if B2B review enabled | ❌ |
| View private verification document | ❌ | ❌ | ⚠ own only if product flow permits | ⚠ own only | ✅ authorized |
| Manage catalog reference data | ❌ | ❌ | ❌ | ❌ | ✅ |

## Notes

- A user may have more than one Identity role if future account behavior requires it; business actions still depend on verification and ownership.
- Do not infer permission from a route area alone.
- Every record-changing endpoint re-checks ownership/participation from the database.
