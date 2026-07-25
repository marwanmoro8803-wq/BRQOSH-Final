"""
Load PageAuditCatalog from tools/UiAudit/PageAuditCatalog.cs — single source of truth.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass
class ButtonSpec:
  label_contains: str
  action: str
  dialog_kind: str | None = None


@dataclass
class PageSpec:
  nav_title: str
  buttons: list[ButtonSpec]
  min_grid_rows: int | None = None


_ACTION_MAP = {
  "OpenDialogCancel": "open_dialog_cancel",
  "OpenDialogSave": "open_dialog_save",
  "RunReport": "run_report",
  "ExportPdf": "export_pdf",
  "RequireSelectionWarning": "require_selection_warning",
  "SelectFirstThenAct": "select_first_then_act",
  "ClickSafe": "click_safe",
  "OpenWizardCancel": "open_wizard_cancel",
  "VerifyGridOnly": "verify_grid_only",
}


def load_catalog(repo_root: Path) -> list[PageSpec]:
  cs = repo_root / "tools" / "UiAudit" / "PageAuditCatalog.cs"
  if not cs.exists():
    return _fallback_catalog()
  text = cs.read_text(encoding="utf-8")
  pages: list[PageSpec] = []

  # Match: new("Title", [ ... ], MinGridRows: N) or new("Title", [ ... ])
  page_pattern = re.compile(
    r'new\("([^"]+)",\s*\[(.*?)\]\s*(?:,\s*MinGridRows:\s*(\d+))?\)',
    re.DOTALL,
  )
  btn_pattern = re.compile(
    r'new\("([^"]+)",\s*AuditAction\.(\w+)(?:,\s*"([^"]*)")?\)',
  )

  for m in page_pattern.finditer(text):
    title = m.group(1)
    body = m.group(2)
    min_rows = int(m.group(3)) if m.group(3) else None
    buttons: list[ButtonSpec] = []
    for bm in btn_pattern.finditer(body):
      action = _ACTION_MAP.get(bm.group(2), bm.group(2).lower())
      buttons.append(ButtonSpec(bm.group(1), action, bm.group(3) or None))
    pages.append(PageSpec(title, buttons, min_rows))

  return pages if pages else _fallback_catalog()


def _fallback_catalog() -> list[PageSpec]:
  return [
    PageSpec("لوحة التحكم", [ButtonSpec("KPI", "verify_grid_only")], 1),
    PageSpec("دليل الحسابات", [
      ButtonSpec("حساب جديد", "open_dialog_cancel", "account"),
      ButtonSpec("تعديل", "require_selection_warning"),
    ]),
  ]
