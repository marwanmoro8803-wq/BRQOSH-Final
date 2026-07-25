# Report Excellence — Findings-First QA Output

> **Leak:** Anthropic `code-review/report-findings-tool.md`  
> **Master report:** `AUDIT_REPORT.md` · per-role sections in P1

---

## Reporting law (Anthropic)

| Rule | Detail |
|------|--------|
| Findings-first | most-severe at top |
| Verified only | CONFIRMED or PLAUSIBLE — drop unverified |
| Max 32 | findings per report pass |
| Single channel | tool/report file — not duplicate prose dump |

---

## Severity (binding)

| Level | Icon | Criteria |
|-------|------|----------|
| Critical | RED | data loss · security · crash · silent failure · blocker |
| High | ORANGE | major UX break · WCAG AA fail · logic error |
| Medium | YELLOW | inconsistency · recoverable error UX |
| Low | GREEN | polish · copy · minor alignment |

---

## Required fields per finding

| Field | MUST |
|-------|------|
| `summary` | one sentence defect |
| `failure_scenario` | inputs/state → wrong outcome |
| `repro` | numbered steps · copy-pasteable |
| `evidence` | screenshot/log path |
| `principle` | WCAG · Nielsen · OWASP · heuristic id |
| `suggested_fix` | code or UX change — when known |

Optional: `file` + `line` when source mapped.

---

## Executive summary (AUDIT_REPORT.md top)

| # | Section |
|---|---------|
| 1 | Quality score 0–100 |
| 2 | Knowledge Compliance Score 0–100 |
| 3 | Go / no-go production |
| 4 | Critical blockers count |
| 5 | One-paragraph narrative |

---

## Master report sections (AUDIT_REPORT.md — L476–499)

| # | Section | Owner role |
|---|---------|------------|
| 1 | Executive Summary | S12 |
| 2 | Chronological Audit Diary | Manual QA · Beta |
| 3 | Learned Application Model (state graph) | QA Auto |
| 4 | Critical Architecture & Connectivity Gaps | QA Auto · Pen |
| 5 | UX & Accessibility Compliance | UX Auditor |
| 6 | Design Pathology & Visual Defects | UX Auditor |
| 7 | Empathetic Experience Report | Beta Tester |
| 8 | Performance & Memory Analysis | Perf Tester |
| 9 | Security & Penetration Findings | Pen Tester |
| 10 | I18n & L10n Test Report | L10n |
| 11 | Compatibility Test Matrix | Compatibility |
| 12 | Microcopy & Documentation Audit | Copywriter |
| 13 | Dependency & CVE Report | Pen · DevOps |
| 14 | DevOps Integration Guide | DevOps |
| 15 | Bug Tracker Summary | S12 |
| 16 | Code Deep Analysis Summary | Superintelligence |
| 17 | Self-Healer Action Log | Superintelligence |
| 18 | Blueprint Refactoring (fix blocks) | QA Auto · Pen |

---

## Progressive disclosure

```
Executive → severity table → expandable detail per finding → appendix evidence
```

Export: Markdown required · HTML/PDF P3 (launcher).

---

## One-click fix block format

```markdown
### Fix for FINDING-NNN
**Why:** ...
```language
// minimal patch
```
```

**MUST NOT** suggest fix without linking to finding ID.

---

## Integration

| System | Link |
|--------|------|
| Bug tracker | each finding → `BUG-XXXX.md` via bug-tracker-spec |
| Shura | SAVE audit → council on go/no-go claim |

**Leak grep:** `report-findings-tool.md` · `brqosh-review/code-review.md`
