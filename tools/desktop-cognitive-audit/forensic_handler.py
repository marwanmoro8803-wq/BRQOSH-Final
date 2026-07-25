"""
Forensic handler — crash recovery, log tailing, source mapping, silent-failure probes.
"""

from __future__ import annotations

import json
import logging
import os
import re
import subprocess
import time
import traceback
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import psutil

logger = logging.getLogger(__name__)


@dataclass
class ForensicEvent:
  timestamp: str
  phase: str
  error: str
  traceback: str
  screenshot: str | None = None
  log_excerpt: str | None = None
  source_hint: str | None = None
  recovered: bool = False


class ForensicHandler:
  def __init__(
    self,
    process_name: str,
    crisis_log: Path,
    repo_root: Path,
    vision: Any | None = None,
    max_restarts: int = 6,
  ):
    self.process_name = process_name
    self.crisis_log = Path(crisis_log)
    self.crisis_log.parent.mkdir(parents=True, exist_ok=True)
    self.repo_root = repo_root
    self.vision = vision
    self.max_restarts = max_restarts
    self.restart_count = 0
    self.events: list[ForensicEvent] = []
    self.last_safe: dict[str, str] = {}

  def mark_safe(self, profile: str = "", screen: str = "") -> None:
    if profile:
      self.last_safe["profile"] = profile
    if screen:
      self.last_safe["screen"] = screen

  def kill_app(self) -> None:
    for proc in psutil.process_iter(["name"]):
      try:
        if proc.info["name"] and self.process_name.lower() in proc.info["name"].lower():
          proc.kill()
      except (psutil.NoSuchProcess, psutil.AccessDenied):
        pass
    time.sleep(1.5)

  def tail_logs(self, patterns: list[str], max_lines: int = 40) -> str:
    excerpts: list[str] = []
    for pat in patterns:
      expanded = os.path.expandvars(pat)
      p = Path(expanded)
      if p.is_dir():
        for f in sorted(p.glob("**/*"), key=lambda x: x.stat().st_mtime if x.exists() else 0, reverse=True)[:3]:
          if f.is_file() and f.suffix in (".log", ".txt", ".json"):
            try:
              lines = f.read_text(encoding="utf-8", errors="replace").splitlines()[-max_lines:]
              excerpts.append(f"--- {f} ---\n" + "\n".join(lines))
            except Exception:
              pass
      elif p.is_file():
        try:
          lines = p.read_text(encoding="utf-8", errors="replace").splitlines()[-max_lines:]
          excerpts.append(f"--- {p} ---\n" + "\n".join(lines))
        except Exception:
          pass
    return "\n\n".join(excerpts)[:8000]

  def map_error_to_source(self, error_text: str, ui_map: dict | None = None) -> str | None:
    if ui_map is None:
      return None
    for page in ui_map.get("pages", []):
      for el in page.get("elements", []):
        h = el.get("click_handler") or ""
        if h and h.lower() in error_text.lower():
          return f"{el.get('handler_file')}:{el.get('handler_line')}"
    m = re.search(r"(\w+Window|\w+Page|\w+Dialog)", error_text)
    if m:
      name = m.group(1)
      for page in ui_map.get("pages", []):
        if name.lower() in (page.get("class_name") or "").lower():
          return page.get("xaml_file")
    return None

  def log_event(self, ev: ForensicEvent) -> None:
    self.events.append(ev)
    with self.crisis_log.open("a", encoding="utf-8") as f:
      f.write(json.dumps(ev.__dict__, ensure_ascii=False) + "\n")

  def wrap(
    self,
    phase: str,
    fn,
    *,
    restart_fn=None,
    recover_fn=None,
    source_hint: str | None = None,
    log_patterns: list[str] | None = None,
    ui_map: dict | None = None,
  ):
    try:
      return fn()
    except Exception as exc:
      shot = None
      if self.vision:
        try:
          frame = self.vision.perceive_window(
            __import__("pywinauto").Desktop(backend="uia").window(active_only=True),
            f"crisis_{phase}",
          )
          shot = frame.path
        except Exception:
          pass
      log_ex = self.tail_logs(log_patterns or []) if log_patterns else ""
      hint = source_hint or self.map_error_to_source(str(exc), ui_map)
      ev = ForensicEvent(
        timestamp=datetime.now(timezone.utc).isoformat(),
        phase=phase,
        error=str(exc),
        traceback=traceback.format_exc(),
        screenshot=shot,
        log_excerpt=log_ex[:2000] if log_ex else None,
        source_hint=hint,
      )
      self.log_event(ev)
      if restart_fn and self.restart_count < self.max_restarts:
        self.restart_count += 1
        self.kill_app()
        time.sleep(2)
        try:
          restart_fn()
          if recover_fn:
            recover_fn()
          ev.recovered = True
          self.log_event(ev)
          return None
        except Exception as rex:
          ev.error += f" | recovery: {rex}"
          self.log_event(ev)
      raise

  def run_seed(self, seed_project: Path, profile: str) -> bool:
    if not seed_project.exists():
      return False
    env = {**os.environ, "BRQOSH_AUDIT_PROFILE": profile}
    try:
      r = subprocess.run(
        ["dotnet", "run", "--project", str(seed_project), "-c", "Release", "--nologo", "-v", "q"],
        cwd=str(self.repo_root),
        env=env,
        capture_output=True,
        text=True,
        timeout=300,
      )
      return r.returncode == 0
    except Exception as exc:
      logger.warning("Seed failed: %s", exc)
      return False

  def build_desktop(self, project: Path) -> bool:
    try:
      r = subprocess.run(
        ["dotnet", "build", str(project), "-c", "Release", "-v", "q", "--nologo"],
        cwd=str(self.repo_root),
        capture_output=True,
        text=True,
        timeout=300,
      )
      return r.returncode == 0
    except Exception:
      return False

  def probe_silent_failure(self, before_files: set[Path], after_files: set[Path], action: str) -> bool:
    """Return True if action claimed save but no file delta detected (heuristic)."""
    return len(after_files - before_files) == 0 and "save" in action.lower()
