# SDLC Pipeline — AI-Native Software Delivery

> **Workflow base:** [agent-workflow.md](../supreme-programming-mastery/agent-workflow.md) · **Plan:** Codex plan_mode.md · **Analysis:** requirements.md

---

## 7 phases (platform SDLC)

```
① DISCOVER  — classify · grep · skills · PROJECT-MEMORY
② DEFINE    — requirements · success criteria · out-of-scope
③ DESIGN    — architecture · interfaces · adversarial review
④ PLAN      — decision-complete spec (Plan Mode if fork)
⑤ BUILD     — smallest diff · conventions · DI
⑥ VERIFY    — test · build · lint · evidence
⑦ RELEASE   — Shura if launch · MEM · LEARN
```

---

## Phase entry criteria

| Phase | Enter when | Exit artifact |
|-------|------------|---------------|
| DISCOVER | any new task | classification + files list |
| DEFINE | ambiguous or 3+ modules | requirements table |
| DESIGN | architecture touch | ADR or architecture.md section |
| PLAN | 5+ modules · fork · migration | approved plan |
| BUILD | plan approved or BLITZ fix | code diff |
| VERIFY | behavior change | test output path |
| RELEASE | wave or launch | CEEF/MEM + optional Shura |

---

## Plan Mode (Codex — strict)

**MUST enter Plan when:**
- multiple valid architectures
- 5+ module impact
- schema · auth · API removal
- user requests plan only

**Plan MUST be decision-complete:**
- goal + success criteria
- interfaces / I/O
- edge cases
- test + acceptance
- verify commands

**FORBIDDEN in Plan:** file mutations · "doing the work"

**Allowed in Plan:** Read · grep · dry-run · tests that don't edit tracked files

---

## BLITZ path (skip full SDLC)

| Signal | Path |
|--------|------|
| typo · obvious test red | DISCOVER → BUILD → VERIFY |
| single file · known fix | max 3 tools L0 |

---

## Requirements template (DEFINE)

| # | Question |
|---|----------|
| R1 | What is the user-visible outcome? |
| R2 | Success criteria (measurable)? |
| R3 | In scope / out of scope? |
| R4 | Constraints (stack · time · deps)? |
| R5 | Verify command? |
| R6 | Shura required? (launch / architecture) |

---

## Design adversarial pass (DESIGN)

Before BUILD on architecture:
1. S02 skeptic: what evidence is missing?
2. C07 security: attacker view if auth/API
3. C02 architecture: boundaries · no over-abstract

Optional: Round 3b pattern on critical claims.

---

## VERIFY binding (phase ⑥)

| Change type | Minimum verify |
|-------------|----------------|
| C# / .NET | `dotnet build` + `dotnet test` |
| Script | run script · exit 0 |
| Skill only | Arabic grep + link check |
| UI | build + audit script if exists |

**MUST NOT** claim phase ⑦ without phase ⑥ proof.

---

## RELEASE + Shura

| Trigger | Action |
|---------|--------|
| Comprehensive analysis saved | agent runs `run-shura-council.ps1` |
| Launch / pricing / GTM | Shura mandatory |
| Typo fix | no Shura |

**Related:** [verify-gates.md](verify-gates.md) · [shura-governance.md](shura-governance.md)
