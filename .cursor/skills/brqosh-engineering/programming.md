# Smart Mind v7 — Universal Programming Supremacy

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-engineering | see per-file load order

> **Surpassing human cognition** = system · breadth · no forgetting — not syntax memorization

**Complements:** [programming-universe.md](programming-universe.md) · [polyglot-pitfalls-v7.md](polyglot-pitfalls-v7.md)

---

## 1. Equation for Surpassing the Human Engineer

```
Genius human = 20 years experience × limited memory × fatigue × bias
Mind v7      = 12 axes × checklist × no forgetting × test proof
```

| Humans | v7 |
|--------|-----|
| Forget a review angle | xhigh 10 + polyglot pitfalls |
| Fatigue after 4 hours | persist until verify |
| Assume library exists | grep `.csproj` first |
| Mix financial and UI concerns | separate I vs UI angles |
| "Looks correct" | dotnet test |

---

## 2. The 12 Axes (Every Software Project)

| # | Axis | Question never skipped |
|---|------|------------------------|
| 1 | **Correctness** | Is behavior correct for every input? |
| 2 | **Types** | Does the type system prevent the error? |
| 3 | **Memory** | leaks · dispose · scope |
| 4 | **Concurrency** | race · deadlock · async |
| 5 | **I/O** | N+1 · timeout · retry idempotent |
| 6 | **Security** | injection · authz · secrets |
| 7 | **Observability** | logs · audit · errors |
| 8 | **Testability** | unit · integration · repro |
| 9 | **Deploy** | migrations · rollback |
| 10 | **Performance** | hot path · allocation |
| 11 | **Maintainability** | naming · boundaries |
| 12 | **Domain** | business rules — BRQOSH: financial |

**Rule:** large programming task → walk all 12 mentally before closure.

---

## 3. "Read the Full System" Method (Beyond Human Limits)

### 90 Minutes — Any New Codebase
```
0–15   tree + DOMAIN_MAP + entry points
15–35  grep interfaces · *Service · *Page
35–55  tests mirror (what proves the system?)
55–75  data model (entities · migrations)
75–90  risk map (financial · security · ⏭)
```

### 4 Questions 90% of Humans Never Ask
1. **What breaks the system silently?** (no exception — wrong result)
2. **Where is the source of truth?** (DB · event · cache)
3. **What hidden assumption does the last change carry?**
4. **Which test should fail but was never written?**

---

## 4. Language Pyramid — Depth, Not Surface

### Tier A — BRQOSH Mastery (mandatory)
C# 12 · EF Core · SQL · XAML · PowerShell

### Tier B — Read and Review (when touched)
TypeScript · Python · JSON/YAML · Bash

### Tier C — Architectural Understanding (when integrating)
Rust · Go · Java/Kotlin · Swift · PHP

### Tier D — Awareness (no execution unless requested)
R · MATLAB · Solidity · GraphQL · WASM

**Pitfall details:** [polyglot-pitfalls-v7.md](polyglot-pitfalls-v7.md)

---

## 5. Architectural Patterns (Choose, Do Not Invent)

| Pattern | When | BRQOSH |
|---------|------|--------|
| Layered | ERP · enterprise | ✅ Domain/App/Infra |
| CQRS-lite | read ≠ write heavy | reports vs posting |
| Event notification | audit | IDomainEventPublisher |
| Repository | testability | via EF |
| Microservices | ❌ not now | monolith modular |
| Serverless | ❌ | desktop + api |

---

## 6. Code Lifecycle (Idea to Production)

```
Spec → Spike(optional) → Test-first → Implement → Review xhigh
→ Security → Simplify → CI → CEEF → Monitor
```

**Human shortcut:** Spec → code → "works on my machine"  
**v7:** Test-first + Golden for financial logic

---

## 7. Codex 5.5 — Global Engineering Judgment

| Situation | v7 decision |
|-----------|-------------|
| Open details | most conservative + repo-consistent |
| structured data | strong types/APIs — no string hacks |
| abstraction | only on real duplication or local pattern |
| test scope | narrow=focused · wide=Golden+integration |
| frontend | ERP dense — no Codex marketing |

---

## 8. Explicit Superiority Over Leaks (Programming)

| Leak weakness | v7 |
|---------------|-----|
| Generic, no project | BRQOSH + Tier A depth |
| Contradictory | unified 12 axes |
| No test proof | 417+ |
| UI slop in Codex | inverted design |
| No polyglot pitfalls | encyclopedia |

---

## 9. SUPREME Daily Checklist (Programming)

- [ ] 12 axes — any gap?
- [ ] polyglot pitfalls for languages in diff
- [ ] xhigh 10 if review
- [ ] repro test before fix
- [ ] persist until gates
- [ ] no foreign stack in BRQOSH

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [tools.md](tools.md)

---

## CODE COUNCIL (C01)

Programming = C01 depth · diagnosis: [rapid-diagnosis-v11.md](rapid-diagnosis-v11.md) · council: [routing](routing.md) §C01
