# Karpathy Synthesis — Four Principles (Extracted)

> **Source:** `andrej-karpathy-skills-main/` — principles only · **not** bulk copy  
> **Index:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml) · family `Karpathy`  
> **Attribution:** [Andrej Karpathy observations](https://x.com/karpathy/status/2015883857489522876) via forrestchang/andrej-karpathy-skills (MIT)  
> **Shura:** CEEF-KARP-001 — synthesize into existing stack · **no** duplicate `alwaysApply` rule

---

## 1. Why this module exists

~75% overlap with `fable-omni-core` · `agent-workflow` · `brqosh-supreme-cortex` · Codex leaks.  
**Unique value:** crisp naming · `EXAMPLES.md` anti-patterns · explicit verify-plan template.

**Law:** Merge here — do **not** add second always-on rule alongside `brqosh-supreme-cortex.mdc`.

---

## 2. Four principles → our stack

| # | Karpathy | Enforced in |
|---|----------|-------------|
| K1 | **Think Before Coding** — surface assumptions · ask · present forks | `fable-omni-core` classify · `impact-before-change` · Inquiry mode |
| K2 | **Simplicity First** — minimum code · no speculative features | `supreme-programming` §smallest diff · user scope law |
| K3 | **Surgical Changes** — every line traces to request · no drive-by | `agent-workflow` ③ · `tools-cursor` StrReplace |
| K4 | **Goal-Driven Execution** — verifiable success · repro-first | `brqosh-debug` · `PromptAuditEngine` · persistence law |

**Tradeoff (Karpathy):** biases toward caution over speed — use judgment on L0 BLITZ typos.

---

## 3. Verify-plan template (K4 — use every multi-step task)

```
1. [Step] → verify: [command or observable]
2. [Step] → verify: [command or observable]
3. [Step] → verify: [command or observable]
```

| Vague request | Verifiable rewrite |
|---------------|-------------------|
| "Fix auth" | Write test reproducing bug → green → full suite |
| "Add validation" | Tests for invalid inputs → implement → pass |
| "Make search faster" | Clarify latency vs throughput → benchmark before/after |

---

## 4. Anti-patterns (from EXAMPLES — paraphrased)

| Principle | Anti-pattern | Do instead |
|-----------|--------------|------------|
| K1 | Silent scope/format assumptions on "export data" | List assumptions · one question max then best-effort |
| K2 | Strategy pattern for one discount function | One function until second use case proves need |
| K3 | Type hints + quote reformats while fixing empty-email bug | Touch only failing lines |
| K4 | "Review and improve" without criteria | Repro test → fix → regression command |

Full examples: grep `andrej-karpathy-skills-main/EXAMPLES.md` — do not paste into skills.

---

## 5. Routing

| Task signal | Read first |
|-------------|------------|
| Any code change | `agent-workflow.md` + this file |
| Bug / test red | `brqosh-debug` + K4 repro-first |
| Review / refactor request | K3 surgical + `brqosh-review/simplify.md` |
| Ambiguous feature | K1 think-before + `requirements.md` |
| Prompt / QA harness wave | `prompt-audit-engine.md` + K4 verify plan |

---

## 6. Forbidden

| DO NOT | DO INSTEAD |
|--------|------------|
| Copy `karpathy-guidelines.mdc` as always-on | This synthesis module + supreme-cortex |
| Paste EXAMPLES.md into SKILL.md | `skill-lessons.yaml` L-KARP-* |
| Claim Karpathy = new intelligence layer | Route to existing modules above |

---

## 7. Verify

```powershell
Test-Path .cursor/skills/supreme-programming-mastery/karpathy-synthesis.md
rg "Karpathy:" .cursor/skills/brqosh-skills-hub/leak-corpus-index.yaml
```

**Related:** [agent-workflow.md](agent-workflow.md) · [skills-first.md](skills-first.md) · [prompt-audit-engine.md](../omni-qa-army/prompt-audit-engine.md)
