#!/usr/bin/env python3
"""Build leak-corpus-index.yaml from system_prompts_leaks-main → skill routing."""
from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
LEAKS = REPO / "system_prompts_leaks-main" / "system_prompts_leaks-main"
OUT = REPO / ".cursor" / "skills" / "brqosh-skills-hub" / "leak-corpus-index.yaml"

# Family → primary BRQOSH skill modules (principle extraction targets)
FAMILY_MAP: dict[str, list[str]] = {
    "Anthropic": [
        "fable-omni-core/operating-manual.md",
        "human-intelligence-mastery/research-intelligence.md",
        "brqosh-review/code-review.md",
        "brqosh-review/security.md",
        "brqosh-agents/harness.md",
    ],
    "OpenAI": [
        "supreme-programming-mastery/agent-workflow.md",
        "supreme-programming-mastery/tools-cursor.md",
        "brqosh-engineering/engineering.md",
        "brqosh-communication/human-parity.md",
    ],
    "Google": [
        "brqosh-agent-protocol/protocol.md",
        "brqosh-agents/reference.md",
        "brqosh-analysis/deep-cognition.md",
    ],
    "xAI": [
        "brqosh-agents/reference.md",
        "brqosh-agents/session-protocol.md",
        "fable-omni-core/ai-supremacy.md",
    ],
    "Cursor": [
        "supreme-programming-mastery/tools-cursor.md",
        "supreme-programming-mastery/agent-workflow.md",
    ],
    "Microsoft": [
        "brqosh-engineering/tools.md",
        "brqosh-review/code-review.md",
    ],
    "Perplexity": [
        "human-intelligence-mastery/research-intelligence.md",
        "brqosh-agents/reference.md",
    ],
    "Meta": ["human-intelligence-mastery/evenhandedness.md"],
    "Qwen": ["supreme-programming-mastery/polyglot-pitfalls.md"],
    "Misc": ["brqosh-analysis/deep-cognition.md", "universal-coding/reference.md"],
}


def family_for(path: Path) -> str:
    rel = path.relative_to(LEAKS)
    if rel.parts:
        return rel.parts[0]
    return "Misc"


def rel_leak(path: Path) -> str:
    return str(path.relative_to(LEAKS)).replace("\\", "/")


def main() -> None:
    if not LEAKS.is_dir():
        raise SystemExit(f"Leak corpus not found: {LEAKS}")

    files = sorted(LEAKS.rglob("*.md")) + sorted(LEAKS.rglob("*.txt"))
    by_family: dict[str, list[str]] = {}
    for f in files:
        fam = family_for(f)
        by_family.setdefault(fam, []).append(rel_leak(f))

    lines = [
        "# Leak corpus index — system_prompts_leaks-main (287 files)",
        "# Grep-only source. Principles merged into domain skills — never load bulk in sessions.",
        "",
        f"corpus_path: system_prompts_leaks-main/system_prompts_leaks-main",
        f"total_files: {len(files)}",
        "version: v15",
        'updated: "2026-07-10"',
        "",
        "rule: |",
        "  Extract principles only into .cursor/skills/ modules.",
        "  Never paste leak text into SKILL.md.",
        "  Use this index to grep a specific family file when deepening a module.",
        "",
        "family_routing:",
    ]
    for fam in sorted(by_family):
        targets = FAMILY_MAP.get(fam, FAMILY_MAP["Misc"])
        lines.append(f"  {fam}:")
        lines.append(f"    count: {len(by_family[fam])}")
        lines.append("    merge_into:")
        for t in targets:
            lines.append(f"      - {t}")

    lines.append("")
    lines.append("files_by_family:")
    for fam in sorted(by_family):
        lines.append(f"  {fam}:")
        for rel in by_family[fam]:
            lines.append(f"    - {rel}")

    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {OUT} ({len(files)} files, {len(by_family)} families)")


if __name__ == "__main__":
    main()
