"""
Vision brain — accessibility APIs first (UIA), OCR fallback (pytesseract + OpenCV).
Maps UI elements with bounding boxes and extracted text.
"""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

try:
  import mss
  import mss.tools
except ImportError:
  mss = None

try:
  from PIL import Image
except ImportError:
  Image = None

try:
  import pytesseract
except ImportError:
  pytesseract = None


@dataclass
class VisualElement:
  control_type: str
  name: str
  automation_id: str | None
  rect: tuple[int, int, int, int] | None
  enabled: bool
  visible: bool
  text_children: list[str] = field(default_factory=list)


@dataclass
class FrameCapture:
  path: str
  semantic: str
  timestamp: str
  elements: list[VisualElement]
  ocr_text: str = ""
  window_title: str = ""


class VisionBrain:
  def __init__(self, screenshots_dir: Path, ocr_enabled: bool = True, ocr_lang: str = "ara+eng"):
    self.dir = Path(screenshots_dir)
    self.dir.mkdir(parents=True, exist_ok=True)
    self.ocr_enabled = ocr_enabled and pytesseract is not None
    self.ocr_lang = ocr_lang
    self.frames: list[FrameCapture] = []
    self._step = 0

  def _name(self, semantic: str) -> str:
    self._step += 1
    safe = re.sub(r"[^\w\u0600-\u06FF\-]+", "_", semantic)[:70]
    ts = datetime.now(timezone.utc).strftime("%H%M%S")
    return f"frame_{self._step:04d}_{safe}_{ts}.png"

  def perceive_window(self, window: Any, semantic: str) -> FrameCapture:
    """Capture + UIA tree scan + optional OCR."""
    filename = self._name(semantic)
    path = self.dir / filename
    title = ""
    elements: list[VisualElement] = []

    try:
      img = window.capture_as_image()
      img.save(path)
    except Exception:
      path = self._full_screen(path)

    try:
      title = window.window_text() or ""
    except Exception:
      pass

    elements = self._scan_uia_tree(window)
    ocr = self._ocr(path) if self.ocr_enabled else self._concat_uia_text(elements)

    frame = FrameCapture(
      path=str(path),
      semantic=semantic,
      timestamp=datetime.now(timezone.utc).isoformat(),
      elements=elements,
      ocr_text=ocr[:4000],
      window_title=title,
    )
    self.frames.append(frame)
    return frame

  def _scan_uia_tree(self, root: Any) -> list[VisualElement]:
    out: list[VisualElement] = []
    types = ("Button", "Edit", "ComboBox", "Text", "DataItem", "CheckBox", "ListItem", "MenuItem")
    try:
      for ct in types:
        for el in root.descendants(control_type=ct)[:80]:
          try:
            info = el.element_info
            rect = None
            try:
              r = el.rectangle()
              rect = (r.left, r.top, r.right, r.bottom)
            except Exception:
              pass
            texts = []
            for t in el.descendants(control_type="Text")[:3]:
              try:
                tx = t.window_text()
                if tx:
                  texts.append(tx)
              except Exception:
                pass
            out.append(VisualElement(
              control_type=ct,
              name=info.name or "",
              automation_id=getattr(info, "automation_id", None),
              rect=rect,
              enabled=getattr(info, "enabled", True),
              visible=not getattr(info, "offscreen", False),
              text_children=texts,
            ))
          except Exception:
            continue
    except Exception as exc:
      logger.debug("UIA scan failed: %s", exc)
    return out[:200]

  def _concat_uia_text(self, elements: list[VisualElement]) -> str:
    parts = [e.name for e in elements if e.name]
    parts += [t for e in elements for t in e.text_children]
    return "\n".join(parts[:100])

  def _ocr(self, path: Path) -> str:
    if not self.ocr_enabled or Image is None:
      return ""
    try:
      with Image.open(path) as img:
        return pytesseract.image_to_string(img, lang=self.ocr_lang)
    except Exception as exc:
      logger.debug("OCR: %s", exc)
      return ""

  def _full_screen(self, path: Path) -> Path:
    if mss is None:
      return path
    with mss.mss() as sct:
      shot = sct.grab(sct.monitors[0])
      mss.tools.to_png(shot.rgb, shot.size, output=str(path))
    return path

  def read_visible_text(self, frame: FrameCapture) -> str:
    if frame.ocr_text.strip():
      return frame.ocr_text
    return self._concat_uia_text(frame.elements)
