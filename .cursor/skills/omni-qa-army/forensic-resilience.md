# Forensic Resilience — Crash Recovery & Self-Healing

> **Source:** raw prompt L61–67 (ideas only)  
> **Leaks:** Anthropic debug.md (repro-first) · xAI grok-build (persistence)  
> **Roles:** QA Auto (S01) · Manual QA (S02)  
> **Related:** [brqosh-debug/SKILL.md](../brqosh-debug/SKILL.md) · [vision-perception.md](vision-perception.md)

---

## Purpose

When the app **blocks, warns, crashes, or lies** — trace, adapt, recover, and continue until reachable states are exhausted.

---

## Warning and modal handling (MUST)

| Step | Action |
|------|--------|
| 1 | Read popup completely via UIA/OCR |
| 2 | Pull runtime logs if available |
| 3 | Trace to component/source if identifiable |
| 4 | Self-correct payload to clear block |
| 5 | Resume from safe checkpoint |

---

## Crash resilience protocol

```
CRASH → capture screen + error text → kill process → restart app
      → resume from last safe checkpoint in state graph
      → log forensic entry in chronicle
```

**MUST NOT** end audit on first crash unless unrecoverable (3 restart failures).

---

## Silent failure forensics

| Symptom | Forensic action |
|---------|-----------------|
| UI says saved | verify filesystem |
| No error on bad input | check logs |
| Intermittent bug | correlate with prior anomalies (superintelligence recall) |

Link to [empathetic-ux.md](empathetic-ux.md) silent-failure rules.

---

## Learning from failures

| Event | Update |
|-------|--------|
| Unexpected behavior | `app_state_model.json` path marked broken |
| Expectation mismatch | chronicle anomaly flag |
| Recovery success | checkpoint recorded |

---

## Persistence law (grok-build)

| MUST | Detail |
|------|--------|
| Retry after single failure | alternate approach before abort |
| Document each recovery | chronicle + BUG ticket if defect |
| No "done" with open crash loop | unrecovered crash = Critical |

---

## brqosh-debug alignment

| brqosh-debug | forensic-resilience |
|--------------|---------------------|
| Hypothesis tree | crash → config/logic/data/env |
| Minimal repro | exact steps in BUG-XXXX |
| Evidence-first | logs + screenshot paths |

Use brqosh-debug for **agent-in-Cursor** sessions; this module for **desktop audit runtime** (future code).

---

## Forbidden

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| Ignore modal and click through | modal gate (orchestrator O03) |
| Restart without logging | forensic chronicle entry |
| Claim healed without re-verify | re-run failed path |

---

## Future code owners (when authorized)

`forensic_handler.py` · `audit_orchestrator.py` (checkpoint) · `vision_brain.py` (crash screen)

---

## Verify

```powershell
Test-Path .cursor/skills/omni-qa-army/forensic-resilience.md
rg "forensic" .cursor/skills/brqosh-debug/
```
