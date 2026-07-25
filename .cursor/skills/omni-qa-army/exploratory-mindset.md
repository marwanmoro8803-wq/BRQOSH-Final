# Exploratory Mindset — Human Pace & Chronicle

> **Leaks:** xAI grok-build (verify · pace) · Microsoft copilot-cli (critic)  
> **Roles:** Manual QA · Beta Tester (P1)

---

## Purpose

Mimic **expert human exploration** — fast, associative, bored-user resistant — with structured evidence.

---

## Dynamic pace (MUST)

| Rule | Detail |
|------|--------|
| Adaptive timeout | 50ms element appear → don't wait 500ms |
| Overlap actions | type while dropdown opens — race probes |
| Context-switch | pen test → UX → perf within same session |
| Parallel work | while app busy · analyze prior screenshots |

**FORBIDDEN:** robotic fixed-delay loops without intent.

---

## Boredom interrupt (orchestrator law)

If same screen **3 times** without progress:

1. Trigger creative sabotage (see [chaos-exploration.md](chaos-exploration.md))
2. OR switch to unvisited graph branch
3. Log interrupt reason

---

## Chronicle diary (MANDATORY per action)

Full template and session rules: [chronicle-protocol.md](chronicle-protocol.md).

**MUST** flag mismatch as learning anomaly in `app_state_model.json`.

---

## Exploration sequence (forms)

| Phase | Action |
|-------|--------|
| 1 Valid path | realistic compliant data → advance |
| 2 Return | same form · malicious/weird payloads |
| 3 Document | both paths separately in diary |

**MUST NOT** skip valid path and only fuzz.

---

## Devil's advocate (Microsoft critic pattern)

Before closing a screen:

- Why might this not work?
- What did developer forget?
- What would tired user do wrong?

One critic sentence per major screen in diary.

---

## Verify before session close (grok-build)

| MUST | Evidence |
|------|----------|
| All reachable graph edges attempted OR marked blocked | state model |
| Crashes recovered via forensic restart | log entry |
| No "done" without observation captures | evidence dir non-empty |

---

## P1 owner modules

`human_navigator.py` · `chronicle_logger.py` · `audit_orchestrator.py`
