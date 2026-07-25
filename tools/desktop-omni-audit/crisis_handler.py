"""
Self-healing crisis loop: capture failures, restart app, restore navigation state.
"""

from __future__ import annotations

import json
import logging
import subprocess
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

import psutil

logger = logging.getLogger(__name__)


@dataclass
class CrisisEvent:
  timestamp: str
  phase: str
  error: str
  traceback: str
  screenshot: str | None = None
  source_hint: str | None = None
  recovered: bool = False


@dataclass
class CrisisState:
  last_safe_page: str | None = None
  last_safe_step: str | None = None
  events: list[CrisisEvent] = field(default_factory=list)
  restart_count: int = 0


class CrisisHandler:
  def __init__(
    self,
    process_name: str,
    crisis_log: Path,
    max_restarts: int = 5,
    vision: Any | None = None,
  ):
    self.process_name = process_name
    self.crisis_log = Path(crisis_log)
    self.crisis_log.parent.mkdir(parents=True, exist_ok=True)
    self.max_restarts = max_restarts
    self.vision = vision
    self.state = CrisisState()

  def mark_safe(self, page: str | None = None, step: str | None = None) -> None:
    if page:
      self.state.last_safe_page = page
    if step:
      self.state.last_safe_step = step

  def kill_app(self) -> None:
    for proc in psutil.process_iter(["name", "pid"]):
      try:
        if proc.info["name"] and self.process_name.lower() in proc.info["name"].lower():
          proc.kill()
      except (psutil.NoSuchProcess, psutil.AccessDenied):
        pass
    time.sleep(1.5)

  def log_event(self, event: CrisisEvent) -> None:
    self.state.events.append(event)
    with self.crisis_log.open("a", encoding="utf-8") as f:
      f.write(json.dumps(event.__dict__, ensure_ascii=False) + "\n")

  def wrap(
    self,
    phase: str,
    fn: Callable[[], Any],
    *,
    restart_fn: Callable[[], Any] | None = None,
    recover_fn: Callable[[], None] | None = None,
    source_hint: str | None = None,
  ) -> Any:
    """Execute fn with crisis recovery."""
    try:
      result = fn()
      return result
    except Exception as exc:
      screenshot = None
      if self.vision is not None:
        try:
          shot = self.vision.capture_full_screen(f"crisis_{phase}")
          screenshot = str(shot)
        except Exception:
          pass

      event = CrisisEvent(
        timestamp=datetime.now(timezone.utc).isoformat(),
        phase=phase,
        error=str(exc),
        traceback=traceback.format_exc(),
        screenshot=screenshot,
        source_hint=source_hint,
        recovered=False,
      )
      self.log_event(event)
      logger.error("Crisis in %s: %s", phase, exc)

      if restart_fn and self.state.restart_count < self.max_restarts:
        self.state.restart_count += 1
        self.kill_app()
        time.sleep(2)
        try:
          restart_fn()
          if recover_fn:
            recover_fn()
          event.recovered = True
          self.log_event(event)
          return None
        except Exception as recover_exc:
          event.error += f" | recovery failed: {recover_exc}"
          self.log_event(event)
      raise

  def run_seed(self, repo_root: Path, seed_project: Path, profile: str) -> bool:
    """Run FullScenarioAudit to prepare QA data."""
    if not seed_project.exists():
      logger.warning("Seed project not found: %s", seed_project)
      return False
    env = {**dict(**__import__("os").environ), "BRQOSH_AUDIT_PROFILE": profile}
    try:
      result = subprocess.run(
        ["dotnet", "run", "--project", str(seed_project), "-c", "Release", "--nologo", "-v", "q"],
        cwd=str(repo_root),
        env=env,
        capture_output=True,
        text=True,
        timeout=300,
      )
      if result.returncode != 0:
        logger.warning("Seed exited %s: %s", result.returncode, result.stderr[:500])
      return result.returncode == 0
    except Exception as exc:
      logger.warning("Seed failed: %s", exc)
      return False

  def build_dotnet(self, project: Path, repo_root: Path) -> bool:
    try:
      r = subprocess.run(
        ["dotnet", "build", str(project), "-c", "Release", "-v", "q", "--nologo"],
        cwd=str(repo_root),
        capture_output=True,
        text=True,
        timeout=300,
      )
      return r.returncode == 0
    except Exception as exc:
      logger.warning("Build failed: %s", exc)
      return False
