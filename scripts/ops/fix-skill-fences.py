#!/usr/bin/env python3
"""Fix broken `` code fences in skill SKILL.md files."""
import os

SKILLS = os.path.join(os.path.dirname(__file__), "..", "..", ".cursor", "skills")
BT = chr(96)
BAD = BT * 2
GOOD = BT * 3
SKIP = ("archive", "brqosh-ui-design-system", "Anthropic")

count = 0
for root, _dirs, files in os.walk(SKILLS):
    if any(s in root for s in SKIP):
        continue
    for name in files:
        if name != "SKILL.md":
            continue
        path = os.path.join(root, name)
        lines = open(path, encoding="utf-8").read().splitlines()
        new = [GOOD if line == BAD else line for line in lines]
        if new != lines:
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(new) + "\n")
            count += 1
            print(path)
print(f"fixed {count}")
