#!/usr/bin/env python3
"""v14 final cleanup: remove frozen archive/; keep العقل-الذكي/SKILL.md redirect only."""
from __future__ import annotations

import shutil
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ARCHIVE = REPO / ".cursor" / "skills" / "العقل-الذكي" / "archive"
HUB = REPO / ".cursor" / "skills" / "brqosh-skills-hub"


def main() -> None:
    if ARCHIVE.is_dir():
        count = sum(1 for _ in ARCHIVE.rglob("*") if _.is_file())
        shutil.rmtree(ARCHIVE)
        print(f"Removed archive/ ({count} files)")
    else:
        print("archive/ already absent — OK")

    skill_only = REPO / ".cursor" / "skills" / "العقل-الذكي" / "SKILL.md"
    if not skill_only.is_file():
        raise SystemExit("Missing العقل-الذكي/SKILL.md redirect")

    ui_ds = REPO / ".cursor" / "skills" / "brqosh-ui-design-system" / "SKILL.md"
    if not ui_ds.is_file():
        raise SystemExit("Missing brqosh-ui-design-system/SKILL.md")

    print("OK — skills v14 final layout verified")


if __name__ == "__main__":
    main()
