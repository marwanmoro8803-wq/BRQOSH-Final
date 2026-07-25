# CognitiveQa Product Module — Deep Reference

> **Skill:** [SKILL.md](SKILL.md) · **Shell:** [CognitiveQa/design-system/CLOUD-CODE-SHELL.md](../../CognitiveQa/design-system/CLOUD-CODE-SHELL.md)  
> **STATUS:** [CognitiveQa/STATUS.yaml](../../CognitiveQa/STATUS.yaml)

---

## Product definition

CognitiveQa is a **.NET 9 cognitive desktop QA platform** that:

1. Hosts a **Cloud Code IDE shell** (WPF) for human operators
2. Orchestrates **18 role-based auditors** against a target application or source tree
3. Supports **headless CLI** and **live FlaUI** Windows automation
4. Emits **evidence-grade reports** under `audit_reports/`
5. Integrates **Shura governance** and **Omega Fabric** platform services

This is the **primary shippable product** of Program Developer workspace.

---

## Solution dependency graph

```
CognitiveQa.Cli ──────┐
CognitiveQa.Desktop ──┼──► CognitiveQa.Orchestrator
                      │         │
                      │         ├── CognitiveQa.Auditors (18 roles)
                      │         ├── CognitiveQa.Automation (FlaUI)
                      │         ├── CognitiveQa.SourceIntel (Roslyn/XAML)
                      │         ├── CognitiveQa.Reports
                      │         ├── CognitiveQa.Meta
                      │         └── CognitiveQa.Repair (perfection loop)
                      │
                      └── CognitiveQa.Core (shared models)
CognitiveQa.Tests ──── validates all layers
```

---

## Desktop shell — files you edit

| File | Responsibility |
|------|----------------|
| `MainWindow.xaml` | Cloud Code layout — activity bar, sidebar, tabs, panels |
| `MainWindow.xaml.cs` | Navigation, `_uiReady` guard, event handlers |
| `CloudCodeTheme.xaml` | VS Code dark palette |
| `App.xaml` | Theme merge · startup |
| `Services/ShellNavigationService.cs` | Tab ↔ activity bar sync |
| `Services/LocaleService.cs` | RTL toggle · bilingual labels |
| `Validators/DesktopShellValidator.cs` | Structural gate for tests |

**MUST preserve:** Activity bar icons RUN/CONFIG/COUNCIL/REPORTS/WORKFLOW · bottom TERMINAL/SHURA/OUTPUT · GridSplitters.

---

## Orchestrator phases

| Phase | Entry | Output |
|-------|-------|--------|
| LAUNCH | CLI `--target-source` or Desktop Run | `SessionContext` |
| EXPLORE | HumanNavigator | interaction chronicle |
| AUDIT | Parallel auditors | `Finding` list |
| REPORT | Report writers | `AUDIT_REPORT.md` (18 sections) |
| META | Weakness synthesis | `WEAKNESS_SYNTHESIS.md` |
| REPAIR | Perfection loop | `FIX_PLAN.md`, `VERIFY_LOOP.md` |

**Exit codes:** 0 Pass · 1 Critical · 2 High · 3 Orchestrator error

---

## Auditor army (18 roles)

Located in `CognitiveQa.Auditors`. Each role produces deliverables consumed by Reports + Meta.

| Category | Examples |
|----------|----------|
| Perception | Cognitive Perception Mind |
| Security | RBAC · injection patterns |
| UX | RTL · empty states · density |
| Resilience | Forensic · chaos patterns |
| Constitution | Roslyn rule violations |

Full role list: grep `class.*Auditor` in `CognitiveQa.Auditors/`.

---

## Live automation (FlaUI)

| Component | Role |
|-----------|------|
| `FlaUiDriver` | UIA3 attach to target process |
| `HumanNavigator` | Human-like explore sequence |
| `VisionBrain` | OCR fallback when UIA weak |
| `LiveFlaUiSmokeRunner` | Dogfood Desktop shell |

**Requirement:** Windows host · target EXE or published Desktop.

```powershell
dotnet run --project src/CognitiveQa.Cli -c Release -- --live --ocr-fallback -t YourApp.exe
```

---

## Platform services (Omega / Shura)

| Service | Location | Role |
|---------|----------|------|
| SmartWorkflowOrchestrator | Core platform | Workflow routing |
| PackageVulnerabilityGate | Security | NuGet audit |
| OmegaVeto | Governance | Block unsafe actions |
| SupremeVisionOrchestrator | Vision chain | Weakness reading |
| Shura integration | Desktop SHURA panel | Council output display |

See `CognitiveQa/STATUS.yaml` platform section for current wave IDs.

---

## Output artifacts

| File | Generator |
|------|-----------|
| `audit_reports/AUDIT_REPORT.md` | Reports |
| `audit_reports/WEAKNESS_SYNTHESIS.md` | Meta |
| `audit_reports/CHRONICLE_DIARY.md` | Automation |
| `audit_reports/COGNITIVE_COUNCIL.md` | Council synthesis |
| `audit_reports/FIX_PLAN.md` | Repair (perfection) |
| `audit_reports/VERIFY_LOOP.md` | Repair |
| `audit_reports/evidence/` | Screenshots · logs |

---

## Ops scripts (repo root)

| Script | Purpose |
|--------|---------|
| `write-system123-cloud-code-ui-gate.ps1` | UI structure gate |
| `run-cognitive-supreme-gate.ps1` | Supreme holistic gate |
| `run-cognitive-live-smoke.ps1` | FlaUI dogfood |
| `run-cognitive-audit.ps1` | Headless audit wrapper |

---

## MUST (editing CognitiveQa)

| ID | Rule |
|----|------|
| CQ-M01 | Run `dotnet test` after behavior change |
| CQ-M02 | Run UI gate after MainWindow/theme change |
| CQ-M03 | Guard XAML event handlers with `_uiReady` during InitializeComponent |
| CQ-M04 | ASCII-only strings in `Start-CognitiveQa.ps1` (PowerShell parser) |
| CQ-M05 | Update `CognitiveQa/STATUS.yaml` on phase change |

---

## MUST NOT

| ID | Forbidden |
|----|-----------|
| CQ-F01 | Revert to Mission Control horizontal-only nav without CEEF |
| CQ-F02 | Hardcode Arabic in agent law files (use LocaleService) |
| CQ-F03 | Break AutomationProperties names used by FlaUI tests |
| CQ-F04 | Port features back to `cognitive-qa/` Python tree |

---

## Verify

```powershell
cd CognitiveQa
dotnet build CognitiveQa.sln -c Release --nologo
dotnet test CognitiveQa.sln -c Release --nologo
cd ..
.\scripts\ops\write-system123-cloud-code-ui-gate.ps1
```

**Related:** [platform-vision.md](platform-vision.md) · [omni-qa-army/SKILL.md](../omni-qa-army/SKILL.md)
