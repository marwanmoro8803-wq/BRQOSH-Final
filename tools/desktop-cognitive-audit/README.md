# BRQOSH Desktop Cognitive Audit

Human-like cognitive test framework for BRQOSH WPF desktop.

## Architecture

| Module | Role |
|--------|------|
| `audit_orchestrator.py` | Central cognitive loop · multi-profile |
| `vision_brain.py` | UIA-first perception · OCR fallback |
| `human_navigator.py` | Login · nav scroll · bored-user sabotage |
| `cognitive_engine.py` | PageAuditCatalog · valid→chaos payloads |
| `payload_factory.py` | Compliant · malicious · chaos strings |
| `forensic_handler.py` | Crash recovery · log tail · source hints |
| `chronicle_logger.py` | ORAR diary + affective annotations |
| `app_state_model.py` | Learning state machine JSON |
| `report_builder.py` | AUDIT_REPORT.md + SECURITY_SABOTAGE.md |

## Quick start

```powershell
cd C:\Users\qc\Projects\BRQOSH\tools\desktop-cognitive-audit

# Reuse omni-audit venv or create new
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# Full cognitive audit (Retail + Construction + Manufacturing)
python audit_orchestrator.py

# Single profile smoke
python audit_orchestrator.py --profile Retail
```

## Outputs

- `output/CHRONICLE.md` — full human diary
- `output/AUDIT_REPORT.md` — master report
- `output/SECURITY_SABOTAGE.md` — payload findings
- `output/app_state_model.json` — learned state machine
- `output/screenshots/frame_*.png` — vision captures

## Complements

- `tools/UiAudit` — FlaUI authoritative button/dialog audit
- `tools/desktop-omni-audit` — static ui_map + pywinauto v1

## Profiles

| Profile | Key modules |
|---------|-------------|
| Retail | POS · Sales · Inventory |
| Construction | Projects · BOQ · Billings |
| Manufacturing | BOM · Work orders · Inventory |

Run all three for maximum A→Z coverage.
