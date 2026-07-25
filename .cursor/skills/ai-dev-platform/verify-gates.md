# Verify Gates — Platform Closure Law

> **Closure reference:** [reference.md](../brqosh-closure/reference.md) · **Workflow:** [agent-workflow.md](../supreme-programming-mastery/agent-workflow.md)

---

## 4 closure levels (platform)

| Level | Proof | Platform example |
|-------|-------|------------------|
| **L1 Code** | builds · files exist | `dotnet build` PASS |
| **L2 Test** | automated tests | `dotnet test` PASS · count in STATUS |
| **L3 Live** | runtime observe | app runs · audit script |
| **L4 External** | out-of-repo gate | hosted deploy · OTP · scale test |

**MUST NOT** present L1 as L4.

---

## Gate matrix by artifact

| Artifact | L1 | L2 | L3 | Notes |
|----------|----|----|-----|-------|
| C# module | build | unit tests | integration | |
| Skill module | link check | Arabic grep | regression script | |
| Shura change | build ShuraCouncil | run council | evidence md | exit code |
| Architecture CEEF | doc exists | Shura exit 0 | — | MEM row |
| UI surface | build | Desktop.Tests | FlaUI if present | |

---

## Pre-done checklist (binding)

```
□ Classification correct (fix vs analyze vs close)
□ Skills read for domain
□ Smallest diff applied
□ Build PASS
□ Test PASS (if behavior change)
□ No Arabic introduced in skills
□ MEM row if substantive
□ Shura if SAVE analysis
□ OPEN_ITEMS / EXTERNAL labeled if applicable
```

---

## Verify commands (this workspace)

```powershell
# Skills language
rg "[\u0600-\u06FF]" .cursor/skills/

# Shura tool
dotnet build ShuraCouncil/ShuraCouncil.csproj -c Release
dotnet run --project ShuraCouncil/ShuraCouncil.csproj -c Release -- --source "reports/CEEF-*.md"

# BRQOSH main repo (when linked)
# dotnet build Brqosh.sln -c Release
# dotnet test Brqosh.sln -c Release
```

---

## Failure protocol

| Fail | MUST |
|------|------|
| Build | fix before report |
| Test | diagnose tree · retry |
| Shura exit 1 | no launch claim · show dissent |
| Arabic grep hit | convert before continue |

Record lessons: `skill-lessons.yaml` + MEM `type: lesson`.

---

## CHAMPION mode (optional competition)

When user requests superiority proof:
- beat generic answer with command output
- honest on failure
- test count from STATUS — not memory

**Related:** [sdlc-pipeline.md](sdlc-pipeline.md) · [shura-governance.md](shura-governance.md)
