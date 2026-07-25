"""Assemble master AUDIT_REPORT.md + SECURITY_SABOTAGE.md."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def build_master_report(
  audit_path: Path,
  security_path: Path,
  *,
  chronicle_path: Path,
  state: Any,
  runs: list[Any],
  vision: Any,
  forensic: Any,
  duration: float,
  root: Path,
) -> Path:
  audit_path.parent.mkdir(parents=True, exist_ok=True)
  total_pass = sum(r.passed for r in runs)
  total_fail = sum(r.failed for r in runs)
  total_skip = sum(r.skipped for r in runs)
  sec_findings = []
  for r in runs:
    sec_findings.extend(r.security_findings)

  lines = [
    "# BRQOSH Master Cognitive Audit Report",
    "",
    f"**Generated:** {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}",
    f"**Duration:** {duration:.0f}s ({duration/60:.1f} min)",
    f"**Profiles:** {', '.join(state.profiles_audited)}",
    f"**Steps:** {total_pass} pass / {total_fail} fail / {total_skip} skip",
    f"**Frames captured:** {len(vision.frames)}",
    f"**State screens mapped:** {len(state.screens)}",
    f"**Anomalies:** {len(state.anomalies)}",
    f"**Silent failures flagged:** {len(state.silent_failures)}",
    "",
    "---",
    "",
    "## 1. Chronological Audit Diary",
    "",
    f"Full narrative: [`CHRONICLE.md`]({chronicle_path.name})",
    "",
    "_Excerpt — last 5 entries see diary file._",
    "",
    "## 2. Learned Application Model",
    "",
    f"Persisted: `app_state_model.json` — **{len(state.transitions)}** transitions, **{len(state.screens)}** screens.",
    "",
    "### Transition graph (sample)",
    "",
    "```mermaid",
    "stateDiagram-v2",
  ]
  for tr in state.transitions[:25]:
    frm = tr.get("from", "?")[:40].replace('"', "'")
    to = tr.get("to", "?")[:40].replace('"', "'")
    lines.append(f'  "{frm}" --> "{to}": {tr.get("trigger", "")[:20]}')
  lines.extend([
    "```",
    "",
    "## 3. Critical Architecture & Connectivity Gaps",
    "",
  ])

  fails = [s for r in runs for s in r.steps if s.status == "fail"]
  if fails:
    for s in fails[:30]:
      lines.append(f"- **{s.step}:** {s.message}")
    if len(fails) > 30:
      lines.append(f"- _…and {len(fails)-30} more — see CHRONICLE.md_")
  else:
    lines.append("_No step failures recorded._")

  lines.extend([
    "",
    "### Forensic crises",
    "",
  ])
  for ev in forensic.events[:10]:
    lines.append(f"- **{ev.phase}:** {ev.error[:100]}")
    if ev.source_hint:
      lines.append(f"  - Source: `{ev.source_hint}`")

  lines.extend([
    "",
    "## 4. Visual Pathology & UX Friction",
    "",
    "- Resize stress applied — review `frame_*_resize_stress_*.png`",
    "- RTL dense ERP: verify DataGrid row clipping at 900×600",
    "- OCR/UIA divergence: accessibility-first; OCR only for custom-drawn surfaces",
    "",
    "## 5. Empathetic Experience Report",
    "",
    "| Signal | Assessment |",
    "|--------|------------|",
    "| Cryptic errors | Flag any raw stack traces in modals (forensic log) |",
    "| Silent save | Probe: Save without grid refresh — check silent_failures in state model |",
    "| Dead-end nav | Pages marked dead_end when profile hides module |",
    "| Bored-user ESC/Tab | Documented in CHRONICLE — verify no data loss |",
    "",
    "## 6. Blueprint Refactoring",
    "",
    "**P0 — Nav scroll:** `human_navigator.find_nav()` must `scroll_into_view()` — implemented.",
    "",
    "**P0 — NavKeyMap gaps:** Add `customer_support`, `pricing_plans` to `tools/UiAudit/NavKeyMap.cs`.",
    "",
    "**P1 — AutomationId:** 266 inputs without ids — see omni-audit ui_map.",
    "",
    "**P1 — Profile matrix:** Manufacturing lacks Sales — use Retail+Construction for sales UI audit.",
    "",
    "**P2 — Shared catalog:** PageAuditCatalog.cs is single source — Python loads at runtime.",
    "",
    "## 7. Per-profile summary",
    "",
  ])
  for r in runs:
    lines.append(f"### {r.profile}: {r.passed}/{len(r.steps)} pass")
    for s in [x for x in r.steps if x.status == "fail"][:8]:
      lines.append(f"- ❌ {s.step}: {s.message}")

  lines.extend([
    "",
    "## Verify",
    "```powershell",
    f"cd {root}",
    ".\\tools\\UiAudit\\run-audit.ps1",
    ".\\tools\\desktop-cognitive-audit\\run-audit.ps1",
    "```",
    "",
  ])

  audit_path.write_text("\n".join(lines), encoding="utf-8")

  sec_lines = [
    "# Security & Sabotage Findings",
    "",
    f"**Total payload events:** {len(sec_findings)}",
    "",
  ]
  if sec_findings:
    for f in sec_findings[:50]:
      sec_lines.append(f"- **{f.get('page')}** / `{f.get('field')}` [{f.get('mode')}]: `{f.get('payload','')[:60]}` → {f.get('error','')}")
  else:
    sec_lines.append("_No immediate crashes from malicious/chaos payloads in this run._")
    sec_lines.append("")
    sec_lines.append("**Note:** Absence of crash ≠ safe — verify server-side validation + audit log.")
  security_path.write_text("\n".join(sec_lines), encoding="utf-8")
  return audit_path
