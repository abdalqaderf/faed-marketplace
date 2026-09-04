# TASK-021 — Final Submission Audit

**Agent:** Codex  
**Mode:** REVIEW ONLY.

## Objective

Audit the cleaned repository exactly as it will be submitted.

## Required checks

1. Inspect from a clean checkout, not from cached `bin/obj`.
2. Confirm repository tree is clean and understandable.
3. Confirm no agent/skill/task/test files remain if they were approved for removal.
4. Confirm no stale references to those deleted files remain.
5. Scan for secrets, passwords, machine-specific paths, local databases, uploads, private storage, and generated IDE/build files.
6. Restore and Release-build the solution.
7. Confirm EF migrations are present and there are no pending model changes.
8. Apply migrations to a fresh SQL Server database.
9. Start the application.
10. Enable/run demo seed using the documented procedure.
11. Verify the important public/Buyer/Merchant/Admin routes.
12. Verify product images and realistic demo data render.
13. Verify README setup commands match the actual repository.
14. Check Git status is clean.
15. Check the retained GitHub workflow does not reference deleted files/projects.

## Output

Create `FINAL_SUBMISSION_AUDIT.md` with only:

- Overall status: `READY FOR SUBMISSION` or `NOT READY`;
- blockers, if any;
- non-blocking notes;
- exact verification commands/results;
- final repository tree;
- final checklist.

Do not modify source files.
