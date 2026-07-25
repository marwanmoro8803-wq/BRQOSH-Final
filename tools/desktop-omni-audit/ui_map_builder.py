"""
Static UI map builder — parses BRQOSH WPF XAML + code-behind for telemetry.
Maps interactive elements → source location → handler → expected data flow.
"""

from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

from payloads import infer_payload_type


@dataclass
class UiElement:
  file: str
  line: int
  xaml_tag: str
  name: str | None
  automation_id: str | None
  automation_name: str | None
  content: str | None
  click_handler: str | None
  handler_file: str | None
  handler_line: int | None
  input_type: str
  payload_type: str
  nav_title: str | None = None


@dataclass
class UiPage:
  xaml_file: str
  class_name: str | None
  nav_title: str | None
  elements: list[UiElement] = field(default_factory=list)


@dataclass
class UiMap:
  generated_from: str
  pages: list[UiPage] = field(default_factory=list)
  navigation_catalog: list[dict[str, str]] = field(default_factory=list)
  stats: dict[str, int] = field(default_factory=dict)

  def to_dict(self) -> dict[str, Any]:
    return {
      "generated_from": self.generated_from,
      "pages": [
        {
          "xaml_file": p.xaml_file,
          "class_name": p.class_name,
          "nav_title": p.nav_title,
          "elements": [asdict(e) for e in p.elements],
        }
        for p in self.pages
      ],
      "navigation_catalog": self.navigation_catalog,
      "stats": self.stats,
    }


# Regex patterns for XAML parsing
_RE_CLASS = re.compile(r'x:Class="([^"]+)"')
_RE_NAME = re.compile(r'\bx:Name="([^"]+)"')
_RE_AUTO_ID = re.compile(r'AutomationProperties\.AutomationId="([^"]+)"')
_RE_AUTO_NAME = re.compile(r'AutomationProperties\.Name="([^"]+)"')
_RE_CONTENT = re.compile(r'\bContent="([^"]+)"')
_RE_CLICK = re.compile(r'\bClick="([^"]+)"')
_RE_TAG_OPEN = re.compile(
  r"<(Button|TextBox|PasswordBox|ComboBox|CheckBox|ToggleButton|DatePicker|DataGrid|ListBox|RadioButton)\b([^>]*)>",
  re.IGNORECASE,
)
_RE_HANDLER = re.compile(r"(?:private|protected|public)\s+(?:async\s+)?void\s+(\w+)\s*\([^)]*\)")

# Nav title from filename heuristic
_PAGE_NAV_HINTS: dict[str, str] = {
  "CustomersPage": "العملاء",
  "LoginWindow": "تسجيل الدخول",
  "MainWindow": "BRQOSH ERP",
  "ChartOfAccountsPage": "دليل الحسابات",
  "JournalEntriesPage": "القيود اليومية",
  "SalesInvoicesPage": "فواتير المبيعات",
  "CompanySettingsPage": "إعدادات المنشأة",
  "UsersPage": "المستخدمون",
}


def _line_number(content: str, index: int) -> int:
  return content[:index].count("\n") + 1


def _parse_handler_map(cs_path: Path) -> dict[str, int]:
  if not cs_path.exists():
    return {}
  text = cs_path.read_text(encoding="utf-8", errors="replace")
  handlers: dict[str, int] = {}
  for m in _RE_HANDLER.finditer(text):
    handlers[m.group(1)] = _line_number(text, m.start())
  return handlers


def _infer_nav_title(xaml_path: Path, class_name: str | None) -> str | None:
  stem = xaml_path.stem
  if stem in _PAGE_NAV_HINTS:
    return _PAGE_NAV_HINTS[stem]
  if class_name:
    short = class_name.split(".")[-1]
    if short in _PAGE_NAV_HINTS:
      return _PAGE_NAV_HINTS[short]
  return None


def parse_xaml_file(xaml_path: Path, desktop_root: Path) -> UiPage:
  text = xaml_path.read_text(encoding="utf-8", errors="replace")
  class_m = _RE_CLASS.search(text)
  class_name = class_m.group(1) if class_m else None

  cs_path = xaml_path.with_suffix(".xaml.cs")
  if not cs_path.exists() and class_name:
    # try Views/Pages/Foo.xaml.cs pattern
    cs_path = xaml_path.parent / f"{xaml_path.stem}.xaml.cs"

  handler_lines = _parse_handler_map(cs_path)
  rel_xaml = str(xaml_path.relative_to(desktop_root.parent.parent)).replace("\\", "/")

  page = UiPage(
    xaml_file=rel_xaml,
    class_name=class_name,
    nav_title=_infer_nav_title(xaml_path, class_name),
    elements=[],
  )

  for m in _RE_TAG_OPEN.finditer(text):
    tag = m.group(1)
    attrs = m.group(2)
    line = _line_number(text, m.start())

    name_m = _RE_NAME.search(attrs)
    auto_id_m = _RE_AUTO_ID.search(attrs)
    auto_name_m = _RE_AUTO_NAME.search(attrs)
    content_m = _RE_CONTENT.search(attrs)
    click_m = _RE_CLICK.search(attrs)

    name = name_m.group(1) if name_m else None
    auto_id = auto_id_m.group(1) if auto_id_m else None
    auto_name = auto_name_m.group(1) if auto_name_m else None
    content = content_m.group(1) if content_m else None
    click = click_m.group(1) if click_m else None

    is_input = tag.lower() in ("textbox", "passwordbox", "combobox", "datepicker")
    is_interactive = tag.lower() in (
      "button", "textbox", "passwordbox", "combobox", "checkbox",
      "togglebutton", "datepicker", "listbox", "radiobutton",
    )
    if not is_interactive and tag.lower() != "datagrid":
      continue
    if not any([name, auto_id, auto_name, content, click]) and tag.lower() != "datagrid":
      continue

    payload_type = infer_payload_type(
      name or "",
      auto_name or content or "",
      tag,
    )
    handler_file = None
    handler_line = None
    if click and cs_path.exists():
      handler_file = str(cs_path.relative_to(desktop_root.parent.parent)).replace("\\", "/")
      handler_line = handler_lines.get(click)

    page.elements.append(
      UiElement(
        file=rel_xaml,
        line=line,
        xaml_tag=tag,
        name=name,
        automation_id=auto_id,
        automation_name=auto_name or content,
        content=content,
        click_handler=click,
        handler_file=handler_file,
        handler_line=handler_line,
        input_type="input" if is_input else "action",
        payload_type=payload_type if is_input else "n/a",
        nav_title=page.nav_title,
      )
    )

  return page


def parse_navigation_catalog(catalog_path: Path) -> list[dict[str, str]]:
  """Extract nav pages from NavigationCatalog.cs."""
  if not catalog_path.exists():
    return []
  text = catalog_path.read_text(encoding="utf-8", errors="replace")
  entries: list[dict[str, str]] = []
  for m in re.finditer(r'new\("([^"]+)",\s*"([^"]+)"\)', text):
    entries.append({"key": m.group(1), "title_ar": m.group(2)})
  return entries


def build_ui_map(desktop_source: Path, repo_root: Path, config_nav: list[str] | None = None) -> UiMap:
  desktop_source = desktop_source.resolve()
  repo_root = repo_root.resolve()

  xaml_files = sorted(desktop_source.rglob("*.xaml"))
  pages: list[UiPage] = []
  total_elements = 0
  total_inputs = 0

  for xf in xaml_files:
    if "Themes" in xf.parts:
      continue
    page = parse_xaml_file(xf, desktop_source)
    if page.elements:
      pages.append(page)
      total_elements += len(page.elements)
      total_inputs += sum(1 for e in page.elements if e.input_type == "input")

  catalog_path = desktop_source / "Navigation" / "NavigationCatalog.cs"
  nav_catalog = parse_navigation_catalog(catalog_path)

  # Enrich nav titles from catalog keys → Arabic titles via config
  title_set = {n["title_ar"] for n in nav_catalog}
  if config_nav:
    title_set.update(config_nav)

  ui_map = UiMap(
    generated_from=str(desktop_source),
    pages=pages,
    navigation_catalog=nav_catalog,
    stats={
      "xaml_files_scanned": len(xaml_files),
      "pages_with_elements": len(pages),
      "interactive_elements": total_elements,
      "input_fields": total_inputs,
      "nav_pages_catalog": len(nav_catalog),
    },
  )
  return ui_map


def save_ui_map(ui_map: UiMap, output_path: Path) -> Path:
  output_path.parent.mkdir(parents=True, exist_ok=True)
  output_path.write_text(
    json.dumps(ui_map.to_dict(), ensure_ascii=False, indent=2),
    encoding="utf-8",
  )
  return output_path


def load_ui_map(path: Path) -> dict[str, Any]:
  return json.loads(path.read_text(encoding="utf-8"))


def find_element_source(ui_map: dict[str, Any], handler_name: str) -> dict[str, Any] | None:
  for page in ui_map.get("pages", []):
    for el in page.get("elements", []):
      if el.get("click_handler") == handler_name:
        return el
  return None


if __name__ == "__main__":
  import argparse

  parser = argparse.ArgumentParser(description="Build BRQOSH UI static map")
  parser.add_argument("--desktop", default="../../src/Brqosh.Desktop")
  parser.add_argument("--out", default="output/ui_map.json")
  args = parser.parse_args()

  root = Path(__file__).resolve().parent
  desktop = (root / args.desktop).resolve()
  ui_map = build_ui_map(desktop, root.parent.parent)
  out = save_ui_map(ui_map, root / args.out)
  print(f"UI map: {out} ({ui_map.stats})")
