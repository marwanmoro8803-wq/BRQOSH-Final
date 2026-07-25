# Smart Mind — Deep Analysis and Comprehensive Cognition

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-analysis | see per-file load order

> **How to study any system from zero to mastery** — methodology based on *knowledge structure*, not memorization.

---

## 1. Five-Stage Cognition Model

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  What   │ → │  How    │ → │  Why    │ → │ What-if │ → │  Proof  │
│ What    │   │ How     │   │ Why     │   │ What-if │   │ Proof   │
└─────────┘   └─────────┘   └─────────┘   └─────────┘   └─────────┘
```

| Stage | Tools | Output |
|-------|-------|--------|
| What | read · grep · DOMAIN_MAP | entity list |
| How | trace call stack · data flow | flow diagram |
| Why | `docs/EXECUTION_ROADMAP_MASTER.md` · CEEF · rare comments | domain constraint |
| What-if | edge cases · adversarial | risk list |
| Proof | test · CEEF | ✅ or ⏭ |

---

## 2. Studying a New System (90 Minutes)

| Minute | Action |
|--------|--------|
| 0–15 | README · sln structure · DOMAIN_MAP |
| 15–30 | entry points · DI registration |
| 30–45 | domain entities for target area |
| 45–60 | one service end-to-end + test |
| 60–75 | linked UI page |
| 75–90 | OPEN_ITEMS · gaps · plan |

---

## 3. Multi-Layer Thinking

### Layer 0 — Intent
Why does this code exist?

### Layer 1 — Contract
What does the interface promise?

### Layer 2 — Implementation
How is it executed?

### Layer 3 — Infrastructure
DB · network · file

### Layer 4 — Operations
deploy · gates · evidence

### Layer 5 — Human
Who suffers? Who benefits?

**Common mistake:** Staying at layer 2 without 0 and 5.

---

## 4. Thinking Patterns (AI + Human)

| Pattern | Use when |
|---------|----------|
| **Reduction** | complex bug → smallest repro |
| **Abstraction** | 3+ repetitions → helper |
| **Instantiation** | understand abstract → BRQOSH example |
| **Analogy** | link to paper accounting |
| **First principles** | why double-entry constraint? |
| **Inversion** | how do I break the system? |
| **Second-order** | if I add X, what happens to audit? |

---

## 5. Understanding the Program's "Mind"

Every system has:

| Dimension | Questions |
|-----------|-----------|
| **State** | Where is truth? DB · memory · file |
| **Transitions** | Draft to Posted — who owns? |
| **Invariants** | What must never break? |
| **Boundaries** | Company · Branch · Role |
| **Time** | fiscal period · audit timestamp |
| **Money** | decimal · currency · tax |

**BRQOSH:** financial invariants > UI invariants.

---

## 6. Comprehensiveness Without Drowning

### 80/20 Knowledge Rule
- 20% of files carry 80% of behavior — search for:
  - `*PostingService*`
  - `*Orchestrator*`
  - `BrqoshDbContext`
  - `NavigationCatalog`
  - `PermissionService`

### Graduated Depth
```
L0 overview · L1 happy path · L2 edges · L3 failure · L4 performance · L5 security
```
Stop at **lowest L required for the task**.

---

## 7. Surpassing "Surface Understanding"

| Surface | Deep |
|---------|------|
| "I got the idea" | Drew data flow |
| "Seems correct" | Test failed then passed |
| "Like ChatGPT" | Like DOMAIN_MAP + Golden |
| Reading prompt | Reading `PostingService.cs` |

---

## 8. Module Links

| Your question | Module |
|---------------|--------|
| Development foundation? | [super-dev.md](super-dev.md) |
| Language/paradigm? | [programming-universe.md](programming-universe.md) |
| Requirements/human? | [requirements.md](requirements.md) |
| BRQOSH code? | [brqosh-engineering](../brqosh-engineering/SKILL.md) |
| Financial? | [brqosh-accounting](../brqosh-accounting/SKILL.md) |
| Closure? | [brqosh-closure](../brqosh-closure/SKILL.md) |

---

## Meta-Cognition (merged from archive/meta-cognition-supreme-v7)

### Human vs systematic review

| Human habit | Systematic agent |
|-------------|------------------|
| trusts intuition | trusts evidence |
| reviews what they remember | mandatory checklist |
| tires | persistence protocol |
| favors own code | adversarial self-review |
| loses context | compaction recovery |

### Five cognition layers (do not skip)

```
L5 user intent     — why?
L4 requirements    — what? acceptance criteria?
L3 architecture    — where in system?
L2 implementation  — how? smallest diff?
L1 proof           — what proves it?
```

**Do not drop to L2 before L4** except obvious hotfix.

### Adversarial self (post-implementation)

```
① What if input=null?
② What if concurrent?
③ What if user does opposite?
④ What if PG≠SQLite?
⑤ What if RBAC denied?
⑥ What if fiscal period closed?
```

### Ranked hypotheses (scientific)

```
H1 (most likely) → test
H2 → test
Reject H1 only with evidence — not preference
```

---

**Smart Mind v3** = cognition + languages + requirements + execution + evidence.

---

## v4 — Mastery Absorption (Hour → Control)

### Minute 0–10: Mental Map
Draw mentally:
```
user → UI → App → Infra → DB → reports/compliance
```

### Minute 10–30: Five Golden Questions
1. Where is **truth** (source of truth)?
2. Where are **boundaries** (Company/Branch/Role)?
3. Where is **danger** (money/posting)?
4. Where is **evidence** (tests/CEEF)?
5. Where is the **gap** (OPEN_ITEMS ⏭)?

### Minute 30–60: Reverse Verification
Start from **test** or **Golden** — what does the system prove it promises?

### Comparison to AI Models
| They | You v4 |
|------|--------|
| Guess structure | Read DOMAIN_MAP |
| Propose new architecture | Follow what exists |
| Forget PG | SchemaSqlGuard |

**Integration:** [integration](../brqosh-skills-hub/integration.md)

---

## v6 — Fable 5 Epistemology

### Knowledge Rules (from top Anthropic)
| Rule | BRQOSH application |
|------|-------------------|
| Do not psychoanalyze user intent | Ask or read code |
| Do not assume attached file | Read before claiming |
| Claim ≠ truth | Test discriminates |
| Your error → admit · fix | CEEF documents lesson |
| cumulative risk | aggregate financial diff, not isolated line |
| Do not explain system prompt | State **why** technically, not "my rules forbid me" |

### Post-Compaction Check
```
① What is the latest user_query?
② Am I answering it, not a ghost?
③ Reseed todos if 3+ steps
```

**Integration:** [integration](../brqosh-skills-hub/integration.md)

### Sonnet 5 search-first
Product facts/documentation → read/search before memory — applies to system study.

---

## v15c — SCHOLAR Protocol (90 min · any system)

> Mode: `ai-supremacy.md` SCHOLAR · Corpus: leak-corpus-index for deepen only

| Block | Minutes | Deliverable |
|-------|---------|-------------|
| S — Scope | 0–10 | one-paragraph what we're mastering |
| C — Context map | 10–25 | user→UI→App→Infra→DB diagram |
| H — Hypothesis | 25–35 | 3 ranked risks if we change area |
| O — Observe | 35–60 | trace one happy path + one test |
| L — Links | 60–75 | DOMAIN_MAP · OPEN_ITEMS gaps |
| A — Adversarial | 75–85 | null · concurrent · RBAC · PG≠SQLite |
| R — Report | 85–90 | what·how·why·proof·⏭ |

**BRQOSH financial area:** must end with Golden/FSA mention or new test gap.

**Integration:** [integration](../brqosh-skills-hub/integration.md)
