"""
Chronological Audit Diary — Observation · Reasoning · Action · Result + affective layer.
Writes append-only CHRONICLE.md in real time.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


class ChronicleLogger:
  """Human-like micro-documentation for every atomic interaction."""

  def __init__(self, diary_path: Path):
    self.path = Path(diary_path)
    self.path.parent.mkdir(parents=True, exist_ok=True)
    self._count = 0
    if not self.path.exists():
      self.path.write_text(
        "# BRQOSH Cognitive Audit Chronicle\n\n"
        "_Observation · Reasoning · Action · Result · Affect_\n\n---\n\n",
        encoding="utf-8",
      )

  def entry(
    self,
    *,
    observation: str,
    reasoning: str,
    action: str,
    result: str,
    monologue: str = "",
    affect: str = "",
    screenshot: str | None = None,
    source_hint: str | None = None,
    meta: dict[str, Any] | None = None,
  ) -> int:
    self._count += 1
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f")[:-3] + " UTC"
    lines = [
      f"## Entry {self._count} — `{ts}`\n",
      f"**Observation:** {observation}\n",
      f"**Reasoning:** {reasoning}\n",
      f"**Action:** {action}\n",
      f"**Result:** {result}\n",
    ]
    if monologue:
      lines.append(f"**Inner monologue:** _{monologue}_\n")
    if affect:
      lines.append(f"**Affective read:** {affect}\n")
    if screenshot:
      rel = Path(screenshot).name
      lines.append(f"![capture](screenshots/{rel})\n")
    if source_hint:
      lines.append(f"**Source hint:** `{source_hint}`\n")
    if meta:
      lines.append(f"```json\n{json.dumps(meta, ensure_ascii=False, indent=2)}\n```\n")
    lines.append("---\n\n")
    with self.path.open("a", encoding="utf-8") as f:
      f.write("".join(lines))
    return self._count

  def learning(self, event: str, model_delta: dict[str, Any]) -> None:
    self.entry(
      observation=f"Learning event: {event}",
      reasoning="Updating internal state model from unexpected system response.",
      action="PATCH app_state_model.json",
      result=json.dumps(model_delta, ensure_ascii=False)[:500],
      monologue="Okay, the app lied to me — time to update the mental map.",
      affect="Mild frustration → curiosity",
    )
