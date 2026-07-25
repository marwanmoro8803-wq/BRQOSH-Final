"""Design Precision & Aesthetic Intelligence — typography, spacing, contrast heuristics."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

try:
  from PIL import Image, ImageStat
except ImportError:
  Image = None


@dataclass
class DesignFinding:
  severity: str
  category: str
  message: str
  screenshot: str | None = None
  principle: str = ""


@dataclass
class DesignAuditResult:
  findings: list[DesignFinding] = field(default_factory=list)
  palette_samples: list[tuple[int, int, int]] = field(default_factory=list)

  @property
  def score(self) -> int:
    crit = sum(1 for f in self.findings if f.severity == "Critical")
    high = sum(1 for f in self.findings if f.severity == "High")
    return max(0, 100 - crit * 25 - high * 10 - len(self.findings))


def _luminance(rgb: tuple[int, int, int]) -> float:
  r, g, b = [x / 255 for x in rgb]
  return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast_ratio(fg: tuple[int, int, int], bg: tuple[int, int, int]) -> float:
  l1, l2 = _luminance(fg), _luminance(bg)
  lighter, darker = max(l1, l2), min(l1, l2)
  return (lighter + 0.05) / (darker + 0.05)


def audit_frames(frames: list[Any], theme_xaml: Path | None = None) -> DesignAuditResult:
  result = DesignAuditResult()
  if not frames or Image is None:
    result.findings.append(DesignFinding("Medium", "tooling", "Pillow unavailable — design pixel audit skipped"))
    return result

  for frame in frames[-12:]:
    path = Path(frame.path if hasattr(frame, "path") else frame.get("path", ""))
    if not path.exists():
      continue
    try:
      with Image.open(path) as img:
        img = img.convert("RGB")
        w, h = img.size
        # Sample corners for palette
        for xy in [(10, 10), (w - 10, 10), (10, h - 10)]:
          result.palette_samples.append(img.getpixel(xy))
        # Crude contrast: center vs corner
        center = img.getpixel((w // 2, h // 2))
        corner = img.getpixel((10, 10))
        ratio = contrast_ratio(center, corner)
        if ratio < 3.0:
          result.findings.append(DesignFinding(
            "High", "contrast", f"Low contrast region in {path.name} (ratio≈{ratio:.1f})",
            screenshot=str(path), principle="WCAG 2.1 — text contrast",
          ))
        if w < 1000:
          result.findings.append(DesignFinding(
            "Medium", "responsive", f"Capture at {w}x{h} — dense ERP may clip at narrow widths",
            screenshot=str(path), principle="Responsive layout",
          ))
    except Exception as exc:
      result.findings.append(DesignFinding("Low", "capture", str(exc)[:80]))

  # Theme token check (static)
  if theme_xaml and theme_xaml.exists():
    text = theme_xaml.read_text(encoding="utf-8", errors="replace")
    if text.count("Margin=") > 80 and "Spacing" not in text:
      result.findings.append(DesignFinding(
        "Medium", "spacing", "Scattered Margin= in BrqoshTheme — prefer tokenized spacing",
        principle="8px grid / design system",
      ))

  # Anti-patterns from BRQOSH rules
  result.findings.append(DesignFinding(
    "Low", "anti-pattern", "Verify no emoji in ERP chrome — monogram nav only",
    principle="brqosh-ui-design-system",
  ))
  return result
