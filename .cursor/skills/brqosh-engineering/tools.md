# Tools — BRQOSH runtime specifics (v16)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-engineering

> **Canonical tool guide (v16):** [../supreme-programming-mastery/tools-cursor.md](../supreme-programming-mastery/tools-cursor.md)  
> Tool hierarchy · parallelism · grep/read strategy · MCP protocol · edit rules · anti-patterns — **all live there. Do not duplicate here.**

This file keeps only what is **BRQOSH-specific**.

---

## 1. Shell — BRQOSH/Windows rules

| Rule | Why |
|------|-----|
| PowerShell `;` not `&&` | Windows shell |
| `--no-pager` on git | Warp-safe |
| HEREDOC commit messages | formatting safety |
| background + Await for `dotnet test` | Application suite ~70s |
| no interactive DB clients | non-interactive harness |

---

## 2. Verify toolchain (canonical commands)

```powershell
cd C:\Users\qc\Projects\BRQOSH
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet build Brqosh.sln -c Release --nologo
dotnet test Brqosh.sln -c Release --nologo
.\scripts\ops\run-external-gates.ps1
.\scripts\ops\agent-skill-regression.ps1
```

| Change touches | Extra gate |
|----------------|-----------|
| Persistence / schema | `schema-gate-pg` checklist (`.cursor/rules/brqosh-schema-governance.mdc`) |
| Financial posting | Golden + FSA (`tools/FullScenarioAudit`) |
| Skills structure | `agent-skill-regression.ps1` |
| Desktop UI | run `Brqosh.Desktop.exe` critical path |

---

## 3. BRQOSH Tool ← Task map (domain additions)

| Task | Tool |
|------|------|
| ZATCA documentation | WebSearch + `docs/ops/O-01_*` |
| session/RBAC question | grep `SecurityContext` + `AppServices` |
| schema type question | Read `SchemaSqlGuard.cs` |
| test counts / phase | Read `brqosh-skills-hub/STATUS.yaml` |
| evidence for closure | `dist/evidence/` + CEEF report |

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [../brqosh-agents/reference.md](../brqosh-agents/reference.md) · [engineering.md](engineering.md)
