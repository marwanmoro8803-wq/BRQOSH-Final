# BRQOSH Desktop Omni-Audit

Python audit suite for the **BRQOSH WPF desktop** application. Complements the in-repo C# [`tools/UiAudit`](../UiAudit) (FlaUI) with:

- **Static telemetry** — XAML/code-behind → UI element map with handlers and payload types
- **Live UIA automation** — `pywinauto` (`backend="uia"`) on Windows
- **Vision** — per-step screenshots + optional `pytesseract` OCR
- **Payload injection** — SQLi, XSS, RTL overrides, huge blobs, numeric edges (10+ per field type)
- **Crisis loop** — capture failure, log, kill/restart app, restore last safe nav state
- **`AUDIT_REPORT.md`** — coverage map, failures, UI pathology notes, refactoring blueprints

## Prerequisites

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11** | WPF + UIA automation |
| **Python 3.10+** | 3.11 recommended |
| **.NET 9 SDK** | Builds/runs `Brqosh.Desktop` |
| **Tesseract OCR** (optional) | For `pytesseract` — [UB Mannheim build](https://github.com/UB-Mannheim/tesseract/wiki) + Arabic `ara` traineddata |

## Quick start

```powershell
cd C:\Users\qc\Projects\BRQOSH\tools\desktop-omni-audit

# 1. Virtual environment
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# 2. Static map only (no app launch)
python audit_orchestrator.py --static-only

# 3. Full live audit (seeds QA data, builds Desktop, navigates all pages)
$env:BRQOSH_TEST_USERNAME = "marwan"
$env:BRQOSH_TEST_PASSWORD = "UiAudit9!"
python audit_orchestrator.py

# Or use wrapper script
.\run-audit.ps1
```

**Outputs:**

- `output/ui_map.json` — static UI telemetry
- `output/screenshots/step_*.png` — semantic captures
- `output/AUDIT_REPORT.md` — master report
- `output/logs/crisis.jsonl` — crash/recovery events

## Configuration

Edit [`config.yaml`](config.yaml):

- `app.exe_candidates` — Desktop binary paths
- `credentials` — env vars `BRQOSH_TEST_USERNAME` / `BRQOSH_TEST_PASSWORD` (defaults match `tools/UiAudit`)
- `navigation_pages` — Arabic nav titles (aligned with `NavigationCatalog.cs`)
- `audit.max_nav_pages` — `0` = all pages; set `5` for smoke runs
- `audit.payload_injection` — disable for faster nav-only runs

## Module layout

| File | Role |
|------|------|
| `audit_orchestrator.py` | Main runner |
| `ui_map_builder.py` | XAML/CS parser → `ui_map.json` |
| `ui_navigator.py` | pywinauto login, nav, payloads |
| `vision_capture.py` | Screenshots + OCR |
| `payloads.py` | Edge-case / malicious strings |
| `crisis_handler.py` | Kill/restart/seed/build recovery |
| `report_builder.py` | `AUDIT_REPORT.md` assembler |
| `config.yaml` | All tunables |

## CLI options

```text
python audit_orchestrator.py --static-only      # map only
python audit_orchestrator.py --max-pages 10     # limit navigation
python audit_orchestrator.py --no-payloads      # skip injection
python audit_orchestrator.py --no-seed          # skip FullScenarioAudit
```

## Relationship to UiAudit (C#)

| Tool | Stack | Strength |
|------|-------|----------|
| `tools/UiAudit` | FlaUI / C# | Deep button actions per `PageAuditCatalog`, nightly CI |
| `tools/desktop-omni-audit` | Python / pywinauto | Payload fuzzing, OCR screenshots, static code map |

Run both for maximum coverage:

```powershell
.\tools\UiAudit\run-audit.ps1
.\tools\desktop-omni-audit\run-audit.ps1
```

## Troubleshooting

**Login window not found** — Build Desktop first: `dotnet build src/Brqosh.Desktop -c Release`

**Tesseract errors** — Set `automation.ocr_enabled: false` in `config.yaml` or install Tesseract and add to `PATH`

**Nav button not found** — Titles must match Arabic labels from `NavigationCatalog`; check locale (`LocaleManager`)

**Password change dialog** — Orchestrator handles first-login password change (same as UiAudit)

## Verify (in-repo gates)

```powershell
cd C:\Users\qc\Projects\BRQOSH
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet test Brqosh.sln -c Release --nologo
```

Live OTP / ZATCA / ZAP remain **⏭ EXTERNAL** — see `docs/OPEN_ITEMS.md`.
