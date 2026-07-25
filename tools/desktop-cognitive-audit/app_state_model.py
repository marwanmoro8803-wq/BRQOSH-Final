"""
Application state machine model — screens, elements, transitions, dead-ends.
Persisted as app_state_model.json for learning & adaptation.
"""

from __future__ import annotations

import json
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


@dataclass
class UiElementNode:
  identity: str
  control_type: str
  name: str
  automation_id: str | None = None
  handler_hint: str | None = None
  visited: bool = False
  payload_tested: bool = False


@dataclass
class ScreenNode:
  screen_id: str
  title: str
  profile: str
  elements: list[UiElementNode] = field(default_factory=list)
  transitions: list[str] = field(default_factory=list)
  dead_end: bool = False
  crash_count: int = 0


@dataclass
class AppStateModel:
  version: int = 1
  updated: str = ""
  profiles_audited: list[str] = field(default_factory=list)
  screens: dict[str, ScreenNode] = field(default_factory=dict)
  transitions: list[dict[str, str]] = field(default_factory=list)
  anomalies: list[dict[str, Any]] = field(default_factory=list)
  silent_failures: list[dict[str, Any]] = field(default_factory=list)

  def touch(self) -> None:
    self.updated = datetime.now(timezone.utc).isoformat()

  def screen_key(self, profile: str, title: str) -> str:
    return f"{profile}::{title}"

  def get_or_create_screen(self, profile: str, title: str) -> ScreenNode:
    key = self.screen_key(profile, title)
    if key not in self.screens:
      self.screens[key] = ScreenNode(screen_id=key, title=title, profile=profile)
    return self.screens[key]

  def record_transition(self, frm: str, to: str, trigger: str) -> None:
    self.transitions.append({"from": frm, "to": to, "trigger": trigger, "at": datetime.now(timezone.utc).isoformat()})
    if frm in self.screens:
      if to not in self.screens[frm].transitions:
        self.screens[frm].transitions.append(to)

  def mark_dead_end(self, screen_id: str, reason: str) -> None:
    if screen_id in self.screens:
      self.screens[screen_id].dead_end = True
    self.anomalies.append({"screen": screen_id, "type": "dead_end", "reason": reason})

  def record_anomaly(self, screen: str, kind: str, detail: str) -> None:
    self.anomalies.append({"screen": screen, "type": kind, "detail": detail, "at": datetime.now(timezone.utc).isoformat()})

  def record_silent_failure(self, screen: str, action: str, expected: str, actual: str) -> None:
    self.silent_failures.append({
      "screen": screen, "action": action, "expected": expected, "actual": actual,
      "at": datetime.now(timezone.utc).isoformat(),
    })

  def unvisited_screens(self, catalog_titles: list[str], profile: str) -> list[str]:
    visited = {s.title for s in self.screens.values() if s.profile == profile and not s.dead_end}
    return [t for t in catalog_titles if t not in visited]

  def to_dict(self) -> dict[str, Any]:
    return {
      "version": self.version,
      "updated": self.updated,
      "profiles_audited": self.profiles_audited,
      "screens": {k: asdict(v) for k, v in self.screens.items()},
      "transitions": self.transitions,
      "anomalies": self.anomalies,
      "silent_failures": self.silent_failures,
    }

  @classmethod
  def from_dict(cls, data: dict[str, Any]) -> AppStateModel:
    m = cls(version=data.get("version", 1), updated=data.get("updated", ""))
    m.profiles_audited = data.get("profiles_audited", [])
    m.transitions = data.get("transitions", [])
    m.anomalies = data.get("anomalies", [])
    m.silent_failures = data.get("silent_failures", [])
    for k, v in data.get("screens", {}).items():
      m.screens[k] = ScreenNode(
        screen_id=v["screen_id"],
        title=v["title"],
        profile=v["profile"],
        elements=[UiElementNode(**e) for e in v.get("elements", [])],
        transitions=v.get("transitions", []),
        dead_end=v.get("dead_end", False),
        crash_count=v.get("crash_count", 0),
      )
    return m


def load_state(path: Path) -> AppStateModel:
  if path.exists():
    return AppStateModel.from_dict(json.loads(path.read_text(encoding="utf-8")))
  return AppStateModel()


def save_state(model: AppStateModel, path: Path) -> None:
  model.touch()
  path.parent.mkdir(parents=True, exist_ok=True)
  path.write_text(json.dumps(model.to_dict(), ensure_ascii=False, indent=2), encoding="utf-8")
