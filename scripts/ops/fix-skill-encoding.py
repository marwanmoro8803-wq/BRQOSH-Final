"""Fix mojibake in SKILL.md files from UTF-8 mis-read/write."""
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SKILLS = REPO / ".cursor" / "skills"

REPLACEMENTS = [
    ("\u00e2\u20ac\u201d", "\u2014"),  # â€" -> em dash (variant)
    ("\u00e2\u20ac\u2014", "\u2014"),  # â€" -> em dash
    ("\u00e2\u2020\u2019", "\u2192"),  # â†' -> arrow
    ("\u00e2\u2020\u2019", "\u2192"),  # duplicate safe
    ("\u0622\u00b7", "\u00b7"),  # آ· -> middle dot
]

def main() -> None:
    count = 0
    for path in SKILLS.rglob("SKILL.md"):
        text = path.read_text(encoding="utf-8")
        orig = text
        for bad, good in REPLACEMENTS:
            text = text.replace(bad, good)
        # common UTF-8 misread patterns as literal sequences
        text = text.replace("â€"", "—")
        text = text.replace("â†'", "→")
        text = text.replace("آ·", "·")
        if text != orig:
            path.write_text(text, encoding="utf-8", newline="\n")
            count += 1
            print(f"fixed {path.relative_to(REPO)}")
    print(f"Fixed {count} files")

if __name__ == "__main__":
    main()
