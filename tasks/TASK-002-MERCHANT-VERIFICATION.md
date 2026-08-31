# TASK-002 — Merchant Verification

## Objective
Implement merchant application, private document handling, admin approval/rejection, audit logging, and the `ApprovedMerchant` authorization policy.

## Deliverables
- `MerchantProfile`
- `MerchantVerificationDocument`
- merchant verification states
- merchant application screens
- protected file-storage abstraction + development implementation
- admin verification queue/detail
- approve/reject/suspend actions
- `AdminActionLog`
- authorization policy
- migration + tests

## Critical rules
- Verification files are private.
- User cannot self-assign Approved.
- Non-admin cannot access verification documents.
- Merchant role alone is insufficient to sell.
- Rejection reason is recorded.
- Admin action is audited.

## Exit criteria
- [ ] Buyer cannot access merchant-only workflow.
- [ ] Pending merchant cannot submit listings.
- [ ] Admin can approve/reject.
- [ ] Private document URL is not public.
- [ ] Unauthorized document request fails.
- [ ] Audit entry is persisted.
- [ ] Relevant tests pass.
