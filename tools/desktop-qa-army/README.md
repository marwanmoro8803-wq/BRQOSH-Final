# BRQOSH Desktop QA Army

Ten-role cognitive audit framework for **Brqosh.Desktop** (WPF · UIA · pywinauto).

## Roles

| # | Role | Module | Deliverable |
|---|------|--------|-------------|
| 1 | QA Auto | `audit_orchestrator.py` | `TEST_FRAMEWORK_ARCHITECTURE.md` |
| 2 | Manual QA | `human_navigator.py`, `chronicle_logger.py` | `EXPLORATORY_TEST_SESSIONS.md`, `CHRONICLE.md` |
| 3 | UX / A11y | `design_auditor.py`, `accessibility_auditor.py` | `UX_ACCESSIBILITY_COMPLIANCE_REPORT.md` |
| 4 | Pen Tester | `pentest_engine.py`, `dependency_scanner.py` | `PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md` |
| 5 | Beta Tester | `human_navigator.py` (chaos) | `BETA_TESTER_USER_JOURNEY_FEEDBACK.md` |
| 6 | Perf | `performance_profiler.py` | `PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md` |
| 7 | L10n | `l10n_i18n_tester.py` | `I18N_AND_L10N_TEST_REPORT.md` |
| 8 | DevOps | `run_audit.ps1`, CI YAML | `DEVOPS_INTEGRATION_GUIDE.md` |
| 9 | Compatibility | `compatibility_tester.py` | `COMPATIBILITY_TEST_MATRIX.md` |
| 10 | Copywriter | `copywrite_docs_auditor.py` | `MICROCOPY_AND_DOCS_AUDIT.md` |

Master report: **`audit_reports/AUDIT_REPORT.md`**

## Quick start (Windows)

```powershell
cd tools\desktop-qa-army
.\run_audit.ps1
# Single profile (faster smoke):
.\run_audit.ps1 --profile Retail
```

### Prerequisites

- .NET 9 SDK
- Python 3.11+
- **Tesseract OCR** (optional fallback): [GitHub releases](https://github.com/UB-Mannheim/tesseract/wiki)
- Credentials: `marwan` / `UiAudit9!` or `BRQOSH_TEST_USER` / `BRQOSH_TEST_PASSWORD`

### Do not run in parallel

Never run **Python QA Army** and **`tools/UiAudit`** (FlaUI) at the same time — UIA race → `0x8000FFFF`.

## Architecture

```
audit_orchestrator.py
  ├── Static: design, i18n, copy, deps, pentest battery
  └── Live per profile: seed → build → login → cognitive_engine (PageAuditCatalog)
        └── audit_reports/* deliverables
```

Complements C# `tools/UiAudit` for authoritative button/dialog/grid coverage.

## Exit codes

- `0` — within failure budget
- `1` — critical threshold exceeded (CI gate)

## External boundaries

- O-01 ZATCA OTP · O-05 ZAP · live SaaS API = **EXTERNAL** — see `docs/OPEN_ITEMS.md`
