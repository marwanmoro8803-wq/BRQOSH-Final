---
name: brqosh-agent-protocol
description: >-
  BRQOSH agent protocol v12: triage, impact-before-change, MEM writeback,
  council routing (14 domains), verify before done. Use at session start.
---

# BRQOSH Agent Protocol

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Every message

1. **CLASSIFY** — `fable-omni-core` + universal-understanding (any topic)
2. Last user request leads
3. Mode: execute | analyze | review | creative | teach
4. BRQOSH? -> accounting + UI conventions
5. Activate 1-3 councils max

## Cycle

```
TRIAGE -> IMPACT -> DISCOVER -> DELIBERATE -> ACT -> VERIFY -> MEM -> LEARN -> REPORT
```

## Rules

- Smallest correct diff; no drive-by refactors
- Read before edit; parallel grep/glob
- impact-before-change before mutation; MEM + LEARN after verify
- No "done" without build+test (see brqosh-skills-hub STATUS.yaml)
- External limits: OTP, SOCPA, ZAP -> document as skipped, do not fake closure

## References

- [protocol.md](protocol.md) — always-on workflow
- [impact-before-change.md](impact-before-change.md) — pre-flight · MEM writeback
- [knowledge-accumulation.md](knowledge-accumulation.md) — LEARN loop · Cursor artifact routing
- [routing.md](routing.md) — 14 councils + trigger table
- [constitution.md](constitution.md) — ten governors · five-step cycle · closure law

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-SESSION · **Fable:** CLASSIFY from fable-omni-core

## Default path

```
SKILL → protocol.md → impact-before-change.md (before code) → routing.md → domain skill → VERIFY → MEM → LEARN
```

---

### `SKILL.md` · L0 · 1/3

| | |
|--|--|
| **TRIGGER** | Every BRQOSH session start |
| **SKIP** | Never in BRQOSH repo |
| **NEXT** | protocol.md |
| **EXIT** | Mode: execute \| analyze \| review \| teach |
| **ANTI** | Skip triage on multi-domain task |
| **TIME** | 20s |

---

### `protocol.md` · L1 · 2/6

| | |
|--|--|
| **TRIGGER** | Every message — permanent workflow |
| **NEXT** | impact-before-change.md (before code) · routing.md |
| **EXIT** | TRIAGE→IMPACT→DISCOVER→ACT→VERIFY→MEM pipeline |
| **ANTI** | Suggest without execute when asked |
| **TIME** | 45s |

---

### `impact-before-change.md` · L1 · 2b/6

| | |
|--|--|
| **TRIGGER** | Any Write/StrReplace/delete in src/tests/schema/rules |
| **SKIP** | Inquiry-only · read-only review |
| **EXIT** | آ§1â€“آ§6 answered · verify plan defined · MEM after verify |
| **ANTI** | Edit before grep callers |
| **TIME** | 60s (L2+ Plan Mode longer) |

---

### `knowledge-accumulation.md` · L1 · 2c/6

| | |
|--|--|
| **TRIGGER** | After verify · discussion insight · mistake · plan close |
| **SKIP** | Casual chat · no new durable fact |
| **EXIT** | LEARN loop done · MEM and/or skill-lessons updated |
| **ANTI** | Paste chat into SKILL.md |
| **TIME** | 45s |

---

### `constitution.md` · L1 · 3/6

| | |
|--|--|
| **TRIGGER** | Complex wave · review · closure · ambiguous authority |
| **SOURCE** | archive/MASTER-CONSTITUTION.md (merged) |
| **EXIT** | Ten governors + six-step cycle internalized |
| **TIME** | 60s |

---

### `routing.md` · L2 · 4/6

| | |
|--|--|
| **TRIGGER** | Multi-domain · council selection |
| **EXIT** | 1-3 councils (C01-C14) · skills chosen |
| **ANTI** | More than 3 councils |
| **TIME** | 30s |

## Scenarios

| Signal | councils → skills |
|--------|-------------------|
| test fail | C01+C11 → debug+engineering |
| UI RTL | C03+C06 → ui+engineering |
| ZATCA | C06+C12 → accounting+closure |
| CEEF close | C12 → closure |
