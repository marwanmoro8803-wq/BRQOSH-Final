"""
Assembles AUDIT_REPORT.md from omni-audit session data.
"""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def _rel(path: str | None, report_dir: Path) -> str:
  if not path:
    return ""
  p = Path(path)
  try:
    return str(p.relative_to(report_dir)).replace("\\", "/")
  except ValueError:
    return p.name


def build_audit_report(
  *,
  output_path: Path,
  ui_map: dict[str, Any],
  nav_results: list[Any],
  session: Any,
  crisis_events: list[Any],
  vision_captures: list[dict[str, Any]],
  config: dict[str, Any],
  repo_root: Path,
  duration_sec: float,
  comprehensive_results: Any | None = None,
) -> Path:
  output_path.parent.mkdir(parents=True, exist_ok=True)
  report_dir = output_path.parent

  passed = sum(1 for r in nav_results if getattr(r, "success", False))
  failed = len(nav_results) - passed
  total_payloads = getattr(session, "payloads_injected", 0)

  comp_line = ""
  if comprehensive_results is not None:
    comp_line = (
      f"**Comprehensive steps:** {comprehensive_results.passed} pass / "
      f"{comprehensive_results.failed} fail / {comprehensive_results.skipped} skip "
      f"(total {len(comprehensive_results.steps)})  \n"
    )

  lines: list[str] = [
    "# BRQOSH Desktop Omni-Audit Report",
    "",
    f"**Generated:** {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}  ",
    f"**Duration:** {duration_sec:.1f}s  ",
    f"**User:** {getattr(session, 'username', 'n/a')}  ",
    f"**Pages audited:** {len(nav_results)} ({passed} pass / {failed} fail)  ",
    comp_line,
    f"**Payloads injected:** {total_payloads}  ",
  "",
    "---",
    "",
    "## 1. Omni-Coverage Map",
    "",
    "### Static source telemetry",
    "",
    f"| Metric | Value |",
    f"|--------|-------|",
    f"| XAML files scanned | {ui_map.get('stats', {}).get('xaml_files_scanned', 0)} |",
    f"| Pages with interactive elements | {ui_map.get('stats', {}).get('pages_with_elements', 0)} |",
    f"| Interactive elements mapped | {ui_map.get('stats', {}).get('interactive_elements', 0)} |",
    f"| Input fields mapped | {ui_map.get('stats', {}).get('input_fields', 0)} |",
    f"| Navigation catalog entries | {ui_map.get('stats', {}).get('nav_pages_catalog', 0)} |",
    "",
    "### Live navigation depth",
    "",
    "| Page | Status | Payloads | Buttons | Screenshot |",
    "|------|--------|----------|---------|------------|",
  ]

  for r in nav_results:
    status = "✅" if getattr(r, "success", False) else "❌"
    shot = _rel(getattr(r, "screenshot", None), report_dir)
    shot_md = f"![{r.page}]({shot})" if shot else "—"
    lines.append(
      f"| {r.page} | {status} | {getattr(r, 'payloads_tested', 0)} | "
      f"{getattr(r, 'buttons_clicked', 0)} | {shot_md} |"
    )

  lines.extend([
    "",
    "### Sample mapped controls (code telemetry)",
    "",
  ])

  shown = 0
  for page in ui_map.get("pages", [])[:15]:
    for el in page.get("elements", [])[:3]:
      if shown >= 20:
        break
      handler = el.get("click_handler") or "—"
      hf = el.get("handler_file") or el.get("file", "")
      hl = el.get("handler_line") or el.get("line", "")
      lines.append(
        f"- `{el.get('xaml_tag')}` **{el.get('automation_name') or el.get('name') or '—'}** "
        f"→ `{hf}:{hl}` handler `{handler}` payload `{el.get('payload_type')}`"
      )
      shown += 1

  if comprehensive_results is not None:
    lines.extend([
      "",
      "### Comprehensive interaction audit (buttons · dialogs · grids · warnings)",
      "",
      "| Step | Status | Message |",
      "|------|--------|---------|",
    ])
    fail_steps = [s for s in comprehensive_results.steps if s.status == "fail"]
    for s in comprehensive_results.steps:
      if s.status != "fail" and len(comprehensive_results.steps) > 80:
        continue  # omit pass rows if huge
      icon = "✅" if s.status == "pass" else ("⏭" if s.status == "skip" else "❌")
      lines.append(f"| {s.step} | {icon} | {s.message[:80]} |")
    if fail_steps:
      lines.extend(["", "#### Failed steps (detail)", ""])
      for s in fail_steps:
        lines.append(f"- **{s.step}:** {s.message}")

  lines.extend([
    "",
    "---",
    "",
    "## 2. Critical & Logic Failures",
    "",
  ])

  failures = getattr(session, "failures", [])
  if not failures and not crisis_events:
    lines.append("_No critical runtime failures recorded in this run._")
  else:
    for f in failures:
      page = f.get("page", "?")
      err = f.get("error", "")
      hint = _source_hint_for_page(ui_map, page)
      lines.append(f"### ❌ {page}")
      lines.append(f"- **Error:** {err}")
      if hint:
        lines.append(f"- **Source hint:** `{hint}`")
      lines.append("")

    for ev in crisis_events:
      if isinstance(ev, dict):
        lines.append(f"### Crisis: {ev.get('phase', '?')}")
        lines.append(f"- **Error:** {ev.get('error', '')}")
        if ev.get("source_hint"):
          lines.append(f"- **Deduced source:** `{ev['source_hint']}`")
        if ev.get("screenshot"):
          rel = _rel(ev["screenshot"], report_dir)
          lines.append(f"- ![crisis]({rel})")
        lines.append(f"- **Recovered:** {ev.get('recovered', False)}")
        lines.append("")

  lines.extend([
    "",
    "---",
    "",
    "## 3. UI/UX Visual Pathology",
    "",
    "_Automated heuristics — supplement with human review of screenshots._",
    "",
  ])

  pathology = _analyze_pathology(nav_results, vision_captures)
  if pathology:
    for item in pathology:
      lines.append(f"- {item}")
  else:
    lines.append("- No automated visual pathology flags (contrast/scroll require manual review).")

  lines.extend([
    "",
    "### Screenshot gallery (last 12 steps)",
    "",
  ])
  for cap in vision_captures[-12:]:
    rel = _rel(cap.get("path"), report_dir)
    sem = cap.get("semantic", "step")
    lines.append(f"#### {sem}")
    if rel:
      lines.append(f"![{sem}]({rel})")
    ocr = cap.get("ocr_preview", "")
    if ocr:
      lines.append(f"> OCR preview: {ocr[:200]}...")
    lines.append("")

  lines.extend([
    "",
    "---",
    "",
    "## 4. Ultimate Refactoring Blueprints",
    "",
  ])

  blueprints = _refactoring_blueprints(ui_map, failures, repo_root)
  for bp in blueprints:
    lines.append(bp)
    lines.append("")

  lines.extend([
    "---",
    "",
    "## 5. Verify & Complement",
    "",
    "In-repo gates (run after fixes):",
    "```powershell",
    f"cd {repo_root}",
    "$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'",
    "dotnet build Brqosh.sln -c Release --nologo",
    "dotnet test Brqosh.sln -c Release --nologo",
    ".\\tools\\UiAudit\\run-audit.ps1",
    "```",
    "",
    "**Note:** Live OTP/ZATCA/SOCPA gates remain ⏭ EXTERNAL per `docs/OPEN_ITEMS.md`.",
    "",
  ])

  output_path.write_text("\n".join(lines), encoding="utf-8")
  return output_path


def _source_hint_for_page(ui_map: dict[str, Any], page_title: str) -> str | None:
  for page in ui_map.get("pages", []):
    if page.get("nav_title") == page_title:
      return page.get("xaml_file")
  for entry in ui_map.get("navigation_catalog", []):
    if entry.get("title_ar") == page_title:
      return f"src/Brqosh.Desktop/Navigation/NavigationCatalog.cs (key: {entry.get('key')})"
  return "src/Brqosh.Desktop/MainWindow.xaml.cs — BuildNavigation()"


def _analyze_pathology(nav_results: list[Any], captures: list[dict]) -> list[str]:
  items: list[str] = []
  fail_pages = [r.page for r in nav_results if not getattr(r, "success", False)]
  if fail_pages:
    items.append(
      f"**Navigation gaps:** {len(fail_pages)} pages failed to load — "
      f"check RTL nav button text vs `NavigationCatalog` titles: {', '.join(fail_pages[:5])}"
      + ("…" if len(fail_pages) > 5 else "")
    )
  if len(captures) < len(nav_results):
    items.append("**Screenshot coverage gap:** fewer captures than nav steps — vision pipeline may have dropped frames.")
  return items


def _refactoring_blueprints(
  ui_map: dict[str, Any],
  failures: list[dict],
  repo_root: Path,
) -> list[str]:
  bps: list[str] = []

  # AutomationId coverage
  missing_auto = 0
  for page in ui_map.get("pages", []):
    for el in page.get("elements", []):
      if el.get("input_type") == "input" and not el.get("automation_id"):
        missing_auto += 1

  if missing_auto > 0:
    bps.append(
      f"**P1 — AutomationId gaps ({missing_auto} inputs):** "
      "Add `AutomationProperties.AutomationId` to TextBox/PasswordBox in dialog XAML "
      "(pattern: `LoginWindow.xaml` lines 109–125). Improves pywinauto + FlaUI stability."
    )

  bps.append(
    "**P1 — Unify live audit paths:** "
    "Python omni-audit (`tools/desktop-omni-audit`) complements C# `tools/UiAudit`. "
    "Share `PageAuditCatalog` button specs via generated JSON to avoid drift."
  )

  if failures:
    bps.append(
      "**P0 — Nav resolution failures:** "
      "Cross-check `MainWindow.xaml.cs` `BuildNavigation()` localized titles with "
      "`NavigationCatalog.cs` Arabic `TitleAr` — UIA `title` match is exact."
    )

  bps.append(
    "**P2 — Payload hardening:** "
    "Ensure Application-layer validators reject SQLi/XSS/huge blobs before EF — "
    "grep `FluentValidation` on dialog ViewModels tied to mapped handlers."
  )

  bps.append(
    "**P2 — Crisis telemetry:** "
    "Wire unhandled WPF exceptions to `AuditLog` with view key — enables line-level trace "
    "from `crisis_handler.py` source hints."
  )

  return bps
