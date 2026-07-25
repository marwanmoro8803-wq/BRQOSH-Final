# Platform Vision — Program Developer

> **Skill:** [SKILL.md](SKILL.md) · **Language:** [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)

---

## North star

Build a **comprehensive, integrated AI software development platform** where humans direct intent and agents execute with **verified evidence** — not vibes.

```
Human intent → classified → planned → multi-agent execution → verified artifact → governed release
```

---

## Core product modules (in-repo target)

| Module | Purpose | Leak pattern source |
|--------|---------|---------------------|
| **IDE Shell** | Editor · terminal · file tree · agent panel | Cursor · Codex tools |
| **Agent Orchestrator** | Route · spawn · parallel · wait · close | Codex multi_agent · grok spawn_subagent |
| **Skill Studio** | Create · patch · route agent skills | Hermes skill_manage · Claude skills |
| **Shura Council** | Evidence-gated consensus on architecture/launch | BRQOSH Shura v2 · deep-research 3b |
| **Project Memory** | Durable facts · decisions · lessons | Fable memory · PROJECT-MEMORY |
| **SDLC Engine** | Req → design → code → test → deploy | Codex plan_mode · OpenCode 5-step |
| **Leak Lab** | Grep corpus → extract principles → skills | leak-corpus-index · synthesis law |
| **Verify Hub** | Build · test · lint · gates · evidence | Anthropic verify · grok verify-before-done |

---

## Value equation

```
VALUE = (intent_clarity × agent_depth × verified_evidence) ÷ (theatre + unverified_claims)
```

---

## In scope (v1)

| MUST ship in-repo |
|-------------------|
| Skill stack (this workspace `.cursor/skills/`) |
| ShuraCouncil executable governance |
| Agent protocol + harness documentation |
| Leak synthesis pipeline (grep → module) |
| SDLC + verify gates as agent law |
| Scaffold templates for new projects |

---

## Out of scope (v1 — document as EXTERNAL)

| NOT v1 | Reason |
|--------|--------|
| Hosted SaaS multi-tenant | infrastructure wave |
| Custom LLM training | roadmap |
| Mobile native IDE | separate product |
| Live billing / Stripe | O-external pattern |
| 1M-user scale test | load test EXTERNAL |

---

## User personas

| Persona | Need | Platform answer |
|---------|------|-----------------|
| Solo developer | Ship fast with quality | Agent orchestrator + verify gates |
| Tech lead | Architecture decisions | Shura + Plan Mode |
| Skill author | Encode team law | Skill Studio |
| Researcher | Deep tech evaluation | Leak Lab + adversarial verify |

---

## Non-goals (FORBIDDEN positioning)

| FORBIDDEN | WHY |
|-----------|-----|
| "Magic AI writes perfect code" | violates verify law |
| Chat-only UX without repo grounding | violates tools-over-memory |
| Bulk leak paste as "intelligence" | violates synthesis law |
| Skip Shura on launch decisions | violates governance |

---

## Success criteria (platform v1)

| # | Criterion | Verify |
|---|-----------|--------|
| S1 | Agent completes fix with test proof | dotnet test / native test PASS |
| S2 | New skill authored under 500 lines SKILL.md | skill-studio checklist |
| S3 | Shura runs on saved analysis | council exit 0 + evidence path |
| S4 | Zero Arabic in skill bodies | `rg [\u0600-\u06FF] .cursor/skills/` |
| S5 | Leak principle lands in exactly one primary module | LEARNING-INDEX route |

**Related:** [architecture.md](architecture.md) · [sdlc-pipeline.md](sdlc-pipeline.md)
