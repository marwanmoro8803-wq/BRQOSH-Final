# Prompt Audit Engine — Universal Dynamic Harness

> **Law:** [prompt-source-law.md](prompt-source-law.md) PS06 — harness allowed · cognitive-qa forbidden  
> **Tool:** `PromptAuditEngine/` · **Shura:** reads `dist/evidence/prompt-audit/latest.json`  
> **Stacks:** [universal-coding/reference.md](../universal-coding/reference.md)  
> **Leaks:** grep only via [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)

---

## Purpose

Always-on **multi-language audit harness** that embodies البرومبيت principles via **skills + evidence** — not the removed Python `cognitive-qa/` wave.

```
PROMPT IDEAS → SKILLS (omni-qa-army) → PAE (C#) → dist/evidence → SHURA → AGENT
```

| Layer | What runs |
|-------|-----------|
| **Skills** | 10 QA roles · 12 axes · report-excellence · prompt-source-law |
| **PAE** | stack detect · build/test · linkage · workflow strength |
| **Shura** | DP1–DP6 · C-PROMPT-* · supreme gates |
| **Agent** | acts on findings · never claims desktop UI audit without FlaUI |

---

## Immutable laws

| ID | Law |
|----|-----|
| PAE01 | PAE is **governance harness** — not full prompt Python implementation |
| PAE02 | **MUST** detect stack from files — never assume language |
| PAE03 | **MUST** write `dist/evidence/prompt-audit/latest.{md,json}` every run |
| PAE04 | **MUST NOT** recreate `cognitive-qa/` without user release phrase |
| PAE05 | Watch mode default **300s** — agent may also run on-demand |
| PAE06 | Strength score = linkage + workflow + verify (when run) |

---

## Phases (maps to prompt orchestrator — skill only)

| Phase | PAE action | Prompt engine analog |
|-------|------------|---------------------|
| **DISCOVER** | `StackDetector` | hyper-dimensional state map (file-level) |
| **VERIFY** | `VerifyRunner` | test oracles · Codex verify-before-close |
| **LINK** | `LinkageAnalyzer` | cross-correlation · project refs |
| **WORKFLOW** | `WorkflowAnalyzer` | governance chain · Shura · skills |
| **REPORT** | `AuditReportWriter` | report-excellence severity path |
| **COUNCIL** | agent runs Shura on CEEF SAVE | omniscient overseer |

---

## Stack coverage

| Stack | Detect | Verify (default) |
|-------|--------|------------------|
| .NET | `*.sln` / `*.csproj` | `dotnet build` + `dotnet test` |
| Node | `package.json` | `npm test` · `npm run build` |
| Python | `pyproject.toml` / `requirements.txt` | `pytest` or `compileall` |
| Rust | `Cargo.toml` | `cargo check` |
| Go | `go.mod` | `go test ./...` |
| JVM | `pom.xml` / `gradle` | `mvn test` / `gradle test` |
| Generic | skills-only repo | linkage + workflow only |

---

## Always-on operation

```powershell
# Single audit
.\scripts\ops\run-prompt-audit-engine.ps1

# Watch every 5 minutes (daemon)
.\scripts\ops\run-prompt-audit-engine.ps1 -Watch

# Fast governance-only (no compile)
.\scripts\ops\run-prompt-audit-engine.ps1 -SkipBuild

# Audit + Shura council
.\scripts\ops\run-prompt-audit-engine.ps1 -RunShura
```

---

## Agent pipeline (every substantive session)

```
1. Read prompt-source-law + this file
2. Run PAE (or read latest.json if <1h fresh)
3. If strength < 70 or PARTIAL → fix linkage/workflow first
4. On wave close → SAVE CEEF-PROMPT-* → Shura auto-run
5. Never claim prompt desktop QA complete without user release + FlaUI
```

---

## Shura integration

| Evidence | Path |
|----------|------|
| Latest audit | `dist/evidence/prompt-audit/latest.json` |
| Council | `dist/evidence/shura-council/council-*.md` |
| CEEF SAVE | `dist/evidence/prompt-governance/CEEF-PROMPT-002.md` |

**Decision DP6:** PAE harness approved for universal code/workflow audit.

---

## Verify

```powershell
dotnet build PromptAuditEngine/PromptAuditEngine.csproj -c Release
.\scripts\ops\run-prompt-audit-engine.ps1 -SkipBuild
Test-Path cognitive-qa   # MUST be False
```

**Related:** [orchestrator-law.md](orchestrator-law.md) · [superintelligence-core.md](superintelligence-core.md) · [shura-prompt-governance.md](../brqosh-analysis/shura-prompt-governance.md) · [karpathy-synthesis.md](../supreme-programming-mastery/karpathy-synthesis.md) (goal-driven verify)
