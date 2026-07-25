# Applied Sciences & Engineering Practice — Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: human-intelligence-mastery/sciences  
> **Synthesis:** Codex engineering judgment · Gemini technical integrity · OpenCode verify  
> **BRQOSH handoff:** [brqosh-engineering](../../brqosh-engineering/SKILL.md)

---

## Universal Engineering Loop

```
Requirements → constraints → design → prototype → test → iterate → maintain → retire
```

| Phase | Proof |
|-------|-------|
| Requirements | acceptance criteria |
| Design | review · tradeoffs documented |
| Prototype | minimal working |
| Test | reproducible failure before fix |
| Maintain | observability · runbooks |

---

## Discipline Invariants

| Field | Key invariants |
|-------|----------------|
| Civil/structural | Load paths · safety factors · codes |
| Mechanical | Materials · fatigue · tolerances |
| Electrical | Power · grounding · standards · arc flash |
| Chemical/process | Mass balance · exotherm control |
| Software | Correctness · security · operability |
| Industrial | Reliability · redundancy · human factors |

---

## Software / Computer Engineering

**Hand off to supreme-programming + brqosh-engineering for implementation:**

| Principle | Source |
|-----------|--------|
| Read before edit | Cursor |
| Repro before fix | Gemini |
| Test scales with risk | Codex |
| No library without lockfile proof | Gemini |
| Persistence through verify | Codex |

---

## Systems & Reliability

| Concept | Application |
|---------|-------------|
| MTBF / MTTR | ops planning |
| Failover | treasury · posting redundancy design |
| Idempotency | API mutations |
| Backpressure | queue design |
| Human factors | ERP UX — error messages · undo paths |

**BRQOSH:** `IdempotencyStore` pattern in Api.

---

## Failure Analysis (5 Whys + fishbone)

```
Symptom → why ×5 → root cause vs contributing factors
→ corrective action (fix) vs preventive (process)
→ verify fix doesn't break invariant
```

Link: [brqosh-debug/reference.md](../../brqosh-debug/reference.md)

---

## Standards & Compliance

- Cite standard name + version when relevant (ISO, IEEE, SOC2)
- Don't invent compliance — grep docs or say unknown
- **BRQOSH:** ZATCA in-repo vs live — 4 closure levels

---

## Energy · Environment · Agriculture

- Lifecycle analysis · externalities
- Policy + technology intersection — evenhanded
- Search for current regulations

---

## BRQOSH Construction / Manufacturing Modules

| Module | Engineering lens |
|--------|------------------|
| Construction | project costing · WIP |
| Manufacturing | BOM · WO · variance |
| Inventory | WAC/FIFO · negative stock policy |

Domain detail: [brqosh-accounting/reference.md](../../brqosh-accounting/reference.md)

---

## Anti-Patterns

| Avoid | Instead |
|-------|---------|
| "Best practice" without context | tradeoff table |
| Hand-wavy reliability | measurable SLO |
| Skip test on "engineering judgment" | judgment + test for risk tier |

**Next:** [formal.md](formal.md) · [natural.md](natural.md)
