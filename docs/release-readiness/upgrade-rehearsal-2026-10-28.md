# Legacy database upgrade rehearsal — 2026-10-28 (box, local throwaway PostgreSQL only)

Base: `acct/phase7` rebased onto PC head `c6e2306c4` (bundle 17:24) + licence-agreement migration.

1. Created DB `upg3` with **1.0.32** binaries (`b6d22f0d8`): 78 migrations, 100 accounts, no `license_activations`.
2. Seeded with 1.0.32 code: 120 posted journal entries (seeded RNG) through `JournalPostingService`.
3. Started current code on the same DB (`DatabaseInitializer.InitializeAsync`).

| Check | Result |
|---|---|
| Migrations | 78 → **90 applied, 0 pending** |
| New chart accounts | 100 → 109 (5221, 2118, 5109, 12071, 12076 present); 1113 renamed to "Contract Assets (Unbilled Work)" |
| New tables | `license_activations` ✔, `license_agreements` ✔ + immutability trigger ✔ |
| Ledger fingerprint (count · Σdebit · Σcredit · md5 of every line) | **identical** to a fresh 1.0.32 reference DB (`upg4`): `240 · 287,611.16 · 287,611.16 · 8c8aa30b…2793`, 120 entries |

Harness (not in repo): `/tmp/acct/upg/{old,seed,new}` on the box.
