# Agent Orchestration — Multi-Agent Supreme

> **Harness:** [harness.md](../brqosh-agents/harness.md) · **Synthesis:** Codex multi_agent_v1 · grok spawn_subagent · Perplexity wide_research · Hermes workflows

---

## Principle (279-leak merge)

```
Orchestration = classify → delegate with contract → parallel readonly → persist → verify → close agents
```

**VALUE:** speed from parallelism WITHOUT chaos from missing context.

---

## When to spawn (Codex + grok)

| Trigger | Action |
|---------|--------|
| User asks parallel agents | single message · multiple spawns |
| Wide repo discovery | Task explore readonly |
| Independent subtasks | spawn per task · not one mega-agent |
| Same-file mutation | **FORBIDDEN** parallel — sequential only |
| Skill reading for parent | parent reads SKILL — do not delegate skill interpretation |

---

## Subagent prompt contract (MANDATORY)

Subagent does NOT see parent chat. Every spawn MUST include:

| Field | Content |
|-------|---------|
| **Goal** | one sentence outcome |
| **Paths** | absolute paths · patterns |
| **Constraints** | readonly · no commit · scope limits |
| **Success** | test names · PASS · structured return |
| **Context** | stack · conventions · verify command |

**Leak source:** docker-gordon absolute paths · Codex resume_agent context.

---

## Agent types (map to Cursor Task tool)

| Type | Use | Mode |
|------|-----|------|
| `explore` | wide discovery | readonly |
| `generalPurpose` | multi-step implementation | write |
| `shell` | git · build · test | write |
| `bugbot` | review branch/diff | readonly |
| `security-review` | security audit | readonly |
| Parent | triage · integrate · verify | full |

---

## Parallel rules (Cursor + Perplexity)

| DO | DON'T |
|----|-------|
| parallel grep/Read/git status | parallel StrReplace same file |
| batch readonly explores | spawn 10 agents for typo |
| wide_research pattern for 10+ entities | manual one-by-one |
| Await long builds | poll in tight loop |

---

## Persistence (Codex 5.5 — binding)

| Situation | MUST |
|-----------|------|
| User says fix | implement through verify |
| Test fails | diagnose · retry |
| Long command | background + Await |
| Blocker | alternate approach before ask user |

---

## Plan vs execute (grok enter_plan_mode)

| Condition | Mode |
|-----------|------|
| Genuine architecture ambiguity | Plan Mode · readonly explore |
| User said implement/fix | execute unless 5+ module fork |
| Clear path in request | skip plan theatre |

**Plan Mode:** explore allowed · mutate FORBIDDEN until approval.

---

## Close / resume (Codex multi_agent_v1)

```
spawn → work → wait → integrate results → close_agent
```

Parent MUST integrate subagent output before claiming done. Never orphan agents.

---

## Anti-patterns

| FORBIDDEN | WHY |
|-----------|-----|
| Subagent without paths | wrong file · wasted turn |
| Theatre debate in chat | use Shura tool for consensus |
| Delegate verify to user | agent runs tests |
| 14 councils in one message | max 3 councils |

**Related:** [sdlc-pipeline.md](sdlc-pipeline.md) · [agent-workflow.md](../supreme-programming-mastery/agent-workflow.md)
