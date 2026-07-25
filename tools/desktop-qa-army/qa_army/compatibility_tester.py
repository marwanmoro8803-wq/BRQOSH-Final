"""Compatibility matrix — resolutions and DPI stress notes."""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class CompatRow:
  resolution: str
  dpi: str
  status: str
  notes: str


@dataclass
class CompatibilityResult:
  matrix: list[CompatRow] = field(default_factory=list)


def build_matrix(resize_tested: bool, frames_count: int) -> CompatibilityResult:
  res = CompatibilityResult()
  base_notes = "UIA capture OK" if frames_count else "Not exercised"
  for r in ["1366x768", "1920x1080", "2560x1440"]:
    for dpi in ["100%", "125%", "150%"]:
      status = "PASS" if resize_tested and r == "1920x1080" and dpi == "100%" else "PARTIAL"
      notes = base_notes if status == "PASS" else "Manual OS DPI switch recommended"
      res.matrix.append(CompatRow(r, dpi, status, notes))
  return res
