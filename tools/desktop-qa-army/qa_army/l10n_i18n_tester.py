"""L10n/I18n — RTL, Arabic-Indic digits, LocaleManager coverage."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class I18nFinding:
  severity: str
  message: str
  file: str = ""


@dataclass
class I18nResult:
  findings: list[I18nFinding] = field(default_factory=list)
  locale_manager_usages: int = 0
  hardcoded_ar_only: int = 0


RTL_TEST_STRINGS = [
  "مرحبا بالعالم",
  "١٢٣٤٥٦٧٨٩٠",
  "test\u202Ehidden",
  "SAR 1,234.56",
  "١٬٢٣٤٫٥٦ ر.س",
]


def audit_source_i18n(desktop_src: Path) -> I18nResult:
  res = I18nResult()
  xaml_files = list(desktop_src.rglob("*.xaml"))
  for xf in xaml_files:
    if "Themes" in xf.parts:
      continue
    text = xf.read_text(encoding="utf-8", errors="replace")
    if "LocaleManager.T" in text:
      res.locale_manager_usages += text.count("LocaleManager.T")
    # Content= with Arabic but no LocaleManager in same file (heuristic)
    if re.search(r'Content="[^"]*[\u0600-\u06FF]', text) and "LocaleManager" not in text:
      res.hardcoded_ar_only += 1
      if res.hardcoded_ar_only <= 15:
        res.findings.append(I18nFinding(
          "Medium", "Hardcoded Arabic string in XAML", str(xf.relative_to(desktop_src.parent.parent)),
        ))
  if res.locale_manager_usages < 50:
    res.findings.append(I18nFinding("High", f"Only {res.locale_manager_usages} LocaleManager.T — expand i18n"))
  res.findings.append(I18nFinding("Info", "RTL FlowDirection required on shell — verify AR+EN toggle"))
  return res


def rtl_payloads() -> list[str]:
  return RTL_TEST_STRINGS
