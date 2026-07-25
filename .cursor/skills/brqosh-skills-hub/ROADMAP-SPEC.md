# ROADMAP-SPEC — Engineering Contract Per File (v14)

> Strict engineering team standard: every file = operational contract.  
> **v14:** One `SKILL.md` per package (+ reference modules). No separate ROADMAP files.  
> **Fable source:** `../claude-fable-5.md` — grep tags only. Never load full prompt.

## Mandatory Fields (per file)

| Field | Meaning |
|-------|---------|
| `id` | Unique `skill:filename` |
| `layer` | L0 hub · L1 omni · L2 domain · L3 reference |
| `step` | Order in path `N/M` |
| `trigger` | When you **must** read the file |
| `skip_when` | When you **must not** read it |
| `prereq` | Files before it |
| `next` | Files after it (default) |
| `fable_tag` | Section in claude-fable-5.md (grep) |
| `exit_criteria` | Exit checklist |
| `anti_patterns` | Common failures |
| `time_budget` | Expected read time |
| `verify` | How to prove you applied the file |

## Read Layers

```
L0  SKILL.md (roadmap embedded)     — always — section ## Engineering Roadmap
L1  operating-manual / universal    — every message
L2  domain SKILL engineering path   — by scenario
L3  reference deep                  — on demand only
```

## Machine Index

Large skills ship `FILE-ROADMAP.yaml` for scripts and agents.

## Fable Rule

| DO | DON'T |
|----|-------|
| Operational synthesis in modules | Paste 3800 lines into SKILL.md |
| Grep tag → read section | Load claude-fable-5 whole |
| FILE-ROADMAP.yaml for order | Guess read order |

## Standard Paths

| Path ID | Sequence |
|---------|----------|
| `P-OMNI` | SKILL → operating-manual → universal-understanding → [research] → verify |
| `P-CODE` | skills-first → agent-workflow → twelve-axes → languages/* → testing |
| `P-BRQOSH-BUG` | P-CODE → brqosh-debug → engineering → dotnet test |
| `P-BRQOSH-CLOSE` | brqosh-closure → STATUS.yaml → gates → CEEF |
| `P-RESEARCH` | universal → research → copyright → synthesize |
