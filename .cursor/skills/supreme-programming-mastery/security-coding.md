# Security Coding

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

> Fable: no malware/exploits even "for education"

## OWASP-minded checklist

- [ ] Input validated at boundary
- [ ] SQL parameterized / EF parameterized
- [ ] AuthN + AuthZ on every sensitive path
- [ ] Secrets in env/KeyVault — never committed
- [ ] Dependencies scanned (known CVEs)
- [ ] Error messages don't leak internals
- [ ] CSRF/XSS for web surfaces
- [ ] Rate limits on auth endpoints

## Confidence filter (review)

Report security findings only when ≥80% confidence — reduce noise.

## ERP / financial

- Audit log on mutations
- RBAC before admin DB
- No bypass posting without trail

## BRQOSH

`SecurityContext` bind · `SchemaSqlGuard` · `brqosh-review/security.md`
