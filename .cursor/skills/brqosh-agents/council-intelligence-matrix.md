# Council Intelligence Matrix — 14 Councils + 12 Shura + F00 + 279-Leak Synthesis v2

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agents  
> **Corpus:** `system_prompts_leaks-main/system_prompts_leaks-main` (279 files)  
> **Index:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)  
> **Shura entity:** [shura-intelligence-core.md](../brqosh-analysis/shura-intelligence-core.md)  
> **Rule:** grep principles only — never bulk-load leaks in sessions

---

## 0. Core idea

**Supreme intelligence here =** composite synthesis of the best global agent patterns (Anthropic · OpenAI · Google · xAI · Cursor · Microsoft · Perplexity) **applied** to BRQOSH ERP — not a claim of omniscience.

```
VALUE = (diagnosis_speed × domain_depth × verified_evidence) ÷ theatre
```

---

## 1. Intelligence layers (how the agent works)

| Layer | When | Mechanism | File |
|-------|------|-----------|------|
| **L0 Fable** | every message | 12 principles · classify · humility | fable-omni-core |
| **L1 Councils** | daily execution | 1–3 from C01–C14 · 1–3 sentences | routing.md |
| **L2 Dream Team** | CEEF wave · 5+ modules | 7 roles · parallel readonly | dream-team.md |
| **L3 Shura** | after SAVE analysis | **v5 tool** 12 seats + F00 + 28 disciplines · real evidence | `ShuraCouncil/` |
| **L3b Adversarial** | with L3 | 3-vote on critical claims | `AdversarialVerifier.cs` |
| **L4 Leak depth** | specialty deepen | grep one file from family | leak-corpus-index.yaml |

---

## 2. The 14 councils (C01–C14) — specialty × leak × skill

| ID | Global specialty | Leak families (grep) | Skill module | Extracted intelligence pattern |
|----|------------------|---------------------|--------------|-------------------------------|
| C01 CODE | engineering · SDLC | OpenAI Codex · Anthropic Code | brqosh-engineering · supreme-programming | read-first · minimal diff · test scales risk |
| C02 ARCH | architecture · integration | Anthropic · Google gemini-cli | brqosh-analysis/super-dev.md | boundaries · adversarial · no over-abstract |
| C03 UI | WPF · RTL · ERP density | Anthropic design · Cursor | brqosh-ui · design-system | Financial Truth First · not marketing |
| C04 GAMES | Unity · gameplay | Misc creative | brqosh-creative | honest limits · playtest |
| C05 CREATIVE | media · narrative | Anthropic artifact | brqosh-creative | iteration · brief discipline |
| C06 ACCOUNTING | accounting · ZATCA · GL | — (domain) | brqosh-accounting | debit=credit · audit trail · Golden |
| C07 SECURITY | RBAC · API · SSRF | Anthropic security-review | brqosh-review/security.md | attacker mindset · ≥80% confidence |
| C08 OPS | CI · CEEF · gates | OpenAI · Microsoft | brqosh-closure | 4 closure levels · evidence paths |
| C09 STUDY | explain · research | Anthropic deep-research · Perplexity | human-intelligence | fan-out · cite · copyright |
| C10 COMMS | bilingual · teaching | OpenAI · devin-cli | brqosh-communication | accessible prose · no condescension |
| C11 QA | repro · regression | Anthropic verify · Gemini repro | brqosh-debug | repro-before-fix · hypothesis tree |
| C12 PRODUCT | scope · roadmap | Codex plan_mode | brqosh-analysis/requirements.md | smallest scope · say no |
| C13 ANALYSIS | hard decisions | Google notebooklm · indus-ai | deep-cognition.md | synthesize don't summarize |
| C14 SCIENCE | hypotheses · experiments | xAI expert personas | human-intelligence/sciences | hypothesis · evidence tiers |

**Conflict priority:** security > accounting > verify > architecture > product > UX > aesthetics (routing.md §4).

---

## 3. Shura Council (S01–S12 + F00) — seat × councils × evidence × leaks

| Seat | Role | Councils | Leak pattern | Checks / engine |
|------|------|----------|--------------|-----------------|
| S01 | Advocate | C12 · C03 | Codex optimistic delivery | UI rate · tests · scenario |
| S02 | Opponent | C11 · C13 | Microsoft devil's advocate · Fable | UI fail · P0 · **3b voter** |
| S03 | Critic | C11 · C08 | Anthropic verify skill | tests · FlaUI · **3b voter** |
| S04 | Strategist | C02 · C12 | Gemini long-horizon | G-items · GTM |
| S05 | Security | C07 | Anthropic security-review | P0 · API key · **conflict #1** |
| S06 | Engineer | C01 · C08 | Cursor making_code_changes | UiAudit · NavKeyMap · Shura |
| S07 | Market | C12 · C10 | Perplexity market synthesis | G-31/G-32 · zero-code |
| S08 | Accountant | C06 | — domain | FullScenario · **conflict #2** |
| S09 | Economist | C12 · C14 | Codex ROI judgment | automation ROI |
| S10 | CEO | C12 | Codex scope discipline | P0 backlog · GTM focus |
| S11 | Tax authority | C06 · C07 | ZATCA domain | O-01 · **conflict #3** |
| S12 | External auditor | C08 · C06 | Fable no false closure | 4-level · **3b voter** |
| **F00** | **Facilitator** | C08 · C13 | Codex escalation · xAI steelman | `FacilitatorEngine.cs` |

**Execution:** the agent runs `run-shura-council.ps1` — NOT the user (`brqosh-shura-auto.mdc`).

### 3b. Program Developer — prompt QA Army seats

When `PROJECT.yaml` exists, seats mirror **البرومبيت.txt** ten roles + overseer + report auditor.

| Seat | Prompt role | Conflict # |
|------|-------------|------------|
| S04 | Pen Tester | #1 |
| S11 | Superintelligence Overseer | #2 |
| S12 | Report Auditor | #3 |
| S02 | Manual Exploratory | 3b voter |
| S07 | L10n/Arabic | RTL mandate |

**Registry:** `PromptSeatIntelligenceRegistry.cs` · **Evaluators:** `PromptGovernanceSeatEvaluators.cs`

---

## 4. Dream Team ↔ Shura ↔ Councils

```
daily (bug/feature)     → C01–C03 councils only
large wave (CEEF)       → Dream Team 7 + councils
after saved analysis    → Shura 12 (automated tool) + P0→P3 decision
```

| Work size | Use |
|-----------|-----|
| typo · single red test | C01 + C11 |
| RTL screen | C03 + C01 |
| posting/ZATCA | C06 + C01 + C11 |
| full audit · launch | SAVE → **Auto-Shura** |
| raw QA prompt governance | SAVE CEEF-PROMPT-001 → **Auto-Shura** (DP mode) |
| CEEF close | Dream Team + gates + Shura if decision needed |

---

## 5. system_prompts_leaks-main — how it was fully absorbed

| Step | What was done |
|------|---------------|
| 1 | Index 279 files → `leak-corpus-index.yaml` |
| 2 | Route each family → skill module (no text copy) |
| 3 | Merge into `elite-models.md` · `leak-synthesis.md` |
| 4 | Apply in protocol · harness · routing · review |
| 5 | Shura seats + F00 + 3b adversarial + ConflictResolver in C# |

**To deepen a specialty:** `rg "pattern" system_prompts_leaks-main/...` then extract ≤5 principle lines into `skill-lessons.yaml` — no paste into rules.

---

## 6. Anti-patterns

| DO | DON'T |
|----|-------|
| skills + councils + Shura tool | load 279 leaks in session |
| agent runs Shura | "run the script yourself" |
| evidence from dist/evidence | decision from agent imagination |
| grep one leak file when needed | claim "supreme intelligence" without verify |

**Next:** [harness.md](harness.md) · [shura-intelligence-core.md](../brqosh-analysis/shura-intelligence-core.md) · [shura-council-protocol.md](../brqosh-analysis/shura-council-protocol.md)
