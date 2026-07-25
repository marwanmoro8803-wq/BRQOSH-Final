# Shura Governance — Platform Decision Council

> **Constitution:** [shura-constitution.md](../brqosh-analysis/shura-constitution.md) · **Protocol:** [shura-council-protocol.md](../brqosh-analysis/shura-council-protocol.md)  
> **Heartbeat:** [shura-heartbeat-core.md](../brqosh-analysis/shura-heartbeat-core.md) · **Tools:** [shura-tools-registry.yaml](../brqosh-analysis/shura-tools-registry.yaml)

---

## Role in Program Developer

Shura is the **evidence-gated consensus layer** for platform decisions — architecture forks, launch readiness, skill stack changes, major SDLC waves.

```
NOT daily bug fixes · NOT chat theatre · YES saved analysis → executable council
```

---

## When Shura MUST run (agent — not user)

| Trigger | Shura |
|---------|-------|
| Comprehensive analysis SAVE | YES |
| Platform architecture CEEF | YES |
| Launch / GTM / pricing | YES |
| Skill stack restructure wave | YES |
| Typo · single test fix | NO |
| Explain-only question | NO |

**Law:** `.cursor/rules/brqosh-shura-auto.mdc`

---

## 7-round session (platform context)

| Round | Platform application |
|-------|---------------------|
| 1 Facts | STATUS · tests · evidence paths |
| 2 Positions | 12 seats · product/tech/market |
| 3 Debate | objections need evidence ID |
| 3b Adversarial | C-SEC · C-TEST · C-ARCH claims |
| 4 Vote | ≥9/12 · ≤1 P0 reject |
| 5 Conflict | security > verify > architecture |
| 6 F00 | steelman · escalation |
| 7 Action | P0→P3 with verify |

---

## Platform-specific seats emphasis

| Seat | Platform question |
|------|-----------------|
| S04 | Platform or feature? |
| S06 | Maintainable agent tools in repo? |
| S07 | Sellable as dev product? |
| S10 | What do we stop to ship this? |
| S12 | False "v1 complete" claim? |
| F00 | Is consensus honest? |

---

## Integration with SDLC

```
SDLC ⑦ RELEASE ──if analysis SAVE──► run-shura-council.ps1 ──► [exit 0] run-shura-dynamo.ps1 ──► MEM type:decision
```

| exit 0 | exit 1 |
|--------|--------|
| unified verdict + action plan | no final launch claim |

---

## Adapt Shura evidence for platform (future)

When platform grows, extend `EvidenceLoader.cs`:
- skill regression PASS
- agent-skill-regression output
- platform integration test logs

**Current:** reuse BRQOSH evidence paths + STATUS.yaml.

---

## MUST NOT

| FORBIDDEN | WHY |
|-----------|-----|
| User runs council manually | auto-law |
| Decision without council JSON | no fake consensus |
| Skip 3b on critical claims | adversarial law |
| Arabic in council reports skill bodies | language-enforcement |

**Related:** [architecture.md](architecture.md) · [verify-gates.md](verify-gates.md)
