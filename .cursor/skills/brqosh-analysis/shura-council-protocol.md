# Shura Council Protocol v5 — Smart Consensus Entity

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Constitution:** [shura-constitution.md](shura-constitution.md) — complete entity law · 41 members · heartbeat  
> **Entity:** [shura-intelligence-core.md](shura-intelligence-core.md) — identity · 8 rounds · leak synthesis  
> **Members:** [shura-members-registry.yaml](shura-members-registry.yaml) · **Heartbeat:** [shura-heartbeat-core.md](shura-heartbeat-core.md)  
> **When:** after any comprehensive analysis (UI · security · market · accounting · launch)  
> **Does NOT replace:** CEEF for waves · 14 BRQOSH councils for daily execution  
> **Complements:** `dream-team.md` · `routing.md` · `brqosh-closure`

---

## 0. What is the Shura Council?

```
entity = ShuraCouncil (C#) + 12 seats + F00 + 28 disciplines + Dynamo heartbeat + real evidence
```

| NOT | BUT |
|-----|-----|
| 50-paragraph theatrical chat | executable tool reading `dist/evidence/` |
| agent opinion from memory | `council-*.md` + exit code |
| false closure | 4 levels · explicit ⏭ EXTERNAL |

**Law:** `.cursor/rules/brqosh-shura-auto.mdc` · **Constitution:** `shura-constitution.md` · **Leaks:** `leak-corpus-index.yaml`

### 0b. Dual-mode seat identity (unified IDs)

Same seat IDs — different role names per workspace. See `shura-members-registry.yaml`.

| Seat | BRQOSH ERP | Program Developer |
|------|------------|-------------------|
| S01 | Advocate | QA Automation |
| S02 | Opponent | Manual Exploratory QA |
| S04 | Strategist | Penetration Tester |
| S08 | Accountant | DevOps Integration |
| S11 | Tax authority | Superintelligence |
| S12 | External auditor | Report Auditor |

Engine selects registry via `PROJECT.yaml` presence.

---

## 1. Pipeline — from analysis to decision

```
ANALYZE → SAVE → COUNCIL(v2) → ADVERSARIAL → CONSENSUS → ACTION → VERIFY → MEM
```

| Phase | What | Where saved |
|-------|------|-------------|
| **ANALYZE** | live evidence · no opinion without proof | `dist/evidence/` |
| **SAVE** | CEEF report or `docs/memory/MEM-NNN-*.md` | `reports/CEEF-*.md` |
| **COUNCIL** | `ShuraCouncil` — 12 seats + F00 | `dist/evidence/shura-council/council-*.md` |
| **ADVERSARIAL** | round 3b — 5 critical claims × 3 votes | same file §3 |
| **CONSENSUS** | unified decision + P0→P3 priorities | §7 + JSON |
| **ACTION** | execution waves · owners · verify | action plan |
| **VERIFY** | explicit build/test/live/external | logs in evidence |
| **MEM** | index `PROJECT-MEMORY.yaml` | `type: decision` + dissents |

**Golden rule:** no "final decision" before SAVE + agent runs `run-shura-council.ps1`.

---

## 1b. Execution tool (agent runs — mandatory)

```powershell
.\ShuraCouncil\run-shura-council.ps1 -Source "dist/evidence/prompt-governance/CEEF-PROMPT-001.md"
# BRQOSH UI audit:
.\ShuraCouncil\run-shura-council.ps1 -Source "reports/CEEF-145_Live_UI_Audit_Marwan.md"
```

**Program Developer mode:** when `PROJECT.yaml` exists, council governs raw QA prompt via skills — see [shura-prompt-governance.md](shura-prompt-governance.md). Claims become `C-PROMPT-*`; decisions `DP1–DP5`.

| Output | Path |
|--------|------|
| Markdown v2 report | `dist/evidence/shura-council/council-*.md` |
| Machine JSON | `dist/evidence/shura-council/council-*.json` |
| exit 0 | consensus approved (≥9 approve/conditional · ≤1 P0 oppose · 3b pass) |
| exit 1 | no consensus |

**Sources read:** `STATUS.yaml` · `security-findings.csv` · `market-gap-verified.csv` · `OPEN_ITEMS.md` · `live-ui-audit/audit-*.txt` · `full-scenario-audit.log`.

**Forbidden:** manual Shura CEEF without running the tool.

---

## 2. Council members (12 + F00 + 28 Disciplines v5)

### Tier 1 — Voting core (12 + F00)

| # | Role | Stake | Standing question | Leak |
|---|------|-------|-------------------|------|
| S01 | Advocate | fast launch | what works today? | Codex optimism |
| S02 | Opponent | overstated risk | where is the evidence? | Microsoft devil's advocate |
| S03 | Critic | gaps and tests | what was not checked? | Anthropic verify |
| S04 | Strategist | 3–5 years | platform or screen? | Gemini long-horizon |
| S05 | Security | RBAC · API | what can be stolen? | security-review |
| S06 | Engineer | maintenance · tests | smallest diff? | Cursor |
| S07 | Market | competitors · G-items | will it sell? | Perplexity |
| S08 | Accountant | entry · ZATCA | debit=credit? | domain |
| S09 | Economist | ROI | will the customer pay? | Codex ROI |
| S10 | CEO | focus | what do we stop? | plan_mode |
| S11 | Tax authority | declarations | live compliance? | ZATCA |
| S12 | External auditor | evidence | false closure? | Fable closure |
| **F00** | **Facilitator** | honest consensus | is consensus real? | Codex escalation + steelman |

### Tier 2 — Programming Discipline Council (28 advisory seats D01–D28)

One seat per programming specialty. **Advisory only** — does not change 12-seat vote math.

| Category | Seats | Examples |
|----------|-------|----------|
| Languages | D01–D03, D07 | C#/.NET · Python · TypeScript |
| Systems | D05–D06, D08 | Rust · Go · C/C++ |
| Mobile | D09–D10 | Swift · Android |
| Ops | D11–D12, D23, D27 | DevOps · Cloud · Shell · Git |
| Security | D13 | OWASP · RBAC |
| Architecture | D14 | DDD · ADR |
| Integration | D15 | API · OpenAPI |
| UI | D16 | WPF · RTL · FlaUI |
| QA | D17 | dotnet test · pytest |
| Data | D04, D18 | SQL · ETL |
| AI | D19, D28 | ML · prompts · agents |
| Web | D20–D21 | Frontend · Backend |
| Paradigms | D22, D26 | FP · async |
| Docs / Quality | D24–D25 | CEEF · performance |

**Registry:** `ShuraCouncil/programming-disciplines-registry.yaml` · **Engine:** `ProgrammingDisciplineRegistry.cs` + `ProgrammingDisciplineEvaluators.cs`  
**Report:** council markdown §11 · JSON `disciplineCouncil` · gate `G-DISCIPLINE` (≥60% ready, advisory)

**Conflict priority:** security > accounting > verify > architecture > product > UX > aesthetics.

---

## 3. Shura session — 7 rounds (v2)

### Round 1 — facts (Inquiry only)
- numbers from saved report · 4 closure levels
- **no opinion** — Google Inquiry/Directive split

### Round 2 — initial positions
- each member ≤3 sentences · S01 positive · S02 negative

### Round 3 — debate
- disagreement only · every objection needs **ID** from CEEF or CSV

### Round 3b — adversarial verification (new)
- **BRQOSH:** C-SEC · C-UI · C-GL · C-ZATCA · C-TEST
- **Program Developer (prompt):** C-PROMPT-CODE · C-PROMPT-SKILLS · C-PROMPT-LAW · C-PROMPT-LEAK · C-PROMPT-FALSE
- 3 votes: S02 + S03 + S12
- **2/3 refute = kill claim** (Anthropic deep-research)

### Round 4 — conditional vote

| Vote | Meaning |
|------|---------|
| ✅ approve | execute as-is |
| 🟡 conditional | after condition + verify |
| ❌ oppose | separate wave or ⏭ |

**Consensus:** ≥9/12 approve or conditional · ≤1 oppose on P0.

### Round 5 — conflict resolution
- Conflict Chain on 6-6 split or critical seat conflict

### Round 6 — F00 synthesis
- summary · steelman for minority · escalate if needed
- save dissents to MEM

### Round 7 — decision + plan
- unified ruling · wave 1 (week) · wave 2 (month) · ⏭ external

### Round 8 — leak synthesis + supreme gate (v5)
- `LeakSynthesisEngine` — 14 families · integrity score
- `SupremeGateEngine` — **7 gates** (incl. `G-DISCIPLINE`) · `supremeCertified` in JSON
- `ProgrammingDisciplineEvaluators` — 28 discipline seats · §11 report

### Round H — heartbeat (Dynamo)
- F00 dispatches `run-shura-dynamo.ps1` → 41 member pulses → `dist/evidence/shura-dynamo/`
- See [shura-heartbeat-core.md](shura-heartbeat-core.md)

---

## 4. Mandatory outputs

```markdown
# CEEF-NNN — Shura Council Consensus v2

## 1–8 (auto-generated)
→ shura-intelligence-core.md §7
```

---

## 5. When to activate Shura

| Trigger | Yes |
|---------|-----|
| live UI audit · CEEF audit | ✅ |
| launch / pricing / GTM decision | ✅ |
| omni-qa prompt governance SAVE | ✅ |
| typo fix | ❌ |
| explain-only question | ❌ |

**Supreme certified:** all **7** gates pass — report §10 · JSON `supremeCertified`.

**Verify constitution:**
```powershell
.\scripts\ops\validate-shura-constitution.ps1
```

---

## 6. Anti-patterns

| DO | DON'T |
|----|-------|
| v2 tool + 3b + F00 | theatrical text |
| save dissents in MEM | hide opposition |
| grep one leak file | load 279 files |
| separate in-repo from ⏭ | "market-ready" without OTP |

**Related:** [shura-intelligence-core.md](shura-intelligence-core.md) · `dream-team.md` · `brqosh-closure/SKILL.md`
