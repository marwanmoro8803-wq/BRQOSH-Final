---
name: supreme-programming-mastery
description: >-
  Supreme agentic programming — Fable omni + 12 axes, all languages, SDLC.
  Attach fable-omni-core + human-intelligence-mastery for full understanding.
  Use for any code task or supreme programming / Fable coding requests.
---

# Supreme Programming Mastery

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Agentic code doctrine (Fable-class)

```
1. READ SKILL + CODE before write — project skills and repo patterns first
2. READ before EDIT — view/grep; re-read after each edit (context goes stale)
3. VERIFY before "done" — build, test, lint; tools not guesses
4. SMALLEST correct diff — surgical complete fix, not half-fix
5. PERSIST until verified — shell running = don't stop early
6. NO malicious code — ever
7. INFER from context — don't ask what's already in the thread/repo
```

## Load by task

| Task | Modules |
|------|---------|
| **Cursor / Cloud Code** | **claude-code-cli** + tools-cursor + agent-workflow |
| Karpathy discipline | [karpathy-synthesis.md](karpathy-synthesis.md) (merge — no duplicate rule) |
| Deliverable type | file-vs-inline |
| Bug | debug + languages/* for stack |
| New feature | architecture + testing + domain file |
| Review | review + security-coding |
| New repo / stack | stack-detect + languages/* |
| BRQOSH .NET | brqosh-bridge → `brqosh-engineering` |
| Tools in Cursor | tools-cursor · claude-code-cli |

## Twelve axes (always mental checklist)

Correctness · Types · Memory · Concurrency · I/O · Security · Observability · Testability · Deploy · Performance · Maintainability · Domain

Details: [twelve-axes.md](twelve-axes.md)

## Languages & stacks

| Area | File |
|------|------|
| C# / .NET | [languages/dotnet.md](languages/dotnet.md) |
| TypeScript / JS | [languages/typescript.md](languages/typescript.md) |
| Python | [languages/python.md](languages/python.md) |
| Rust | [languages/rust.md](languages/rust.md) |
| Go | [languages/go.md](languages/go.md) |
| SQL / data | [languages/sql.md](languages/sql.md) |
| C / C++ | [languages/cpp.md](languages/cpp.md) |
| Java / Kotlin | [languages/jvm.md](languages/jvm.md) |
| Swift | [languages/swift.md](languages/swift.md) |
| PHP / Ruby | [languages/scripting.md](languages/scripting.md) |
| Polyglot pitfalls | [polyglot-pitfalls.md](polyglot-pitfalls.md) |

## SDLC modules

| Phase | File |
|-------|------|
| Architecture | [architecture.md](architecture.md) |
| API design | [api-design.md](api-design.md) |
| Database | [database.md](database.md) |
| Frontend / UI code | [frontend.md](frontend.md) |
| Testing | [testing.md](testing.md) |
| Security | [security-coding.md](security-coding.md) |
| Debug | [debug.md](debug.md) |
| Git / delivery | [git-delivery.md](git-delivery.md) |
| DevOps / CI | [devops.md](devops.md) |
| Paradigms | [paradigms.md](paradigms.md) |
| Anti-patterns | [anti-patterns.md](anti-patterns.md) |
| Karpathy (4 principles) | [karpathy-synthesis.md](karpathy-synthesis.md) |

## Cycle

```
SCOPE -> READ (skills+code) -> PLAN -> IMPLEMENT (minimal) -> VERIFY -> REPORT
```

## Related skills

- Cognition/ethics: `human-intelligence-mastery`
- BRQOSH ERP: `brqosh-engineering` + `brqosh-accounting`
- Hub: `brqosh-skills-hub`

---

## Engineering Roadmap

> **Machine index:** [FILE-ROADMAP.yaml](FILE-ROADMAP.yaml)  
> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Default path:** `P-CODE`

## Default path (8 steps)

```
0 SKILL → 1 skills-first → 2 agent-workflow+file-vs-inline → 3 twelve-axes
→ 4 languages/* or SDLC/* → 5 implement → 6 testing → 7 review → 8 git-delivery
```

---

### `skills-first.md` · L1 · 1/8 — **MANDATORY before code**

| | |
|--|--|
| **TRIGGER** | Before first line of code or new file |
| **FABLE** | `skills` (computer_use) |
| **EXIT** | All relevant SKILL.md read · fable-omni + brqosh-* checked |
| **ANTI** | Code before skills · only one skill when several apply |
| **VERIFY** | Name skills you read |
| **TIME** | 60s |

---

### `agent-workflow.md` · L1 · 2/8

| | |
|--|--|
| **TRIGGER** | implement · fix · refactor |
| **EXIT** | 5-step plan · read before edit · re-read after patch |
| **ANTI** | Suggestion without execution · stale context |
| **NEXT** | twelve-axes + stack module |
| **TIME** | 45s |

---

### `file-vs-inline.md` · L1 · 2b/8

| | |
|--|--|
| **FABLE** | `file_creation_advice` |
| **EXIT** | Artifact vs inline decided |
| **ANTI** | 200 lines in chat · unsolicited markdown |

---

### `twelve-axes.md` · L1 · 3/8 — mental checklist

| | |
|--|--|
| **TRIGGER** | Every code change |
| **EXIT** | 12 axes reviewed |
| **TIME** | 30s |

---

### `languages/{stack}.md` · L3 · 5/8

| Manifest | File |
|----------|------|
| *.csproj | languages/dotnet.md → **brqosh-engineering** |
| package.json | languages/typescript.md |
| pyproject.toml | languages/python.md |
| Cargo.toml | languages/rust.md |
| go.mod | languages/go.md |

---

### `testing.md` · L1 · 7/8 — before "done"

| | |
|--|--|
| **EXIT** | Test command executed · real result |
| **ANTI** | Claim pass without dotnet test |

---

### SDLC modules (L3 — as needed)

| File | TRIGGER |
|------|---------|
| architecture | large feature |
| api-design | endpoints |
| database | schema/migration |
| security-coding | auth/secrets |
| debug | general bug → brqosh-debug if BRQOSH |
| review | PR → brqosh-review if BRQOSH |
| git-delivery | commit/PR |

**Detail:** [FILE-ROADMAP.yaml](FILE-ROADMAP.yaml)

## Scenarios

| Scenario | Path |
|----------|------|
| Fix BRQOSH test | skills-first → agent-workflow → dotnet → brqosh-debug |
| New API | skills-first → architecture → api-design → testing |
| Go repo | skills-first → universal-coding → languages/go |

