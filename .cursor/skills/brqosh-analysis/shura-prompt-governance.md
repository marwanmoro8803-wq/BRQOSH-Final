# Shura Prompt Governance — Raw QA Prompt via Council

> **Tool:** `ShuraCouncil/` · **Law:** [prompt-source-law.md](../omni-qa-army/prompt-source-law.md)  
> **SAVE:** `dist/evidence/prompt-governance/CEEF-PROMPT-001.md`  
> **Leaks:** `system_prompts_leaks-main/` via [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)

---

## Purpose

Shura v2 **governs** the raw QA prompt file — converts ideas into **verified council decisions** without implementing prompt code.

```
RAW PROMPT → ANALYSIS SAVE → SHURA (DP1–DP5) → SKILLS ONLY → USER RELEASE → (future code)
```

---

## When to run

| Trigger | Action |
|---------|--------|
| After omni-qa-prompt-analysis update | SAVE CEEF-PROMPT-001 → run council |
| User changes prompt-source-law | re-run council |
| Before any prompt implementation wave | council MUST approve DP1 lifted |

---

## Pipeline

```
1. Read omni-qa-army skills + prompt-source-law
2. Grep leaks (35-file list in analysis) — principles only
3. SAVE CEEF-PROMPT-001.md
4. Agent runs: .\ShuraCouncil\run-shura-council.ps1
5. Read council-*.md + exit code
6. MEM row type: decision
```

**User does NOT run Shura manually** — agent runs per `brqosh-shura-auto.mdc`.

---

## Workspace mode

When `PROJECT.yaml` exists, Shura enters **ProgramDeveloper** mode:

| Evidence loaded | Source |
|-----------------|--------|
| Prompt file exists | repo root `*.txt` |
| omni-qa-army module counts | `.cursor/skills/omni-qa-army/` |
| prompt-source-law | skill file |
| cognitive-qa absent | directory check |
| leak corpus file count | `system_prompts_leaks-main/` |

---

## Council decisions (DP)

| ID | Title | P0 |
|----|-------|-----|
| DP1 | Ideas only — no prompt code without user | yes |
| DP2 | omni-qa-army skills foundation sufficient | yes |
| DP3 | Defer P2/P3 engines and launcher | no |
| DP4 | Leak grep path approved | no |
| DP5 | Shura governs prompt via evidence | yes |

---

## Seat mapping (prompt governance)

| Seat | Prompt focus | Skills |
|------|--------------|--------|
| S01 | skills champion | omni-qa-army |
| S02 | skeptic — no early code | prompt-source-law |
| S03 | QA verify — roles count | report-excellence |
| S06 | harness — Shura + leak index | shura-intelligence-core |
| S10 | CEO focus — no scope creep | ai-dev-platform |
| S12 | no false framework closure | brqosh-closure |

---

## Adversarial (3b)

Claims `C-PROMPT-*` — voters S02 · S03 · S12 · 2/3 refute kills.

---

## Immutable laws

| ID | Law |
|----|-----|
| SPG01 | Council before prompt code wave |
| SPG02 | CEEF SAVE before `run-shura-council.ps1` |
| SPG03 | exit 0 required for "skills phase approved" |
| SPG04 | DP1 remains until user release phrase |

---

## Verify

```powershell
.\ShuraCouncil\run-shura-council.ps1
# exit 0 = prompt governance consensus
```

**Related:** [shura-council-protocol.md](shura-council-protocol.md) · [omni-qa-prompt-analysis.md](../ai-dev-platform/omni-qa-prompt-analysis.md)
