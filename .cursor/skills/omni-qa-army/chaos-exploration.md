# Chaos Exploration — Creative Sabotage Engine

> **Source:** raw prompt L85–97 (ideas only)  
> **Leaks:** Microsoft copilot-cli (critic agent) · xAI grok-build (verify before close)  
> **Roles:** Manual QA (S02) · Beta Tester (S05)  
> **Related:** [exploratory-mindset.md](exploratory-mindset.md) · [roles/role-beta.md](roles/role-beta.md)

---

## Purpose

Drive **curiosity-driven destructive exploration** — not checklist automation. Simulate bored, mischievous, irrational users who break apps in ways scripted tests miss.

---

## Core question (every interaction)

**"What would a brilliantly destructive human tester do here?"**

---

## Sabotage categories (MUST cover per session)

| Category | Examples |
|----------|----------|
| Rapid input | triple-click · hold Enter · spam Tab |
| Spatial chaos | resize to postage-stamp · drag off-screen · extreme DPI |
| Empty-area abuse | click blank regions · double-click static text |
| Flow interruption | Escape mid-operation · Alt+F4 on modals · back during save |
| Weird payloads | emoji-only · PDF paste with control chars · Feb 31 dates · negative cart qty |
| Visual sabotage | 100%–200% DPI · partial off-screen · overlap checks |

---

## Bored User Syndrome (Beta Tester)

| MUST simulate | Document in diary |
|---------------|-------------------|
| Wrong button clicks | frustration note |
| Close dialog mid-operation | data-loss risk |
| Double-click everything | race / duplicate submit |
| Tab obsession through fields | focus order bugs |

---

## Playful payload alchemy (Pen + L10n overlap)

| Type | Example |
|------|---------|
| Unicode chaos | bidi overrides · Tatweel abuse |
| Clipboard abuse | oversized paste · rich text in plain field |
| Locale weirdness | phone with letters · mixed AR/EN |
| Boundary | first/last list item · empty state · max string |

**MUST NOT** use garbage (`test`, `asdf`) on valid-path phase — see exploratory-mindset Q02.

---

## Devil's advocate (Microsoft critic)

Before leaving a screen after sabotage:

1. What weak point did lazy dev likely skip?
2. What would tired user do next?
3. One sentence in chronicle — see [chronicle-protocol.md](chronicle-protocol.md).

---

## Orchestrator integration

| Trigger | Action |
|---------|--------|
| Same screen 3× no progress | boredom interrupt → chaos branch |
| Post-valid-path on form | return with malicious/weird payloads |
| Creative Destruction Synthesis | combine vectors (resize + payload + network yank) when safe |

---

## Finding rules

| Severity | When |
|----------|------|
| Critical | data loss · silent failure · security bypass |
| High | unrecoverable UI state · crash without recovery |
| Medium | confusing UX after chaos · truncated text |
| Low | cosmetic after extreme resize |

**MUST** link each finding to screenshot + repro line — [report-excellence.md](report-excellence.md).

---

## Forbidden

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| Chaos without prior valid-path on same form | Q02 two-phase sequence |
| Sabotage without diary entry | chronicle per action |
| Claim exploit without repro | evidence path |
| Implement `human_navigator.py` without user release | skills govern future code |

---

## Future code owners (when authorized)

`human_navigator.py` · `payload_factory.py` · `audit_orchestrator.py` (boredom interrupt)

---

## Verify

```powershell
Test-Path .cursor/skills/omni-qa-army/chaos-exploration.md
rg "chaos-exploration" .cursor/skills/omni-qa-army/SKILL.md
```
