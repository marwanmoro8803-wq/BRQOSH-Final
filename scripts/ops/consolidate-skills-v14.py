#!/usr/bin/env python3
"""v14: remove redundant ROADMAP stubs — SKILL.md is sole contract."""
import os
from pathlib import Path

SKILLS = Path(__file__).resolve().parents[2] / ".cursor" / "skills"
REMOVE_NAMES = {"ROADMAP.md", "MASTER-ROADMAP.md"}
EXTRA = [
    SKILLS / "العقل-الذكي" / "claude-fable-5.md",
]

removed = []
for path in SKILLS.rglob("*"):
    if path.is_file() and path.name in REMOVE_NAMES:
        path.unlink()
        removed.append(str(path.relative_to(SKILLS)))
for path in EXTRA:
    if path.is_file():
        path.unlink()
        removed.append(str(path.relative_to(SKILLS)))

print(f"removed {len(removed)} files")
for r in sorted(removed):
    print(f"  {r}")
