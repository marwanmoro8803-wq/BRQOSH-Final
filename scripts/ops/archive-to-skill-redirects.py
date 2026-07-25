#!/usr/bin/env python3
"""DEPRECATED v14 final — archive/ deleted. Use finalize-skills-v14.py instead."""
from __future__ import annotations

import sys
from pathlib import Path

ARCHIVE = Path(__file__).resolve().parents[2] / ".cursor" / "skills" / "العقل-الذكي" / "archive"

if __name__ == "__main__":
    if ARCHIVE.is_dir():
        print("archive/ still exists — run: python scripts/ops/finalize-skills-v14.py", file=sys.stderr)
        sys.exit(1)
    print("OK — archive/ already removed (v14 final). No action needed.")
