# Cloud Code Desktop — Omni-QA + WPF Shell Bridge

> **Product:** [CognitiveQa](../../../CognitiveQa/) · **Shell law:** [CLOUD-CODE-SHELL.md](../../../CognitiveQa/design-system/CLOUD-CODE-SHELL.md)  
> **Deep ref:** [cognitiveqa-module.md](../ai-dev-platform/cognitiveqa-module.md)  
> **Language:** English only · binding MUST/MUST NOT

---

## Purpose

This module connects **Omni-QA Army skills** (audit theory, roles, vision) to the **Cloud Code desktop shell** Cloud Code agents edit daily.

```
Omni-QA skills (what to audit) + CognitiveQa.Desktop (where user runs it) + Cli/Orchestrator (how it executes)
```

---

## Desktop shell — agent edit map

| User-facing region | XAML / code location | MUST preserve |
|--------------------|----------------------|---------------|
| Activity bar | `MainWindow.xaml` left column | RUN · CONFIG · COUNCIL · REPORTS · WORKFLOW |
| Explorer sidebar | Sidebar panel | Target path browse · tree |
| Editor tabs | Central tab control | Tab names match activity icons |
| Command strip | Top of editor | Ctrl+K palette hook |
| Bottom panel | Grid row bottom | TERMINAL · SHURA · OUTPUT |
| Status bar | Bottom row | Target · mode · RTL state |
| Theme | `CloudCodeTheme.xaml` | VS Code dark tokens |

**Navigation:** `ShellNavigationService.cs` — activity ↔ tab sync  
**i18n:** `LocaleService.cs` — RTL toggle; default LTR English

---

## Startup guards (MEM-157)

| MUST | Detail |
|------|--------|
| G01 | Set `_uiReady = true` only after `InitializeComponent` completes |
| G02 | Event handlers (`ArabicReport_Changed`, `Options_Changed`) MUST no-op until `_uiReady` |
| G03 | `LocaleService.Apply` MUST null-check tab elements |

**FORBIDDEN:** `IsChecked="True"` on controls that fire handlers during init without guard.

---

## Launcher law

| Entry | Path |
|-------|------|
| Repo root | `START-CognitiveQa.bat` |
| Product folder | `CognitiveQa/Start-CognitiveQa.ps1` |
| Published | `CognitiveQa/publish/desktop/CognitiveQa.Desktop.exe` |

**PowerShell:** ASCII-only `Write-Host` strings — em-dash + Arabic in double quotes breaks parser (MEM-158).

---

## Audit execution paths

| Mode | How | When |
|------|-----|------|
| Desktop UI | User clicks Run in RUN tab | Interactive |
| CLI headless | `CognitiveQa.Cli` flags | CI · scripts |
| Live FlaUI | `--live` + target EXE | Windows UI probe |
| Supreme gate | `supreme-gate -s .` | Holistic closure |

**Output root:** `CognitiveQa/audit_reports/` (or path from session context)

---

## FlaUI test contract

Tests in `CognitiveQa.Tests` use `AutomationProperties.Name` markers:

| Marker | Region |
|--------|--------|
| `CognitiveQa Cloud Code Shell` | Window root |
| `RUN` | Activity / run controls |
| `Run Audit` | Primary action |

**MUST NOT** rename AutomationProperties without updating `LiveFlaUiSmokeRunner` and validators.

---

## Omni-QA role → auditor mapping

| P1 role skill | Code home |
|---------------|-----------|
| `roles/role-*.md` | `CognitiveQa.Auditors` implementations |
| vision-perception | Automation + VisionBrain |
| deep-audit | Auditors + Reports |
| chronicle-protocol | Chronicle + audit_reports |
| launcher-architecture | Desktop + Start scripts |

Grep `class.*Auditor` in `CognitiveQa.Auditors/` for live role list (18 roles).

---

## Shura + evidence

| Item | Path |
|------|------|
| UI CEEF | `dist/evidence/prompt-governance/CEEF-UI-CLOUD-CODE-002.md` |
| Council | `dist/evidence/shura-council/council-*.md` |
| UI gate | `scripts/ops/write-system123-cloud-code-ui-gate.ps1` |

Agent MUST run UI gate after shell XAML/theme changes.

---

## Legacy boundary

| Path | Status |
|------|--------|
| `cognitive-qa/` Python | **FROZEN** — do not extend |
| `CognitiveQa/` .NET | **PRIMARY** — all new work |

---

## Verify

```powershell
cd CognitiveQa
dotnet test CognitiveQa.sln -c Release --nologo
cd ..
.\scripts\ops\write-system123-cloud-code-ui-gate.ps1
```

**Related:** [SKILL.md](SKILL.md) · [vision-perception.md](vision-perception.md) · [launcher-architecture.md](launcher-architecture.md)
