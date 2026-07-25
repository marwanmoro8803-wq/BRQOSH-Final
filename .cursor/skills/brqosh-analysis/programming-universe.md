# Smart Mind — Programming Universe

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-analysis | see per-file load order

> **Framework for understanding every language and paradigm** — not syntax memorization, but *the mind of each language*: what it solves · what it punishes · when to choose it.

---

## Index

1. [Four Paradigms](#1-four-paradigms)
2. [Language Maps](#2-language-maps)
3. [Mind of Each Family](#3-mind-of-each-family)
4. [.NET / BRQOSH](#4-net--brqosh)
5. [Language Requirements](#5-language-requirements)
6. [Design Across Languages](#6-design-across-languages)
7. [Comprehensiveness and Fine Detail](#7-comprehensiveness-and-fine-detail)
8. [Language Choice (Decision)](#8-language-choice-decision)

---

## 1. Four Paradigms

| Paradigm | Mind | Strength | Weakness | Examples |
|----------|------|----------|----------|----------|
| **Procedural** | steps · sequence · state | performance · control | spaghetti without structure | C · Bash |
| **Object-oriented (OOP)** | entities · encapsulation | domain modeling | coupling · deep inheritance | C# · Java |
| **Functional** | immutability · pure functions | test · parallel | awkward I/O | Haskell · F# subsets |
| **Declarative** | what you want, not how | SQL · UI | hard complex logic | SQL · XAML |

**BRQOSH:** OOP in Domain · declarative in XAML · procedural in scripts · functional in queries/pure rules.

---

## 2. Language Maps

### Systems
| Language | Mind | Requirements | When |
|----------|------|--------------|------|
| **C** | memory · pointers · UB | manual memory | kernels · embedded |
| **C++** | RAII · zero-cost | ownership | games · perf |
| **Rust** | borrow checker | compile-time safety | systems safe |
| **Go** | goroutines · simplicity | GC · explicit errors | services · CLI |

### Application Platforms
| Language | Mind | Requirements |
|----------|------|--------------|
| **C#** | types · async · LINQ · ecosystem | .NET runtime · nullable ref |
| **Java** | JVM · enterprise patterns | verbosity · Spring culture |
| **Kotlin** | null-safety · coroutines | JVM/Android |
| **TypeScript** | types on JS | build chain · npm |
| **Python** | readability · batteries | GIL · typing optional |
| **Ruby** | DX · metaprogramming | perf · magic |

### Web
| Layer | Mind |
|-------|------|
| HTML | semantic structure |
| CSS | cascade · layout · tokens |
| JS/TS | event loop · async · DOM |
| SQL | relations · ACID · plans |

### BRQOSH stack (center)
```
C# 9 · WPF · EF Core · PostgreSQL/SQLite · PowerShell ops
```

---

## 3. Mind of Each Family

### How to "think" C#
- types first · nullable · pattern matching
- async all the way for I/O
- LINQ = declarative on collections
- attributes · reflection · source generators
- **DI culture** — no `new` for services in UI

### How to "think" SQL
- sets not loops
- indexes · plans · transactions
- migrations = contracts with production
- **BRQOSH:** SchemaSqlGuard + EF + updater

### How to "think" XAML/WPF
- trees · bindings · styles · resources
- code-behind = orchestration not business logic
- RTL = first-class in BRQOSH
- visual state = triggers · converters

### How to "think" PowerShell
- pipelines · objects
- ops scripts · evidence · gates
- `;` not always `&&`

### How to "think" Python (when needed)
- scripts · data · ML — **not** core BRQOSH
- venv · requirements.txt

### How to "think" JavaScript/TS
- async · modules · npm
- **BRQOSH:** not primary stack — Api may consume JSON

---

## 4. .NET / BRQOSH

### Mind Layers in the Project

```
┌─────────────────────────────────────┐
│ WPF (display · events · binding)    │
├─────────────────────────────────────┤
│ Application (contracts · orchestration)│
├─────────────────────────────────────┤
│ Infrastructure (EF · ZATCA · post)  │
├─────────────────────────────────────┤
│ Domain (rules · entities · events)  │
└─────────────────────────────────────┘
```

### .NET Requirements in BRQOSH
| Requirement | Application |
|-------------|-------------|
| Nullable | enable · treat warnings |
| async | DbContext · HTTP |
| DI | scoped services |
| Testing | xUnit · in-memory DB |
| Config | appsettings · env |

---

## 5. Language Requirements

### Table: What does each language impose?

| Dimension | C# | SQL | XAML | PS |
|-----------|----|----|------|-----|
| Types | compile-time | schema | property types | dynamic |
| Memory | GC | server | UI trees | GC |
| Concurrency | async/lock | transactions | dispatcher | runspaces |
| Errors | exceptions | constraints | binding errors | terminating |
| Packaging | csproj/nuget | migrations | resource dicts | modules |
| Test | xUnit | scripts/fixtures | UI tests | Pester |

**Rule:** Before writing in a language — **state its top three requirements** for the current task.

---

## 6. Design Across Languages

### Universal Principles (beyond language)

1. **Single Responsibility** — file · class · function
2. **Explicit boundaries** — interfaces between layers
3. **Fail fast** — validate at edge
4. **Idempotency** — for sensitive operations (Api)
5. **Audit trail** — for money

### Universal Anti-Patterns
- God object · spaghetti · copy-paste
- premature optimization
- magic numbers/strings
- business logic in UI

---

## 7. Comprehensiveness and Fine Detail

### Checklist: Understand a System in a Given Language

```
□ toolchain (compiler · runtime · package manager)
□ entry points
□ module boundaries
□ data flow (input → persist → output)
□ error model
□ concurrency model
□ test harness
□ deploy artifact
□ observability (logs · audit)
```

### BRQOSH Precision — Details Not to Forget

| Area | Fine detail |
|------|-------------|
| Money | decimal · rounding policy · currency |
| Time | UTC store · local display · Hijri |
| Isolation | CompanyId · BranchId · tenant future |
| Security | hash passwords · RBAC · no secrets in log |
| schema | SQLite type ≠ PG type always |
| UI | RTL column star width · flow direction |
| ZATCA | XML schema · QR TLV · CSID lifecycle |

---

## 8. Language Choice (Decision)

```
IF project == BRQOSH:
  USE C# + WPF + SQL via EF
ELIF script ops:
  USE PowerShell
ELIF one-off analysis:
  MAYBE Python — not in src/ core
ELSE:
  FOLLOW repo convention — NEVER impose foreign stack
```

**For competition:** Depth in **project stack** > surface knowledge of 50 languages.

---

**Execution reference:** [engineering.md](engineering.md) · **Requirements:** [requirements-human-mind.md](requirements-human-mind.md)

---

## v4 — Extended Language Encyclopedia (mind of each stack)

| Language | Mind in one sentence | BRQOSH |
|----------|---------------------|--------|
| **Rust** | ownership at compile-time | not stack |
| **Go** | simplicity · goroutines | O-10 future |
| **Swift/Kotlin** | mobile native | O-13 ⏭ |
| **PHP/Ruby** | web MVC | not here |
| **R/MATLAB** | stats | external |
| **Bash** | pipes · exit codes | scripts |
| **GraphQL** | schema-first | REST currently |

**Advantage:** Master C#/SQL/XAML/PS + other paradigms when needed.

---

## v6 — Codex judgment (no foreign stack)

| Codex 5.5 | BRQOSH |
|-----------|--------|
| match repo patterns | Domain→Application→Infra |
| no invent framework | no React in WPF |
| structured APIs | records · decimal · EF |
| risk-scaled tests | Golden for money |

**Integration:** [engineering.md](engineering.md) · [integration](../brqosh-skills-hub/integration.md) · [polyglot-pitfalls-v7.md](polyglot-pitfalls-v7.md)

---

## v7 — Tier A-D Links

| Tier | Detail file |
|------|-------------|
| A-D axes | [programming-supremacy-v7.md](programming-supremacy-v7.md) |
| Per-language pitfalls | [polyglot-pitfalls-v7.md](polyglot-pitfalls-v7.md) |
