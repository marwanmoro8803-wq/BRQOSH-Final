# Leak Platform Synthesis — 279 Files → Program Developer

> **Index:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml) · **Rule:** grep principles only · never bulk-load

---

## Corpus

```
system_prompts_leaks-main/system_prompts_leaks-main/  (279 files)
```

**MUST:** extract ≤5 line principle per insight · route to ONE primary module.

---

## Family → platform module map

| Family | Count | Platform destination | Key patterns |
|--------|-------|---------------------|--------------|
| **Anthropic** | 119 | fable-omni-core · verify · deep-research 3b | classify · skills · adversarial |
| **OpenAI** | 88 | agent-orchestration · sdlc-pipeline | multi_agent · plan_mode · persistence |
| **Google** | 22 | sdlc-pipeline · deep-cognition | Inquiry/Directive · explore first |
| **xAI** | 11 | agent-orchestration · shura F00 | spawn_subagent · plan_mode · steelman |
| **Cursor** | 1 | supreme-programming tools-cursor | parallel tools · read-before-edit |
| **Microsoft** | 5 | brqosh-review · agent critic | devil's advocate |
| **Perplexity** | 3 | agent-orchestration | wide_research · fan-out |
| **Misc** | 23 | skill-studio · universal-coding | hermes skill_manage · multi-agent |
| **Meta** | 1 | human-intelligence evenhandedness | steelman minority |

---

## Top principles by platform concern

### Multi-agent (OpenAI Codex gpt-5.5-full · grok-build · Misc/hermes)

| Principle | Platform file |
|-----------|---------------|
| spawn with full context contract | agent-orchestration.md |
| parallel only when independent | agent-orchestration.md |
| parent integrates before done | agent-orchestration.md |
| save workflow as skill not memory | skill-studio.md |

### Plan Mode (OpenAI plan_mode · grok enter_plan_mode)

| Principle | Platform file |
|-----------|---------------|
| explore before ask | sdlc-pipeline.md |
| decision-complete plan | sdlc-pipeline.md |
| no mutate in plan | sdlc-pipeline.md |

### Verify (Anthropic verify · grok making_code_changes)

| Principle | Platform file |
|-----------|---------------|
| verify before claiming complete | verify-gates.md |
| repro-before-fix | brqosh-debug |
| findings-first severity | brqosh-review |

### Adversarial (Anthropic deep-research · Microsoft copilot critic)

| Principle | Platform file |
|-----------|---------------|
| 3-vote refute critical claims | shura-intelligence-core |
| devil's advocate on proposals | shura S02 · design phase |

### Skills (Anthropic bundled-skills · Hermes)

| Principle | Platform file |
|-----------|---------------|
| skills before code | supreme-programming skills-first |
| patch outdated skills immediately | skill-studio.md |
| procedures in skills not memory | fable context-continuity |

---

## Grep workflow (MANDATORY)

```powershell
# 1. Find pattern
rg -n "spawn_subagent" system_prompts_leaks-main/system_prompts_leaks-main/

# 2. Read ONE file section (~50-200 lines)
# 3. Extract principle (paraphrase)
# 4. Write to target module
# 5. Append leak-synthesis.md wave note
```

---

## Shura + leak fusion

Shura seats already map leak families in `SeatIntelligenceRegistry.cs`. Platform extends via:
- new evidence loaders for platform gates
- new decisions D8+ for platform launch

---

## Anti-patterns

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| Load 279 files in session | grep one file |
| Paste leak into SKILL.md | paraphrase to module |
| Claim "absorbed all leaks" without module | cite file + principle |
| Duplicate principle in 5 skills | ONE primary per LEARNING-INDEX |

**Related:** [leak-synthesis.md](../brqosh-skills-hub/leak-synthesis.md) · [claude-fable-5/SKILL.md](../claude-fable-5/SKILL.md)
