# Claude Code CLI — Harness & Tools (Cursor Translation)

> **Raw source:** grep sections in [merged_claude_files.md](../../../merged_claude_files.md) only  
> **Index:** [merged-claude-index.yaml](../brqosh-skills-hub/merged-claude-index.yaml)  
> **Cursor map:** [cursor-adaptations.md](../claude-fable-5/cursor-adaptations.md) · [tools-cursor.md](tools-cursor.md)  
> **Language:** English only · binding MUST/MUST NOT

---

## 1. Identity (Cloud Code on this workspace)

Cloud Code agents working on **Program Developer** MUST treat this repo as the product:

| Field | Value |
|-------|-------|
| Primary product | `CognitiveQa/` (.NET 9) |
| Agent law | `.cursor/skills/` + `.cursor/rules/` |
| Onboarding | `docs/CLOUD-CODE-AGENT-GUIDE.md` |
| Verify | `dotnet test CognitiveQa.sln -c Release` |

---

## 2. Harness principles (merged §Harness)

| MUST | Detail |
|------|--------|
| M01 | User-visible text is markdown — write for a teammate catching up |
| M02 | State intent in one sentence before first tool call |
| M03 | Deliverables go in the **final message** — no tool calls after conclusions |
| M04 | Lead with outcome (TLDR first), then supporting detail |
| M05 | Reference code as `file_path:line_number` (clickable in Cloud Code) |
| M06 | Prefer dedicated Read/Grep/Write tools over shell cat/sed |
| M07 | Batch independent tool calls in one response |

| MUST NOT | Detail |
|----------|--------|
| F01 | Arrow-chain shorthand (`A → B → fails`) as sole explanation |
| F02 | Comments that narrate the PR to the reviewer |
| F03 | Retry denied tool calls verbatim — adjust approach |

---

## 3. Autonomous persistence (merged §Context management)

| MUST | Detail |
|------|--------|
| P01 | When enough info exists, **act** — do not re-derive established facts |
| P02 | For reversible follow-on work, **proceed without asking** |
| P03 | Stop only for destructive actions or scope the user must decide |
| P04 | Problem reports / questions → assess and report; **no fix until asked** |
| P05 | Do not end turn on plans or promises — execute with tools first |
| P06 | Before state-changing commands, verify evidence supports that action |

**Cursor equivalent:** Persistence law in `brqosh-supreme-cortex.mdc` — carry through verify in-turn.

---

## 4. Tool mapping (merged → Cursor)

| Claude Code CLI | Cursor Agent | Notes |
|-----------------|--------------|-------|
| `Agent` | `Task` subagent | explore · generalPurpose · shell |
| `Skill` | Read `SKILL.md` | User `/skill-name` = attach skill first |
| `TaskCreate` / `TaskUpdate` | `TodoWrite` | 3+ step non-trivial tasks |
| `EnterPlanMode` | `SwitchMode` plan | Architecture · 5+ modules · schema |
| `Bash` | `Shell` | `;` on PowerShell — not `&&` |
| `Read` | `Read` | offset/limit for large files |
| `Edit` / `Write` | `StrReplace` / `Write` | Prefer edit over create |
| `WebSearch` | `WebSearch` | Post-cutoff facts |
| `WebFetch` | `WebFetch` | URL content |
| `Explore` agent | `Task` explore readonly | Wide discovery |
| `Plan` agent | Plan mode + brqosh-analysis | Design before code |

### Skill tool law (merged §Skill)

| MUST | Detail |
|------|--------|
| S01 | When user invokes `/skill-name`, read that SKILL.md **before** other actions |
| S02 | Scan `agent_skills` + `.cursor/skills/` for plausibly-relevant routers |
| S03 | NEVER mention a skill without reading it |
| S04 | Do not invent skill names not in the available list |

---

## 5. Plan mode (merged §EnterPlanMode)

**MUST use Plan Mode** when:

- New feature with UX/architecture choices
- Multiple valid approaches (cache, auth, state)
- Migration, schema, or posting chain
- 5+ modules or API behavior removal
- Unclear requirements needing exploration

**MUST NOT use Plan Mode** for:

- Single obvious typo or one-line fix
- User gave explicit step-by-step instructions
- Pure explanation (Inquiry)

**Cursor:** `SwitchMode` target `plan` · or `brqosh-analysis` skill for SCHOLAR depth.

---

## 6. File paths (merged → workspace)

| Claude Code path | Program Developer equivalent |
|------------------|------------------------------|
| `/mnt/user-data/uploads` | User attachments + workspace files |
| `/home/claude` (scratch) | Prefer tracked repo paths |
| `/mnt/user-data/outputs` | `dist/evidence/` · `audit_reports/` · committed docs |
| `/mnt/skills/public/<name>/` | `.cursor/skills/<name>/SKILL.md` |

**MUST:** Deliverables for Cloud Code readability go to git-tracked paths (README, docs/, skills/).

---

## 7. Subagent spawn contract (merged §Agent)

When spawning subagents, prompt MUST include:

1. Absolute repo path
2. Goal and success criteria
3. Files allowed to mutate (or readonly)
4. Verify command expected

**Parallel:** Multiple independent `Task` calls in one message.

**FORBIDDEN:** Parallel agents mutating the same files.

---

## 8. Verify

```powershell
Test-Path merged_claude_files.md
Test-Path .cursor/skills/brqosh-skills-hub/merged-claude-index.yaml
rg "claude-code-cli" .cursor/skills/supreme-programming-mastery/SKILL.md
```

**Related:** [agent-workflow.md](agent-workflow.md) · [skills-first.md](skills-first.md) · [ai-dev-platform/SKILL.md](../ai-dev-platform/SKILL.md)
