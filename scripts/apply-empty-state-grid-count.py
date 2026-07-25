#!/usr/bin/env python3
"""Ensure UiTestAutomation.PublishGridCount follows each DataGrid ItemsSource assignment."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "src" / "Brqosh.Desktop" / "Views" / "Pages"

SKIP_GRIDS = {"ProjectBox", "CustomerCombo", "SupplierCombo", "GroupCombo", "BankCombo", "WarehouseCombo"}
ASSIGN_START = re.compile(r"^(\s*)(?P<grid>(?:\w+Grid|Grid))\.ItemsSource\s*=\s*(?P<rest>.*)$")


def ensure_using(text: str) -> str:
    if "using Brqosh.Desktop.Automation;" in text:
        return text
    lines = text.splitlines()
    insert_at = 0
    for i, line in enumerate(lines):
        if line.startswith("using "):
            insert_at = i + 1
    lines.insert(insert_at, "using Brqosh.Desktop.Automation;")
    return "\n".join(lines) + ("\n" if text.endswith("\n") else "")


def count_for_expr(expr: str) -> str:
    expr = expr.strip().rstrip(";")
    if expr == "null":
        return "0"
    low = expr.lower()
    if low.endswith(".tolist()") or low.endswith(".tolistasync()"):
        return f"{expr}.Count"
    if ".select(" in low and not low.endswith(".count()"):
        return f"{expr}.Count()"
    if expr in {"_rows", "_cart", "_pageRows", "_lastRows", "list", "rows", "displayRows", "maps", "contracts", "orders", "notes", "cheques", "shifts", "receipts", "billings", "agreements", "runs", "docs", "vouchers", "visible", "entity.BoqItems.ToList()"}:
        if "." in expr and expr.endswith(")"):
            return f"{expr}.Count"
        return f"{expr}.Count"
    if expr.startswith("_items.Select"):
        return f"{expr}.Count()"
    if "GetValueOrDefault" in expr:
        return f"({expr}).Count"
    return f"({expr} as System.Collections.ICollection)?.Count ?? 0"


def patch_file(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    out: list[str] = []
    changed = False
    i = 0
    while i < len(lines):
        line = lines[i]
        m = ASSIGN_START.match(line)
        if not m or m.group("grid") in SKIP_GRIDS:
            out.append(line)
            i += 1
            continue

        grid = m.group("grid")
        indent = m.group(1)
        expr_parts = [m.group("rest")]
        i += 1
        while i < len(lines) and not expr_parts[-1].rstrip().endswith(";"):
            expr_parts.append(lines[i])
            i += 1

        block = "\n".join([line] + expr_parts[1:])
        out.extend([line] + expr_parts[1:])

        # peek next non-empty for existing PublishGridCount
        j = len(out)
        while j < len(lines) and not lines[j].strip():
            j += 1
        next_line = lines[j] if j < len(lines) else ""
        if f"PublishGridCount({grid}," in next_line:
            continue

        expr = " ".join(p.strip() for p in expr_parts).rstrip(";").strip()
        if expr == "null":
            cnt = "0"
        elif expr.endswith(".ToList()") or expr.endswith(".ToListAsync()"):
            cnt = f"{expr[:-8]}.Count" if expr.endswith(".ToList()") else count_for_expr(expr)
            # if assigned from variable pattern `var x = ...` not applicable
            cnt = count_for_expr(expr)
        else:
            cnt = count_for_expr(expr)

        out.append(f"{indent}UiTestAutomation.PublishGridCount({grid}, {cnt});")
        changed = True

    new_text = "\n".join(out) + ("\n" if text.endswith("\n") else "")
    if changed:
        new_text = ensure_using(new_text)
        path.write_text(new_text, encoding="utf-8")
    return changed


def main() -> None:
    updated = []
    for path in sorted(PAGES.glob("*Page.xaml.cs")):
        if patch_file(path):
            updated.append(path.name)
    print(f"Updated {len(updated)} files")
    for name in updated:
        print(f"  - {name}")


if __name__ == "__main__":
    main()
