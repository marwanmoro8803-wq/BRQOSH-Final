"""WCAG-oriented accessibility audit via UIA tree."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class A11yFinding:
  severity: str
  wcag: str
  message: str
  element: str = ""


@dataclass
class AccessibilityResult:
  findings: list[A11yFinding] = field(default_factory=list)
  named_controls: int = 0
  unnamed_interactive: int = 0
  keyboard_reachable: int = 0

  @property
  def score(self) -> int:
    return max(0, 100 - self.unnamed_interactive * 2 - len([f for f in self.findings if f.severity == "Critical"]) * 20)


def audit_uia_elements(elements: list[Any]) -> AccessibilityResult:
  res = AccessibilityResult()
  interactive = {"Button", "Edit", "ComboBox", "CheckBox", "Hyperlink"}
  for el in elements:
    ct = el.control_type if hasattr(el, "control_type") else el.get("control_type", "")
    if ct not in interactive:
      continue
    name = (el.name if hasattr(el, "name") else el.get("name", "")) or ""
    auto_id = (el.automation_id if hasattr(el, "automation_id") else el.get("automation_id")) or ""
    if name or auto_id:
      res.named_controls += 1
    else:
      res.unnamed_interactive += 1
      res.findings.append(A11yFinding(
        "High", "4.1.2 Name, Role, Value",
        f"Unnamed {ct} — screen reader gap",
        element=ct,
      ))
    if ct == "Button" and len(name) < 2:
      res.findings.append(A11yFinding("Medium", "2.4.6 Headings and Labels", f"Cryptic button label: '{name}'"))
  if res.unnamed_interactive > 30:
    res.findings.append(A11yFinding(
      "Critical", "4.1.2", f"{res.unnamed_interactive} interactive controls lack accessible names",
    ))
  return res


def audit_rtl_flow(frame_text: str) -> list[A11yFinding]:
  findings: list[A11yFinding] = []
  if frame_text and any("\u0600" <= c <= "\u06FF" for c in frame_text):
    if "LTR" in frame_text.upper() and "RTL" not in frame_text.upper():
      findings.append(A11yFinding("Medium", "1.3.2", "Arabic text visible — verify FlowDirection RTL"))
  return findings
