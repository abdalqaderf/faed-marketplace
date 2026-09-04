# TASK-017 — Demo & Runtime Review

**Agent:** Codex  
**Mode:** REVIEW ONLY.

## Objective

Review the application after fixes and realistic demo data have been added.

## Checks

1. Build and start the application.
2. Apply migrations to a clean development database.
3. Run demo seeding twice and confirm the second run is safe/idempotent.
4. Verify the storefront has realistic products and images.
5. Check the main role surfaces:
   - Anonymous;
   - Buyer/User;
   - Pending Merchant;
   - Approved Merchant;
   - Admin.
6. Verify main navigation and authorization behavior.
7. Verify core workflows are represented in demo data.
8. Check for broken image URLs, missing assets, exceptions, empty key pages, and obvious UI issues.
9. If browser automation is available, use it. If not, use HTTP/runtime inspection and clearly mark visual-only checks that still need a manual browser pass.

## Output

Create `FINAL_DEMO_AUDIT.md` with:
- PASS/FAIL;
- findings;
- pages/routes checked;
- account/role checks;
- data/media checks;
- any remaining manual checks.
