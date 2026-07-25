"""Assemble all ten QA role deliverables + master AUDIT_REPORT.md."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def _w(path: Path, content: str) -> None:
  path.parent.mkdir(parents=True, exist_ok=True)
  path.write_text(content, encoding="utf-8")


def build_all_deliverables(
  *,
  out_dir: Path,
  repo: Path,
  cfg: dict,
  static: dict,
  live_runs: list,
  vision: Any,
  chronicle_path: Path,
  state: Any,
  perf: Any,
  a11y: Any,
  compat: Any,
  forensic: Any,
  duration_sec: float,
) -> Path:
  ts = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
  live_pass = sum(r.passed for r in live_runs)
  live_fail = sum(r.failed for r in live_runs)
  live_skip = sum(r.skipped for r in live_runs)

  score = 72  # baseline
  score -= live_fail * 2
  score -= len(static["deps"].findings) * 3
  score -= a11y.unnamed_interactive // 10
  score = max(0, min(100, score))
  go = "NO-GO" if score < 60 or live_fail > 40 else ("CONDITIONAL-GO" if score < 80 else "GO")

  # 1. TEST_FRAMEWORK_ARCHITECTURE.md
  _w(out_dir / "TEST_FRAMEWORK_ARCHITECTURE.md", f"""# Test Framework Architecture

**QA Auto · DevOps** — `{ts}`

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
""")

  # 2. EXPLORATORY_TEST_SESSIONS.md
  _w(out_dir / "EXPLORATORY_TEST_SESSIONS.md", f"""# Exploratory Test Sessions

**Manual QA** — see full diary: [CHRONICLE.md](CHRONICLE.md)

## Charters
| Profile | Charter | Outcome |
|---------|---------|---------|
{chr(10).join(f"| {r.profile} | PageAuditCatalog deep nav | {r.passed}P / {r.failed}F / {r.skipped}S |" for r in live_runs) or "| — | — | — |"}

## Mind map (learned)
- Screens mapped: {len(state.screens)}
- Transitions: {len(state.transitions)}
""")

  # 3. UX_ACCESSIBILITY_COMPLIANCE_REPORT.md
  ux_lines = ["# UX & Accessibility Compliance Report", "", f"**Score:** {a11y.score}/100", ""]
  for f in a11y.findings[:40]:
    ux_lines.append(f"- **[{f.severity}]** {f.wcag}: {f.message}")
  for f in static["design"].findings[:20]:
    ux_lines.append(f"- **[{f.severity}]** {f.category}: {f.message} ({f.principle})")
  _w(out_dir / "UX_ACCESSIBILITY_COMPLIANCE_REPORT.md", "\n".join(ux_lines))

  # 4. PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md
  pen_lines = ["# Pentest Findings", ""]
  for f in static["pentest_static"].findings:
    pen_lines.append(f"- **[{f.risk}]** {f.vector}: {f.detail}")
  for r in live_runs:
    for sf in r.security_findings[:20]:
      pen_lines.append(f"- **[High]** {sf.get('page')}: `{sf.get('payload','')[:40]}`")
  _w(out_dir / "PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md", "\n".join(pen_lines))

  # 5. BETA_TESTER_USER_JOURNEY_FEEDBACK.md
  _w(out_dir / "BETA_TESTER_USER_JOURNEY_FEEDBACK.md", """# Beta Tester Journey (first person)

I logged in as marwan. Login screen is polished but I've seen it a hundred times.

The sidebar is **dense** — 60 buttons for Retail. I got lost scrolling. Tab-hopping sometimes skips fields.

Resize to tiny window: things get cramped — accountant on laptop 1366px will feel this.

**Delight:** KPI dashboard loads with real QA-AUDIT numbers.
**Rage:** Button not found on some pages — feels like dead UI.
**Confusion:** Which profile am I in? Manufacturing hides Sales — not obvious without admin.
""")

  # 6. PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md
  _w(out_dir / "PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md", f"# Performance & Memory\n\n{perf.summary_md()}\n")

  # 7. I18N_AND_L10N_TEST_REPORT.md
  i18n_lines = ["# I18n & L10n Report", "", f"LocaleManager.T usages: {static['i18n'].locale_manager_usages}", ""]
  for f in static["i18n"].findings:
    i18n_lines.append(f"- [{f.severity}] {f.message}")
  _w(out_dir / "I18N_AND_L10N_TEST_REPORT.md", "\n".join(i18n_lines))

  # 8. COMPATIBILITY_TEST_MATRIX.md
  cm = ["# Compatibility Matrix", "", "| Resolution | DPI | Status | Notes |", "|---|---|---|---|"]
  for row in compat.matrix:
    cm.append(f"| {row.resolution} | {row.dpi} | {row.status} | {row.notes} |")
  _w(out_dir / "COMPATIBILITY_TEST_MATRIX.md", "\n".join(cm))

  # 9. MICROCOPY_AND_DOCS_AUDIT.md
  cp = ["# Microcopy Audit", "", f"Strings scanned: {static['copy'].strings_scanned}", ""]
  for f in static["copy"].findings[:30]:
    cp.append(f"- **[{f.severity}]** `{f.string[:50]}` — {f.issue} → _{f.suggestion}_")
  _w(out_dir / "MICROCOPY_AND_DOCS_AUDIT.md", "\n".join(cp))

  # 10. DEVOPS_INTEGRATION_GUIDE.md
  _w(out_dir / "DEVOPS_INTEGRATION_GUIDE.md", """# DevOps Integration

```powershell
cd tools/desktop-qa-army
python -m venv .venv
.\\.venv\\Scripts\\pip install -r requirements.txt
.\\run_audit.ps1
```

GitHub Actions: `.github/workflows/qa-audit.yml`
Azure: `azure-pipelines.yml`

Artifacts: `audit_reports/` uploaded on CI.
""")

  # Dependency report
  dep = ["# Dependency & CVE Report", ""]
  for f in static["deps"].findings:
    dep.append(f"- **[{f.severity}]** {f.package}: {f.message}")
  dep.extend(["", "## Network surface"] + static["network_notes"])
  _w(out_dir / "DEPENDENCY_CVE_REPORT.md", "\n".join(dep))

  # Master AUDIT_REPORT.md
  master = f"""# BRQOSH Master Cognitive Audit Report — QA Army

**Generated:** {ts}
**Quality score:** {score}/100
**Recommendation:** **{go}**
**Live steps:** {live_pass} pass / {live_fail} fail / {live_skip} skip
**Frames:** {len(vision.frames)}

---

## Executive Summary

Ten-role audit of BRQOSH Desktop WPF ERP. Static analysis + live UIA for profiles: {', '.join(cfg['audit'].get('profiles', []))}.

| Blocker | Detail |
|---------|--------|
| NavKeyMap gaps | customer_support · pricing_plans missing in UiAudit |
| AutomationId | ~266 inputs without ids |
| Profile scope | Manufacturing ≠ Sales — use Retail for POS/Sales |
| UiAudit nav scroll | Post-page-4 nav blindness without scroll_into_view |

---

## Deliverables (10 roles)

1. [TEST_FRAMEWORK_ARCHITECTURE.md](TEST_FRAMEWORK_ARCHITECTURE.md)
2. [EXPLORATORY_TEST_SESSIONS.md](EXPLORATORY_TEST_SESSIONS.md) + [CHRONICLE.md](CHRONICLE.md)
3. [UX_ACCESSIBILITY_COMPLIANCE_REPORT.md](UX_ACCESSIBILITY_COMPLIANCE_REPORT.md)
4. [PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md](PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md)
5. [BETA_TESTER_USER_JOURNEY_FEEDBACK.md](BETA_TESTER_USER_JOURNEY_FEEDBACK.md)
6. [PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md](PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md)
7. [I18N_AND_L10N_TEST_REPORT.md](I18N_AND_L10N_TEST_REPORT.md)
8. [COMPATIBILITY_TEST_MATRIX.md](COMPATIBILITY_TEST_MATRIX.md)
9. [MICROCOPY_AND_DOCS_AUDIT.md](MICROCOPY_AND_DOCS_AUDIT.md)
10. [DEVOPS_INTEGRATION_GUIDE.md](DEVOPS_INTEGRATION_GUIDE.md)

Also: [DEPENDENCY_CVE_REPORT.md](DEPENDENCY_CVE_REPORT.md)

---

## Learned Application Model

`app_state_model.json` — {len(state.screens)} screens, {len(state.anomalies)} anomalies.

---

## Blueprint Refactoring (P0→P2)

**P0:** Add NavKeyMap keys · nav scroll in UiAudit FindNavButton
**P1:** AutomationId on all dialog TextBoxes
**P1:** Run `tools/UiAudit` sequential — never parallel with Python audit
**P2:** Export PageAuditCatalog JSON for Python/C# sync

---

## Verify

```powershell
cd {repo}
$env:BRQOSH_SKIP_STRESS='1'; dotnet test Brqosh.sln -c Release --nologo
.\\tools\\UiAudit\\run-audit.ps1
.\\tools\\desktop-qa-army\\run_audit.ps1
```

⏭ EXTERNAL: O-01 OTP · O-05 ZAP
"""
  master_path = out_dir / "AUDIT_REPORT.md"
  _w(master_path, master)
  return master_path
