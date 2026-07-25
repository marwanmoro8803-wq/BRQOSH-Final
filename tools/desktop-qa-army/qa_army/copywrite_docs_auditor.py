"""Microcopy & documentation string audit."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class CopyFinding:
  severity: str
  string: str
  issue: str
  suggestion: str
  file: str


@dataclass
class CopywriteResult:
  findings: list[CopyFinding] = field(default_factory=list)
  strings_scanned: int = 0


JARGON = ["SQLException", "FK_", "null reference", "stack trace", "0x800"]
BLAME = ["نسيت", "you forgot", "خطأ المستخدم"]


def audit_strings(desktop_src: Path) -> CopywriteResult:
  res = CopywriteResult()
  for xf in desktop_src.rglob("*.xaml"):
    text = xf.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r'Content="([^"]{4,120})"', text):
      s = m.group(1)
      res.strings_scanned += 1
      rel = str(xf.relative_to(desktop_src.parent.parent))
      for j in JARGON:
        if j.lower() in s.lower():
          res.findings.append(CopyFinding("Critical", s, f"Technical jargon: {j}", "User-facing Arabic/EN message", rel))
      for b in BLAME:
        if b in s:
          res.findings.append(CopyFinding("High", s, "Blame-the-user tone", "Neutral helpful copy", rel))
  for cs in desktop_src.rglob("*.cs"):
    if "MessageBox.Show" not in (t := cs.read_text(encoding="utf-8", errors="replace")):
      continue
    for j in JARGON:
      if j in t:
        res.findings.append(CopyFinding(
          "Critical", j, "Exception text may reach MessageBox",
          "Wrap with user-safe message", str(cs.relative_to(desktop_src.parent.parent)),
        ))
        break
  return res
