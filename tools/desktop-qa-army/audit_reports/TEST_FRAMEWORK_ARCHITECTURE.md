# Test Framework Architecture

**QA Auto · DevOps** — `2026-07-10 13:15 UTC`

## Stack
- **UI:** pywinauto UIA (Windows WPF)
- **Vision:** UIA-first → OCR fallback (pytesseract)
- **State:** `app_state_model.json` learning graph
- **Catalog:** `PageAuditCatalog.cs` parsed at runtime
- **Complement:** `tools/UiAudit` FlaUI (C# authoritative)

## Modules (`qa_army/`)
| Module | Roles |
|--------|-------|
| audit_orchestrator | QA Auto, DevOps |
| vision_brain, human_navigator, cognitive_engine | Manual, Beta |
| design_auditor, accessibility_auditor | UX |
| pentest_engine, dependency_scanner | Pen Tester |
| performance_profiler | Perf |
| l10n_i18n_tester | L10n |
| compatibility_tester | Compatibility |
| copywrite_docs_auditor | Tech Copywriter |
| forensic_handler, chronicle_logger | QA Auto |

## CI exit codes
- `0` — acceptable failure budget
- `1` — critical threshold exceeded
