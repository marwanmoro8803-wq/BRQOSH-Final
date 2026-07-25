# Vision & Perception — Desktop UI Truth

> **Leaks:** Anthropic `verify.md` · OpenAI Codex `control-chrome.md`  
> **Implements:** Omni-Vision section · Critical Patch `detect_new_surface`

---

## Core law (Anthropic verify adapted)

**Verification = runtime observation of pixels and accessibility tree.**

| DO | DON'T |
|----|-------|
| Launch app · drive GUI · capture evidence | import-and-call internal functions |
| Screenshot + a11y tree at each state change | unit-test theatre as audit |
| Reload after build when hot reload off | stale UI verify |

---

## Perception stack (priority order)

| Priority | Windows | macOS | Fallback |
|----------|---------|-------|----------|
| 1 | UI Automation (`pywinauto` uia) | AXUIElement / AppKit | — |
| 2 | `uiautomation` / `win32gui` | `atomacos` | — |
| 3 | OCR `pytesseract` / `easyocr` | same | custom-drawn only |
| 4 | `pyautogui` coordinates | same | last resort |

**MUST** prefer a11y text over OCR for labels and errors.

---

## `detect_new_surface()` (mandatory behavior)

After **every** interaction:

1. Compare frame to previous (pixel delta or new overlay region).
2. If >30% change OR modal rectangle detected → **switch context** to new surface.
3. Log: `New surface detected — scanning modal/dialog first`.
4. Do not click through blind until surface classified.

---

## Capture discipline

| Event | Capture |
|-------|---------|
| Screen entry | full screenshot + a11y dump |
| Form submit | before/after |
| Error modal | text + screenshot + log tail |
| Crash | crash screen before kill |

Store under: `audit_reports/evidence/YYYYMMDD-HHMMSS/`

---

## DPI and resolution

| MUST test | Method |
|-----------|--------|
| 100% · 125% · 150% · 200% DPI | OS scaling + screenshots |
| 640×480 → 4K window | resize matrix |

Flag: clip · overlap · unclickable · blurry text.

---

## Codex chrome patterns

- One strong candidate → verify directly — no endless re-screenshot loops.
- Localhost/desktop app: reload after code change before UI verify.
- Accessibility snapshot preferred over screenshot when available (Lighthouse pattern).

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| VP01 | OCR when a11y exposes the same text |
| VP02 | Click without post-click stabilization wait (200ms no pixel change) |
| VP03 | Ignore modal because "same window handle" |

**P1 code owner:** `vision_brain.py`
