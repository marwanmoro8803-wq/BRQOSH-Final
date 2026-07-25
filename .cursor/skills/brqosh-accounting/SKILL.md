---
name: brqosh-accounting
description: >-
  Saudi ERP accounting: double-entry, posting, Golden tests, ZATCA logic,
  audit trail, SOCPA constraints. Use for journals, invoices, fiscal periods,
  tax, treasury, or financial reports in BRQOSH.
---

# BRQOSH Accounting

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Non-negotiable

- Debit = credit; every posting has audit trail
- Golden 8/8, FSA 13/13 before claiming financial closure
- ZATCA in-repo != production OTP (external)

## Order

Correctness of entry -> Golden -> UI

## Reference

[reference.md](reference.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-FINANCE · **Layer:** L2 domain

## Default path

```
SKILL → reference.md → journal correctness → Golden 8/8 → UI (last)
```

---

### `SKILL.md` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | journals · invoices · ZATCA · fiscal periods · posting |
| **SKIP** | Never on financial BRQOSH work |
| **NEXT** | reference.md |
| **EXIT** | Load order: correctness before UI |
| **ANTI** | UI-first · false ZATCA OTP closure |
| **VERIFY** | State non-negotiables before implementation |
| **TIME** | 20s |

---

### `reference.md` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | posting details · Golden · FSA · audit trail |
| **SKIP** | Trivial question answered from SKILL |
| **EXIT** | Golden 8/8 · FSA 13/13 or documented gap |
| **ANTI** | Claim financial closure without Golden |
| **VERIFY** | dotnet test financial suite cited |
| **TIME** | 90s |

## Scenarios

| Signal | Path | VERIFY |
|--------|------|--------|
| New journal | reference → engineering | Golden pass |
| ZATCA production OTP | — | external dist/evidence |
