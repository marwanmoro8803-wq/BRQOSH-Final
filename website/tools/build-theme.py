#!/usr/bin/env python3
"""Builds website/dist/brqosh-theme-<ver>.zip for upload via wp-admin → Appearance → Themes.

Refuses to package when:
  * any PHP file fails `php -l`;
  * website/tests/validation-test.php fails;
  * the newest release in inc/content.php is not 1.1.<version.txt> (the site
    would advertise a different version than the product being shipped).

Usage: python3 website/tools/build-theme.py
"""
from __future__ import annotations

import pathlib
import re
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
THEME = ROOT / "website" / "wordpress-theme" / "brqosh"
DIST = ROOT / "website" / "dist"


def fail(msg: str) -> None:
    print(f"build-theme: {msg}", file=sys.stderr)
    sys.exit(1)


def main() -> None:
    for php in sorted(THEME.rglob("*.php")):
        r = subprocess.run(["php", "-l", str(php)], capture_output=True, text=True)
        if r.returncode != 0:
            fail(f"syntax error in {php.relative_to(ROOT)}:\n{r.stdout}{r.stderr}")

    r = subprocess.run(["php", str(ROOT / "website" / "tests" / "validation-test.php")], capture_output=True, text=True)
    print(r.stdout.strip())
    if r.returncode != 0:
        fail("validation tests failed:\n" + r.stderr)

    build = (ROOT / "version.txt").read_text(encoding="utf-8").strip()
    expected = f"1.1.{build}"
    content = (THEME / "inc" / "content.php").read_text(encoding="utf-8")
    latest = re.search(r"function brqosh_releases\(\).*?'version'\s*=>\s*'([\d.]+)'", content, re.S)
    if not latest or latest.group(1) != expected:
        fail(f"newest release in inc/content.php is {latest.group(1) if latest else '?'}, product is {expected}. "
             "Add the release notes to brqosh_releases() first.")

    theme_ver = re.search(r"^Version:\s*(\S+)", (THEME / "style.css").read_text(encoding="utf-8"), re.M).group(1)
    DIST.mkdir(exist_ok=True)
    out = DIST / f"brqosh-theme-{theme_ver}.zip"
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(THEME.rglob("*")):
            if f.is_file() and not f.name.startswith("."):
                z.write(f, pathlib.Path("brqosh") / f.relative_to(THEME))
    print(f"built {out.relative_to(ROOT)} ({out.stat().st_size // 1024} KB) — product {expected}")


if __name__ == "__main__":
    main()
