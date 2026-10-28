# Phase 7 security self-review — Application / Infrastructure / Api (2026-10-28)

Scope: SQL injection, authorization gaps, secrets in code, crypto misuse. Method: targeted source scan
(raw SQL, string-built commands, secret literals, hash/cipher/RNG APIs, TLS/XXE/deserialization switches)
plus a read of every `Brqosh.Api` endpoint and its middleware chain.

## Fixed (real findings)

| # | Finding | Fix |
|---|---|---|
| S7-1 | PBKDF2-HMAC-SHA256 at **100,000** iterations (OWASP 2023: 600,000) | `Pbkdf2Iterations = 600_000`; stored iteration count still honoured; `NeedsRehash` re-hashes on the next successful login (also covers legacy SHA-256) |
| S7-2 | Legacy unsalted SHA-256 hashes compared with `string.Equals` (timing leak) | `CryptographicOperations.FixedTimeEquals` |
| S7-3 | Corrupt `pbkdf2:` value in DB threw `FormatException` out of login / zero-length or `0`-iteration hash accepted shape | returns `false` |
| S7-4 | (payroll integrity, found while reviewing WPS) wage-file deductions omitted `AbsenceDeduction`, so basic+housing+other−deductions ≠ net for any absence | absence included in both the CSV and SIF exports |

Tests: `ThePasswordHashMeetsCurrentGuidanceTests`, `TheWpsSifFileFollowsTheMudadLayoutTests`.

## Checked, no change needed

- **SQL injection:** the only interpolated raw SQL is `DROP INDEX "{index}"` (names come from `pg_indexes` and pass `SchemaSqlGuard.ValidateTable`) and `CREATE DATABASE {DatabaseName}` (compile-time constants). All other raw SQL is parameterised/`FormattableString`.
- **API authz:** API key enforced in Production (`EnsureProductionApiKey`), constant-time compare, rate-limit *before* key check; tenant-bound keys override `X-Tenant-Id`; every `companyId` endpoint calls `RejectTenantCompanyAsync`, every `groupId` endpoint `TenantGuard.IsGroupAllowedAsync`; writes call `RejectAccountingWrite`; webhooks `RejectWebhookAdministration`; ZATCA signer endpoints use their own shared secret and fail closed when unset; idempotency cache keyed by tenant+role+user+path. `fx-closing-translation` is POST but computes only (never posts).
- **Secrets in code:** none found (no password/API-key literals in src).
- **Crypto:** `RandomNumberGenerator` for salts/OTPs; HMAC-SHA1 only in TOTP (RFC 6238 standard); no ECB/DES/MD5, no `BinaryFormatter`, no TLS-validation bypass, no DTD processing. `pg_dump` password passed via `PGPASSWORD` env of the child process (not argv).

## Residual / not changed

- Self-signed code-signing certificate does not establish publisher trust for customers (accepted: no purchases).
- `PGPASSWORD` env could be replaced by a temporary `PGPASSFILE`; low risk on a single-user desktop.
