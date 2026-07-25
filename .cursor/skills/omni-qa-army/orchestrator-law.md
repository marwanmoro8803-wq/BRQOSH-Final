# Orchestrator Law — Central Cognitive Loop (Skill Spec Only)

> **Status:** Design law only — **no** `cognitive-qa/` code in repo  
> **Roles:** QA Auto · DevOps · Manual QA  
> **Related:** [launcher-architecture.md](launcher-architecture.md) · [forensic-resilience.md](forensic-resilience.md)

---

## Immutable laws

| ID | Law |
|----|-----|
| O01 | Single orchestrator owns session lifecycle when code exists |
| O02 | Phases sequential — LAUNCH → EXPLORE → REPORT → SHUTDOWN |
| O03 | Modal gate before every scenario step |
| O04 | `max_steps` hard cap |
| O05 | `roles_enabled` filters findings |
| O06 | Failure → shutdown launcher · exit `3` |
| O07 | Each orchestrator phase ends with named verify (Karpathy K4) |
| O08 | Boredom interrupt after 3 same-screen visits without progress |

---

## Session phases

```
LAUNCH   → load target app · init state model · open chronicle
EXPLORE  → role-filtered scenarios · modal gate · boredom/chaos branches
REPORT   → assemble AUDIT_REPORT.md · bug index · role deliverables
SHUTDOWN → exit code from severity table · cleanup processes
```

---

## Smart navigation loop (L430–434)

| Before every action | MUST |
|---------------------|------|
| Modal check | `vision_brain.detect_new_surface()` |
| Stale screen | increment repeat counter |
| Counter ≥ 3 | boredom interrupt → [chaos-exploration.md](chaos-exploration.md) |

---

## Boredom interrupt (O08)

1. Log interrupt reason in chronicle
2. Trigger creative sabotage OR switch to unvisited graph branch
3. Reset repeat counter on successful transition

---

## Perfection Loop (L242–248) — when user enables toggle

```
repeat until Critical=0 AND High=0 AND KnowledgeComplianceScore=100:
  1. Full cognitive audit (10 roles) → AUDIT_REPORT.md + bug tracker
  2. Code deep analyzer → CODE_DEEP_ANALYSIS_REPORT.md
  3. If violations → self-healer → FIX_PLAN.md → apply → verify
  4. On failure rollback → log in Self-Healer Action Log
```

**MUST NOT** run Perfection Loop without explicit launcher toggle (future code).

---

## Exit code contract (CI/CD)

| Code | Meaning | Pipeline action |
|------|---------|-----------------|
| 0 | pass — no Critical/High open | green |
| 1 | Critical findings open | fail build |
| 2 | High findings open | fail build (configurable warn) |
| 3 | orchestrator exception | fail build |

Scripts: `run_audit.ps1` · `run_audit.sh` → non-zero on Critical per L437–439.

---

## State model (O09)

Maintain `app_state_model.json`:

| Field | Purpose |
|-------|---------|
| screens | discovered nodes |
| edges | transitions |
| anomalies | expectation violations |
| checkpoints | safe resume after crash |

Consult model before action — avoid dead-ends · prioritize unvisited paths.

---

## Headless mode (DevOps)

| Flag | Behavior |
|------|----------|
| `--headless` | no launcher UI · stdout chronicle |
| `--output-dir` | default `audit_reports/` |
| `--roles` | comma list filter |

---

## Implementation

**None in repo.** When user authorizes prompt implementation, implement per Shura CEEF-IMPLEMENT-001 — not before.

**Related:** [prompt-source-law.md](prompt-source-law.md) · [roles/role-automation.md](roles/role-automation.md) · [roles/role-devops.md](roles/role-devops.md)
