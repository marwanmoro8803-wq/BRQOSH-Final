# Design Enforcement Law — BRQOSH (v18 Strict)

> **Authority:** This file is binding for every agent UI task. Violations are closure blockers.  
> **Language:** English only in skill modules. User-facing strings remain bilingual via `LocaleManager.T`.  
> **Precedence:** user explicit words → `BrqoshTheme.xaml` → `MASTER.md` → `pages/*.md` → this stack → agent judgment.

---

## Immutable laws (never override)

| ID | Law | Violation |
|----|-----|-----------|
| L01 | **Financial Truth First** — UI serves posting, audit, tabular truth | cosmetic mask on financial error |
| L02 | **No fake live data** — mock/sample MUST be labeled or use EmptyState | sample rows as production |
| L03 | **No intelligence before data** — KPI/AI widgets require real query | decorative AI dashboard |
| L04 | **No expansion before trust** — no vanity modules before posting integrity | KPI without service |
| L05 | **Operational ERP only** — not marketing, not landing, not hero | hero section in ERP |
| L06 | **Preserve existing design system** — do not invent new visual identity | AI-slop palette override |
| L07 | **Smallest surgical diff** — targeted edits only unless redesign requested | drive-by redesign |
| L08 | **Verify before done** — AR + EN + dark + audit when substantive | claim complete without proof |

---

## MUST (mandatory every UI change)

| ID | Requirement |
|----|-------------|
| M01 | Read `program-design-mastery.md` before any new screen or design decision |
| M02 | Read `design-system/pages/<screen>.md` before XAML for that screen |
| M03 | Use `FindResource` / `UiResources` — never `Resources["Key"]` on UserControl |
| M04 | Use `LocaleManager.T(ar, en)` for every visible string |
| M05 | Use `BrqoshTheme.xaml` keys only — no inline hex in XAML |
| M06 | DataGrid row height **44px** unless page spec overrides |
| M07 | Tabular figures on all financial amounts |
| M08 | Monogram nav/KPI badges — **no emoji** |
| M09 | `Cursor=Hand` on all clickables |
| M10 | `FocusVisualStyle` on all interactive controls |
| M11 | EmptyState when list/grid has no data |
| M12 | Loading state in content area (not full-window flash) |
| M13 | Actionable error message (what · why · fix) — no stack trace to user |
| M14 | RBAC: hide/disable with message — never blank denial screen |
| M15 | FlowDirection from locale — never hardcoded LTR on bilingual pages |
| M16 | Test matrix: AR dark · AR light · EN dark · EN light before closure |
| M17 | Run `ui-ux-pro-max-audit.ps1` after substantive UI change |
| M18 | Shell pattern: **shell → module → document** |
| M19 | Summary before detail on dashboards and documents |
| M20 | Chart procedure: **form before color** — see `visual-effects.md` |
| M21 | Read `print-document-system.md` before any PDF/print change |
| M22 | ZATCA simplified print MUST embed scannable QR — not text placeholder |

---

## MUST NOT (hard forbidden)

| ID | Forbidden | Use instead |
|----|-----------|-------------|
| F01 | Marketing hero sections | immediate work screen |
| F02 | Card inside card | single ContentCard + grid |
| F03 | Section as floating card | full-width band |
| F04 | AI purple/pink gradients | BrqoshTheme teal/slate/gold |
| F05 | Glassmorphism on DataGrid cells | flat readable surface |
| F06 | Neon/crypto aesthetic | trust palette |
| F07 | Emoji icons in ERP | NavIconFactory monogram |
| F08 | Dual-axis charts | two charts or indexed series |
| F09 | Recolor series on filter | color follows entity |
| F10 | 9th generated chart hue | fold to Other or use table |
| F11 | Fake sparkline/KPI data | empty or real series |
| F12 | In-app tutorial paragraphs | discoverable UI + tooltips |
| F13 | Bounce/spring on Post/Save | 150–200ms ease-out only |
| F14 | Layout shift on hover/load | stable dimensions |
| F15 | Hardcoded `#333` text | theme text brushes |
| F16 | One-note purple/beige/slate-only palette | balanced MASTER tokens |
| F17 | Present partial UI closure as complete | explicit gaps |
| F18 | Redesign unrelated UI on small request | surgical edit only |
| F19 | Text-only ZATCA QR footer in PDF | QRCoder bitmap in footer |
| F20 | Reuse sales invoice PDF for purchase/credit without type layout | separate DocType template |

---

## Closure gate (12-axis — all required)

| # | Axis | Pass criterion |
|---|------|----------------|
| 1 | Truth | real data or honest empty — never fake live |
| 2 | Purpose | screen job clear in 3 seconds |
| 3 | Density | accountant sees work surface without excess scroll |
| 4 | Hierarchy | summary → detail |
| 5 | RTL | AR + EN correct FlowDirection and alignment |
| 6 | Theme | BrqoshTheme keys · dark mode |
| 7 | States | empty · loading · error · permission |
| 8 | RBAC | actions match permission |
| 9 | A11y | contrast · keyboard · focus |
| 10 | Motion | 150–320ms · no bounce · reduced-motion respected |
| 11 | Charts | form first · no dual-axis · stable colors |
| 12 | Anti-slop | no hero · no emoji · no marketing gradient |

**Priority when conflicts:** Truth > RBAC > RTL > Theme > rest.

---

## Agent workflow (strict sequence)

```
1. CLASSIFY  — new screen | edit | KPI | report | theme
2. READ      — enforcement → program-design-mastery → page spec → MASTER
3. PLAN      — one screen job · token set · layout sentence
4. CHECK     — reject generic AI-default patterns
5. BUILD     — smallest correct diff
6. VERIFY    — 12-axis · audit script · Desktop.Tests if covered
7. REPORT    — state pass/fail per axis · no false closure
```

---

## Corpus rule (leak synthesis)

- Grep `system_prompts_leaks-main/` for **one file at a time** when deepening
- Extract **principles only** into modules — never paste raw leak text
- Index: [leak-source-index.yaml](leak-source-index.yaml)

---

## Module stack (read order)

| # | Module |
|---|--------|
| 0 | **enforcement.md** (this file) |
| 1 | [program-design-mastery.md](program-design-mastery.md) |
| 2 | [design-foundations.md](design-foundations.md) |
| 3 | [interaction-environment.md](interaction-environment.md) |
| 4 | [tokens.md](tokens.md) |
| 5 | [modules.md](modules.md) |
| 6 | [visual-effects.md](visual-effects.md) |
| 7 | [human-interface.md](human-interface.md) |
| 8 | [wpf-patterns.md](wpf-patterns.md) |
| 9 | [print-document-system.md](print-document-system.md) |
