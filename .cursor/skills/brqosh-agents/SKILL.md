---
name: brqosh-agents
description: >-
  Multi-agent orchestration for BRQOSH: explore subagents, parallel readonly
  review, background builds, Task tool patterns. Use for wide codebase search,
  parallel standards+spec review, or long-running verify.
---

# BRQOSH Agents

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Patterns

| Task | Pattern |
|------|---------|
| Wide explore | explore subagent, readonly |
| Long build/test | background shell |
| Standards + Spec | parallel readonly agents |

**Never parallel-mutate same files.**

## Modules

| Module | When |
|--------|------|
| [reference.md](reference.md) | Task tool · MCP · background · todos |
| [session-protocol.md](session-protocol.md) | Every session mandatory behavior |
| [harness.md](harness.md) | Full agent cycle · Plan Mode · compaction |
| [dream-team.md](dream-team.md) | Large wave · CEEF · adversarial review |
| [council-intelligence-matrix.md](council-intelligence-matrix.md) | 14 councils + 12 Shura + 279-leak synthesis |

## Reference

Start: [reference.md](reference.md) · Large tasks: [dream-team.md](dream-team.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-AGENTS

## Default path

```
SKILL → reference.md → [harness.md | dream-team.md] → Task subagent → parent VERIFY
```

---

### `SKILL.md` · L0 · 1/5

| | |
|--|--|
| **TRIGGER** | Wide explore · parallel review · long build |
| **NEXT** | reference.md |
| **EXIT** | Delegation vs direct decision |
| **TIME** | 20s |

---

### `session-protocol.md` · L1 · 2/5 — every session

| | |
|--|--|
| **TRIGGER** | **Every** session start |
| **SOURCE** | archive/session-omnipotence-v8.md (merged v14) |
| **EXIT** | Before/during/after checklists applied |
| **TIME** | 30s |

---

### `reference.md` · L2 · 3/5

| | |
|--|--|
| **TRIGGER** | Task tool · MCP · background patterns |
| **ANTI** | Parallel mutate same files |
| **TIME** | 60s |

---

### `harness.md` · L2 · 4/5

| | |
|--|--|
| **TRIGGER** | Multi-phase delivery · Claude Code-style cycle |
| **SOURCE** | archive/agent-harness-elite-v7.md (merged) |
| **TIME** | 90s |

---

### `dream-team.md` · L2 · 5/5

| | |
|--|--|
| **TRIGGER** | CEEF wave · 5+ modules · comprehensive PR review |
| **SOURCE** | archive/dream-team-supremacy-v8.md (merged) |
| **EXIT** | Parent VERIFY · no parallel mutate |
| **TIME** | 120s |
