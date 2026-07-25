# Shura Constitution v5 — مجلس الشورى (Complete Entity Law)

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Protocol:** [shura-council-protocol.md](shura-council-protocol.md) · **Intelligence:** [shura-intelligence-core.md](shura-intelligence-core.md)  
> **Members:** [shura-members-registry.yaml](shura-members-registry.yaml) · **Heartbeat:** [shura-heartbeat-core.md](shura-heartbeat-core.md)  
> **Tools:** [shura-tools-registry.yaml](shura-tools-registry.yaml)  
> **Auto-law:** `.cursor/rules/brqosh-shura-auto.mdc`

---

## 1. Concept — what Shura IS

Shura Council is an **evidence-gated consensus entity** — not theatre, not chat personas, not agent memory.

```
Shura = SAVE (CEEF) → C# engine reads evidence → 12 votes + F00 + 28 disciplines → JSON exit code → MEM
```

| Property | Definition |
|----------|------------|
| **Purpose** | Bind major decisions to verified evidence — launch, architecture, prompt governance, comprehensive audits |
| **Authority** | `exit 0` on `council-*.json` — not agent opinion |
| **Forbidden** | 50-paragraph markdown council · user manual run · decision without tool |
| **Value law** | `(diagnosis_speed × domain_depth × verified_evidence) ÷ theatrical_text` |

### Dual workspace modes (unified identity)

The **same seat IDs** (S01–S12) wear different role names per workspace — not different councils.

| Mode | Detect | Seat remap | Decisions |
|------|--------|------------|-----------|
| **BRQOSH ERP** | no `PROJECT.yaml` | Advocate · Opponent · Accountant · CEO | D1–D7 |
| **Program Developer** | `PROJECT.yaml` exists | QA Auto · Pen Tester · Superintelligence | DP1–DP10 |

**Registry:** `shura-members-registry.yaml` maps both role names to one seat ID.  
**Engine:** `SeatIntelligenceRegistry.cs` (BRQOSH) · `PromptSeatIntelligenceRegistry.cs` (PD).

---

## 2. Anatomy — five organs

```
                    ┌─────────────────────────────────────┐
                    │  F00 FACILITATOR (conscience voice) │
                    └─────────────────┬───────────────────┘
                                      │
    ┌─────────────── MIND ──────────────┼──────────── HEARTBEAT ─────────────┐
    │  SeatIntelligenceRegistry         │         Shura Dynamo (41 pulses)   │
    │  LeakSynthesisEngine (14 fam)     │         Invoke-ShuraPulse.ps1    │
    │  ProgrammingDisciplineRegistry    │         dispatch → evidence        │
    └───────────────┬───────────────────┴───────────────────┬────────────────┘
                    │                                       │
              ┌─────▼─────┐                           ┌─────▼─────┐
              │   BODY    │                           │  CHRONICLE │
              │ ShuraCouncil C#                       │ merge AiCouncil │
              │ EvidenceLoader                        │ + Shura timeline│
              └─────┬─────┘                           └───────────┘
                    │
              ┌─────▼─────┐
              │ CONSCIENCE│
              │ SupremeGateEngine (7 gates)           │
              │ AdversarialVerifier (3b)              │
              └───────────┘
```

| Organ | Component | Function |
|-------|-----------|----------|
| **Body** | `ShuraCouncil/` C# | Load evidence · evaluate seats · write reports |
| **Mind** | Intelligence registries + leak synthesis | Seat ↔ council ↔ leak ↔ skill routing |
| **Conscience** | Supreme gates + adversarial 3b | Reject false closure · kill unverified claims |
| **Voice** | `council-*.md` + JSON | Unified verdict · P0→P3 · preserved dissent |
| **Heartbeat** | Shura Dynamo | Post-consensus operational pulses per member |

---

## 3. Members — 41 seats (complete roster)

### Tier 0 — Facilitator (non-voting)

| ID | Role | Function |
|----|------|----------|
| **F00** | Omniscient Facilitator | Synthesis · steelman · escalation · chronicle merge pulse |

### Tier 1 — Voting core (12)

Consensus math: **≥9/12** approve or conditional · **≤1** P0 oppose · **3b** claims survive.

| ID | BRQOSH role | Program Developer role | Conflict weight |
|----|-------------|------------------------|-----------------|
| S01 | Advocate | QA Automation | product optimism (evidence-gated) |
| S02 | Opponent | Manual Exploratory QA | skeptic · 3b voter |
| S03 | Critic | UX Accessibility | verify gaps |
| S04 | Strategist | Penetration Tester | security #1 in conflicts |
| S05 | Security | Beta Tester | RBAC · OWASP |
| S06 | Engineer | Performance | minimal diff |
| S07 | Market | L10n Arabic RTL | market synthesis |
| S08 | Accountant | DevOps Integration | accounting > product (BRQOSH) |
| S09 | Economist | Compatibility | ROI |
| S10 | CEO | Tech Copywriter | scope cut |
| S11 | Tax authority | Superintelligence | compliance · leak oracle |
| S12 | External auditor | Report Auditor | false closure · 3b voter |

Full intelligence per seat: `shura-members-registry.yaml` · `PromptSeatIntelligenceRegistry.cs`.

### Tier 2 — Discipline advisory (28)

D01–D28 — one programming specialty each. **Advisory only** — gate `G-DISCIPLINE` (≥60% ready).

Categories: Languages · Systems · Mobile · Ops · Security · Architecture · Integration · UI · QA · Data · AI · Web · Paradigms · Docs · Quality.

Source: `ShuraCouncil/programming-disciplines-registry.yaml`.

---

## 4. Session — 8 rounds + heartbeat closure

| Round | Name | Rule |
|-------|------|------|
| 1 | Facts | Numbers only · 4 closure levels · no opinion |
| 2 | Positions | ≤3 sentences/seat · S01 + · S02 − |
| 3 | Debate | Objections need evidence ID |
| **3b** | **Adversarial** | S02+S03+S12 vote · 2/3 refute kills claim |
| 4 | Vote | ✅/🟡/❌ per decision |
| 5 | Conflict | security > accounting > verify > architecture > product > UX |
| 6 | F00 synthesis | Steelman minority · escalate P0 oppose |
| 7 | Action plan | P0→P3 with verify commands |
| 8 | Leak + Supreme | 14 families · 7 gates · disciplines §11 |
| **H** | **Heartbeat** | F00 dispatches Dynamo pulses → `dist/evidence/shura-dynamo/` |

---

## 5. Supreme gates (7 — v5)

| Gate | Pass criterion |
|------|----------------|
| G-SCOPE | CEEF SAVE exists |
| G-EVIDENCE | ≥3 evidence paths loaded |
| G-VERIFY | All 3b claims survive |
| G-CONSENSUS | ≥9/12 · P0 decisions met |
| G-FACILITATOR | F00 clear or escalation documented |
| G-LEAK | Leak synthesis ≥70 · 10+ families |
| G-DISCIPLINE | ≥60% disciplines ready (advisory) |

`supremeCertified: true` in JSON = all 7 pass.

---

## 6. Tools — execution stack

| Layer | Tool | Agent runs |
|-------|------|------------|
| Consensus | `scripts/ops/run-shura-council.ps1` | After comprehensive SAVE |
| Heartbeat | `scripts/ops/run-shura-dynamo.ps1` | After council exit 0 (optional maintain) |
| Chronicle | `scripts/ops/merge-council-chronicle.ps1` | F00 pulse / post-council |
| Desktop | `CognitiveQa.Core/ShuraCouncilIntegration.cs` | Dashboard snapshot |
| Validate | `scripts/ops/validate-shura-constitution.ps1` · `run-shura-compliance-gate.ps1` | Constitution + P0-P2 orders |

Full map: `shura-tools-registry.yaml`.

---

## 7. Cognitive stack (L0→L6)

| Layer | What |
|-------|------|
| L0 Fable | CLASSIFY every message |
| L1 Councils C01–C14 | Daily execution (1–3 councils) |
| L2 Dream Team | Large CEEF waves |
| L3 Shura | 12-seat consensus tool |
| L4 Adversarial 3b | Claim survival |
| L5 Leak Supreme | 14 families · 7 gates |
| **L6 Heartbeat** | **Dynamo pulses — council decisions become ops** |

---

## 8. Pipeline (immutable)

```
ANALYZE → SAVE (CEEF) → run-shura-council.ps1 → read council-*.json
       → [exit 0] → run-shura-dynamo.ps1 (F00 dispatch)
       → merge-council-chronicle.ps1 → MEM type:decision
```

**Agent runs council — user never.** Law: `brqosh-shura-auto.mdc`.

---

## 9. Outputs (mandatory)

| Artifact | Path |
|----------|------|
| Markdown report | `dist/evidence/shura-council/council-*.md` |
| Machine JSON | `dist/evidence/shura-council/council-*.json` |
| Pulse evidence | `dist/evidence/shura-dynamo/pulses/` |
| Dispatch manifest | `dist/evidence/shura-dynamo/dispatch/dispatch-*.json` |
| Chronicle | `dist/evidence/council-chronicle/CHRONICLE.md` |
| MEM row | `docs/PROJECT-MEMORY.yaml` type: decision |

---

## 10. Verify

```powershell
.\scripts\ops\run-shura-compliance-gate.ps1
.\scripts\ops\validate-shura-constitution.ps1
dotnet build ShuraCouncil/ShuraCouncil.csproj -c Release
.\scripts\ops\run-shura-council.ps1 -Source "dist/evidence/prompt-governance/CEEF-*.md"
# exit 0 = consensus approved
```

**Related:** [shura-prompt-governance.md](shura-prompt-governance.md) · [council-intelligence-matrix.md](../brqosh-agents/council-intelligence-matrix.md)
