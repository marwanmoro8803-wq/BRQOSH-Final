"""
Screenshot capture and optional OCR for BRQOSH desktop omni-audit.
"""

from __future__ import annotations

import logging
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

try:
    import mss
    import mss.tools
except ImportError:
    mss = None  # type: ignore

try:
    from PIL import Image
except ImportError:
    Image = None  # type: ignore

try:
    import pytesseract
except ImportError:
    pytesseract = None  # type: ignore


class VisionCapture:
  def __init__(self, screenshots_dir: Path, ocr_enabled: bool = True, ocr_lang: str = "ara+eng"):
    self.screenshots_dir = Path(screenshots_dir)
    self.screenshots_dir.mkdir(parents=True, exist_ok=True)
    self.ocr_enabled = ocr_enabled and pytesseract is not None
    self.ocr_lang = ocr_lang
    self._step = 0
    self.captures: list[dict[str, Any]] = []

  def next_semantic_name(self, semantic: str) -> str:
    self._step += 1
    safe = re.sub(r"[^\w\u0600-\u06FF\-]+", "_", semantic.strip())[:80]
    ts = datetime.now(timezone.utc).strftime("%H%M%S")
    return f"step_{self._step:03d}_{safe}_{ts}.png"

  def capture_window(self, window: Any, semantic: str) -> Path:
    """Capture window via pywinauto wrapper or full screen fallback."""
    filename = self.next_semantic_name(semantic)
    path = self.screenshots_dir / filename

    try:
      img = window.capture_as_image()
      img.save(path)
    except Exception as exc:
      logger.warning("Window capture failed (%s); falling back to full screen.", exc)
      path = self.capture_full_screen(semantic, override_name=filename)

    ocr_text = self._ocr(path) if self.ocr_enabled else ""
    entry = {
      "step": self._step,
      "semantic": semantic,
      "path": str(path),
      "ocr_preview": ocr_text[:500] if ocr_text else "",
    }
    self.captures.append(entry)
    return path

  def capture_full_screen(self, semantic: str, override_name: str | None = None) -> Path:
    filename = override_name or self.next_semantic_name(semantic)
    path = self.screenshots_dir / filename

    if mss is None:
      # last resort: create placeholder
      if Image is not None:
        Image.new("RGB", (800, 600), color=(30, 30, 30)).save(path)
      else:
        path.write_bytes(b"")
      return path

    with mss.mss() as sct:
      monitor = sct.monitors[0]
      shot = sct.grab(monitor)
      mss.tools.to_png(shot.rgb, shot.size, output=str(path))

    ocr_text = self._ocr(path) if self.ocr_enabled else ""
    self.captures.append({
      "step": self._step,
      "semantic": semantic,
      "path": str(path),
      "ocr_preview": ocr_text[:500] if ocr_text else "",
    })
    return path

  def _ocr(self, image_path: Path) -> str:
    if not self.ocr_enabled or Image is None or pytesseract is None:
      return ""
    try:
      with Image.open(image_path) as img:
        return pytesseract.image_to_string(img, lang=self.ocr_lang)
    except Exception as exc:
      logger.debug("OCR failed: %s", exc)
      return ""

  def relative_markdown(self, path: Path, report_dir: Path) -> str:
    try:
      rel = path.relative_to(report_dir)
    except ValueError:
      rel = path.name
    return str(rel).replace("\\", "/")
