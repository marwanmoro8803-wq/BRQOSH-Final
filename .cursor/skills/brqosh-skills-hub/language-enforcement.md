# Skill Language Enforcement Law — BRQOSH (v19 Strict)

> **Authority:** Binding for every file under `.cursor/skills/`. Violations are skill-quality blockers.  
> **Scope:** All `SKILL.md` routers and reference modules (`.md`, `.yaml` in skills tree).  
> **Exception:** User-facing UI copy examples ONLY — `LocaleManager.T("ar", "en")` pairs in UI skills.

---

## Immutable laws

| ID | Law | Violation |
|----|-----|-----------|
| L01 | **English only** in skill bodies — instructions, tables, headers, examples | Arabic or mixed prose in skills |
| L02 | **Binding tone** — MUST / MUST NOT / FORBIDDEN / REQUIRED | soft suggestions without imperative |
| L03 | **No chat prose** in skills — principles only, ≤5 lines per lesson | conversation dumps in SKILL.md |
| L04 | **No leak paste** — grep principles, paraphrase, cite ≤15 words once | bulk leak text in modules |
| L05 | **Paths in English** in skill prose — `reports/` not Arabic folder names | Arabic path aliases in instructions |
| L06 | **Bilingual UX is runtime** — `LocaleManager.T` in app, not in agent law files | Arabic agent instructions |

---

## MUST (every skill author / agent edit)

| ID | Requirement |
|----|-------------|
| M01 | Write new skill content in sharp operational English |
| M02 | Use tables for laws, anti-patterns, routing — not long narrative |
| M03 | Lead with purpose, trigger, exit criteria, verify command |
| M04 | Link to this file from every `SKILL.md` router |
| M05 | Grep `[\u0600-\u06FF]` under `.cursor/skills/` before claiming English-only |
| M06 | Preserve technical IDs (C01–C14, S01–S12, MEM-NNN, CEEF-NNN) unchanged |

---

## MUST NOT

| ID | Forbidden |
|----|-----------|
| F01 | Arabic paragraphs in skill modules |
| F02 | Emoji-heavy or motivational fluff in law files |
| F03 | "Maybe" / "consider" without a default action |
| F04 | Duplicate full paragraphs across 5+ skill files |
| F05 | Weaken MUST to "nice to have" during translation |

---

## Tone standard (match `brqosh-ui-design-system/enforcement.md`)

```
GOOD: "MUST run dotnet test before claiming done."
BAD:  "You might want to run tests if possible."
GOOD: "FORBIDDEN: bulk-load leak corpus in session."
BAD:  "It's better not to load all leaks."
```

---

## Verify

```powershell
rg "[\u0600-\u06FF]" .cursor/skills/
# Expected: no matches in skill bodies
```

**Related:** `brqosh-ui-design-system/enforcement.md` · `leak-corpus-index.yaml` · `LEARNING-INDEX.yaml`
