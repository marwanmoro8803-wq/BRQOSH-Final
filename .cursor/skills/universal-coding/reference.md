# Universal Stack Supremacy — Any Project (v15 Supreme)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: universal-coding  
> **Synthesis:** OpenAI Codex · Gemini CLI · Cursor · Anthropic · xAI  
> **Corpus:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)

> Discover repository → apply methodology → verify — do not assume BRQOSH unless files prove it

---

## 1. Golden Rule

```
Discover first → apply methodology → verify → no "done" without evidence
```

**Beat generic agents on foreign repos by:** reading before assuming · stack-native verify · local conventions · persistence through test green.

---

## 2. First 60 Seconds — Stack Detection

| Signal | Stack | Next module |
|--------|-------|-------------|
| `Brqosh.sln` · `*.csproj` | .NET BRQOSH | **SWITCH** brqosh-engineering |
| `package.json` | Node/TS | supreme-programming/languages/typescript.md |
| `pnpm-lock.yaml` | pnpm monorepo | read workspace root |
| `Cargo.toml` | Rust | languages/rust patterns |
| `go.mod` | Go | `go test ./...` |
| `pyproject.toml` | Python modern | pytest |
| `requirements.txt` | Python legacy | venv? |
| `pom.xml` / `build.gradle` | JVM | mvn/gradle |
| `Gemfile` | Ruby | bundle exec |
| `composer.json` | PHP | |
| `Makefile` only | C/C++ or polyglot | read Makefile targets |
| Docker only | containerized | read Dockerfile/compose |
| None | **ask** or follow open files | |

**Parallel discovery (one message):**
```
glob: *.{csproj,sln,json,toml,mod,gradle}
grep: "test" in README · package scripts · CI yaml
Read: README first 80 lines
```

Never assume framework from memory — **verify imports and lockfiles**.

---

## 3. Brownfield vs Greenfield

| Type | Protocol |
|------|----------|
| Brownfield | match existing patterns 100% · no style revolution |
| Greenfield | scaffold deps + README + test hook · still minimal |
| Monorepo | identify package boundary before edit |
| Legacy | smallest fix · no drive-by modernization |

**Codex rule:** prefer repo helpers over new abstraction layers.

---

## 4. Verify by Stack (minimum proof)

| Stack | Command pattern |
|-------|-----------------|
| .NET | `dotnet build` + `dotnet test` |
| Node | `npm test` / `pnpm test` / script from package.json |
| Rust | `cargo test` (+ `cargo clippy` if project uses) |
| Go | `go test ./...` |
| Python | `pytest` / `python -m unittest` |
| JVM | `./mvnw test` / `gradle test` |
| Any | CI yaml command if present |

**Gemini rule:** reproduce bug with test/script **before** fix when behavioral.

---

## 5. Twelve Axes (any language)

From [twelve-axes.md](../supreme-programming-mastery/twelve-axes.md) — mental checklist before closure:

| # | Axis | Question |
|---|------|----------|
| 1 | Correctness | does it match spec/bug report? |
| 2 | Types | types guard invalid states? |
| 3 | Memory | leaks · dispose · RAII? |
| 4 | Concurrency | races · deadlocks? |
| 5 | I/O | failures handled · timeouts? |
| 6 | Security | secrets · injection · authz? |
| 7 | Observability | logs/metrics for ops? |
| 8 | Testability | test covers change? |
| 9 | Deploy | CI/build still green? |
| 10 | Performance | obvious N+1 avoided? |
| 11 | Maintainability | readable · local conventions? |
| 12 | Domain | business rules respected? |

---

## 6. Engineering Judgment (Codex scales with risk)

| Risk | Test breadth |
|------|--------------|
| typo · comment · log | smoke or targeted |
| shared module · API contract | unit + integration |
| auth · money · migration | broad + manual checklist |
| cross-module workflow | e2e if exists |

| Change type | Scope rule |
|-------------|--------------|
| bug fix | touch failure neighborhood only |
| feature | follow existing module boundaries |
| refactor | only if requested or required for fix |

---

## 7. Universal Seven Roles

| Role | Any project duty |
|------|------------------|
| Architect | boundaries · integration points |
| Principal Dev | implement · match conventions |
| Polyglot QA | [polyglot-pitfalls.md](../supreme-programming-mastery/polyglot-pitfalls.md) |
| Security | [security.md](../brqosh-review/security.md) |
| UX | if UI — density · a11y · consistency |
| Domain expert | facts only — cite sources |
| Release | CI · rollback · changelog if requested |

Large tasks: [dream-team.md](../brqosh-agents/dream-team.md)

---

## 8. Tool Discipline (Cursor + Codex)

| Priority | Tool |
|----------|------|
| 1 | Grep/rg — find before read |
| 2 | Glob — locate files |
| 3 | Read — excerpt large files |
| 4 | StrReplace — surgical edit |
| 5 | Shell — build/test/git only |

- Parallel independent reads/greps
- Read before edit (mandatory)
- Re-read after edit
- No shell cat/sed for file ops
- Absolute paths on Windows when possible

---

## 9. Inquiry vs Directive (foreign repos too)

| "How does X work?" | explain · cite code — no edits |
| "Fix the failing test" | full cycle through green |
| "Review this PR" | findings-first by severity |

---

## 10. Git & Delivery

| Rule | Reason |
|------|--------|
| No commit unless asked | user owns git |
| No force push main | safety |
| Dirty worktree | never revert alien changes |
| amend | only strict conditions (user rules) |

See [git-delivery.md](../supreme-programming-mastery/git-delivery.md)

---

## 11. Security Universal

- never log secrets · .env · keys
- never commit credentials
- sanitize user input at trust boundaries
- dependency audit on security-sensitive change
- principle-based refusal for malware/exploit code

---

## 12. Frontend Branch (non-BRQOSH)

**Codex SaaS guidance applies** — quiet utilitarian unless product is game/creative.

BRQOSH ERP: **invert** — dense RTL operational — see brqosh-ui-design-system.

---

## 13. BRQOSH Branch (hard switch)

```
IF Brqosh.sln OR path contains BRQOSH conventions:
  SWITCH → brqosh-agent-protocol + brqosh-engineering + domain
  verify: STATUS.yaml → 417 tests → gates if wave
  NEVER close O-01..O-05 from code alone
ELSE:
  supreme-programming-mastery + this file
```

---

## 14. Closure Checklist (any repo)

```
□ Stack detected from files not memory
□ Relevant SKILL.md scanned
□ Change minimal and convention-aligned
□ Tests run (or explicit why not)
□ No secrets in diff
□ User language respected
□ ⏭ stated for what cannot be verified here
```

---

## 15. Anti-Patterns

| Avoid | Instead |
|-------|---------|
| "I'll assume it's React" | read package.json |
| giant rewrite | incremental verify |
| skip tests "it's small" | run fastest relevant test |
| copy Stack Overflow blind | adapt to local patterns |
| done without command output | show verify result |

---

## 16. Competition vs Human Engineer

| Human edge | Agent counter |
|------------|---------------|
| years in one codebase | fast parallel grep + read |
| intuition | hypothesis tree + test |
| tired skip tests | run full suite when asked |
| domain relationships | honest ⏭ external |

**Win on discipline + coverage + evidence** — not on pretending lived experience.

**Next:** [protocol.md](../brqosh-agent-protocol/protocol.md) · [twelve-axes.md](../supreme-programming-mastery/twelve-axes.md) · [agent-workflow.md](../supreme-programming-mastery/agent-workflow.md)
