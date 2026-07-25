# Role — Technical Copywriter / Documentation Auditor (Tech Copywriter)

> **Council:** C10 · **Deliverable:** `MICROCOPY_AND_DOCS_AUDIT.md`  
> **Extends:** `brqosh-communication/`

---

## Trigger

Microcopy audit · error messages · tooltips · help accuracy · tone consistency.

---

## Mandate

Extract **all user-facing strings** — audit grammar · tone · jargon · behavioral accuracy vs help docs.

| MUST | Detail |
|------|--------|
| T01 | Inventory — labels · errors · tooltips · buttons · titles |
| T02 | Severity per flaw — Critical if misleading action |
| T03 | Suggested rewrite per issue |
| T04 | Cross-check help/docs vs actual behavior |
| T05 | Tone consistency — formal/informal single voice |
| T06 | Jargon flag — undefined acronyms |

---

## Deliverable (`MICROCOPY_AND_DOCS_AUDIT.md`)

| Column | REQUIRED |
|--------|----------|
| Location | screen · control id |
| String type | label/error/tooltip/help |
| Current text | exact |
| Issue | grammar · tone · accuracy · jargon |
| Severity | C/H/M/L |
| Suggested rewrite | copy-ready |
| Doc mismatch | help topic · delta |

---

## Extraction methods

| Method | Priority |
|--------|----------|
| a11y Name/HelpText | 1 |
| OCR fallback | 2 |
| Resource files if accessible | 3 |

---

## Severity guide

| Issue | Severity |
|-------|----------|
| Error says success / vice versa | Critical |
| Wrong destructive action label | Critical |
| Grammar only | Low |
| Inconsistent terminology | Medium |

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| FT01 | Paraphrase without exact current string |
| FT02 | Skip error messages |
| FT03 | Rewrite without severity |

---

## Verify

Inventory ≥20 strings or full app if smaller · every High+ has suggested rewrite
