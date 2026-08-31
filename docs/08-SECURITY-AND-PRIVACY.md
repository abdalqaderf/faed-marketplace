# 08 — Security and Privacy

## 1. Authentication

Use ASP.NET Core Identity.

Require confirmed/valid account behavior according to implementation policy.

Roles:
- Buyer
- Merchant
- Admin

Merchant verification is a domain state, not an Identity role.

---

## 2. Authorization

Use server-side policies/ownership checks.

Examples:
- `ApprovedMerchant`
- `AdminOnly`
- listing owner;
- order participant;
- B2B participant.

Never authorize using hidden buttons alone.

---

## 3. Verification documents

Business verification documents are sensitive.

Requirements:
- private object storage;
- randomized object key;
- allowed content types;
- file-size limit;
- no executable content;
- authorized admin download/stream endpoint;
- audit admin access where practical;
- never render public storage URL.

---

## 4. Listing/dispute uploads

Validate:
- extension;
- content type;
- size;
- image dimensions where useful.

Generate server-side safe filenames/object keys.

Do not trust original file name.

---

## 5. CSRF / forms

Use ASP.NET Core antiforgery for state-changing MVC forms.

---

## 6. Mass assignment

Do not bind domain entities directly from requests.

Use dedicated input ViewModels.

Never allow client assignment of:
- MerchantId;
- BuyerId;
- status;
- verification state;
- approved flags;
- calculated totals;
- stock counters.

---

## 7. Price integrity

At checkout/offer acceptance:
- load current price from DB;
- calculate totals server-side;
- snapshot accepted values.

Never trust a hidden HTML price field.

---

## 8. Stock integrity

SQL Server concurrency + transaction.

Do not use distributed locks in MVP unless actual multi-node behavior requires it.

---

## 9. IDOR protection

Every authenticated detail/action endpoint checks participation or ownership.

Guessing another order ID must not reveal it.

---

## 10. XSS and free text

Razor encoding remains enabled.

Do not render merchant/customer HTML as raw content.

If rich text is ever introduced, sanitize explicitly.

---

## 11. Secrets

Use development user secrets and production environment variables/secret store.

Never commit:
- database passwords;
- storage keys;
- SMTP/API secrets;
- admin seed password.

---

## 12. Admin seed

If development seed creates an admin:
- password comes from environment/user secrets;
- seed only in Development or explicit bootstrap mode;
- never store a fixed production password in repository.

---

## 13. Auditability

Admin moderation and dispute actions are logged.

Important business transitions should have structured logs.

---

## 14. Data minimization

Collect only merchant/customer data required for current flows.

Do not collect national identifiers unless a verified business requirement exists.

---

## 15. Security testing priorities

- role/ownership access;
- unapproved merchant restrictions;
- direct URL access to private documents;
- file upload abuse;
- forged price;
- forged stock;
- forged status;
- duplicate review;
- concurrency;
- expired offers/deals.
