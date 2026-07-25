# Upgrade domain skill Engineering Roadmap sections to v12 contract tables
$ErrorActionPreference = "Stop"
$SkillsBase = (Resolve-Path (Join-Path $PSScriptRoot "..\..\.cursor\skills")).Path

$upgrades = @{
    "brqosh-accounting" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-FINANCE` · **Layer:** L2 domain

## Default path

``````
SKILL → reference.md → journal correctness → Golden 8/8 → UI (last)
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | journals · invoices · ZATCA · fiscal periods · posting |
| **SKIP** | Never on financial BRQOSH work |
| **NEXT** | reference.md |
| **EXIT** | Load order: correctness before UI |
| **ANTI** | UI-first · false ZATCA OTP closure |
| **VERIFY** | State non-negotiables before implementation |
| **TIME** | 20s |

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | posting details · Golden · FSA · audit trail |
| **SKIP** | Trivial question answered from SKILL |
| **EXIT** | Golden 8/8 · FSA 13/13 or documented gap |
| **ANTI** | Claim financial closure without Golden |
| **VERIFY** | dotnet test financial suite cited |
| **TIME** | 90s |

## Scenarios

| Signal | Path | VERIFY |
|--------|------|--------|
| New journal | reference → engineering | Golden pass |
| ZATCA production OTP | — | external dist/evidence |
"@

    "brqosh-debug" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-BUG` · **Layer:** L2 domain  
> **With:** supreme-programming + brqosh-engineering

## Default path

``````
SKILL → reference.md → repro → hypothesis → minimal fix → dotnet test
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | build fail · test fail · "code is broken" |
| **SKIP** | Never — triage before fix |
| **NEXT** | reference.md |
| **EXIT** | Rapid tree applied · one hypothesis stated |
| **ANTI** | Fix without repro · wide refactor |
| **VERIFY** | Root + evidence + verify command named |
| **TIME** | 30s |

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | Golden fail · schema · flaky · WPF NullRef |
| **SKIP** | Simple fix obvious from stack trace |
| **EXIT** | Root cause + verify command |
| **ANTI** | Schema change without SchemaSqlGuard |
| **VERIFY** | dotnet test PASS on targeted suite |
| **TIME** | 120s |

## Scenarios

| Symptom | Path | VERIFY |
|---------|------|--------|
| GoldenPosting fail | reference Golden section → accounting | Golden pass |
| NullRef WPF | reference UI → brqosh-ui | repro + fix |
| Schema PG | reference SchemaSqlGuard | guard + migration test |
"@

    "brqosh-closure" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-CLOSE` · **Layer:** L2 domain · **Status:** [STATUS.yaml](../brqosh-skills-hub/STATUS.yaml)

## Default path

``````
SKILL → reference.md → 4 levels → gates script → CEEF → OPEN_ITEMS
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | "close" · CEEF · wave · "done" · competition mode |
| **SKIP** | Never when claiming completion |
| **NEXT** | reference.md |
| **EXIT** | 4 levels named · no partial closure |
| **ANTI** | Checkmark without build/test/evidence |
| **VERIFY** | Each level has proof or explicit external |
| **TIME** | 25s |

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | CEEF write · O-01..O-05 · gates detail |
| **SKIP** | Quick status — SKILL suffices |
| **EXIT** | CEEF report + evidence paths |
| **ANTI** | OTP/ZAP/VM closed from code only |
| **VERIFY** | sync-agent-status · run-external-gates |
| **TIME** | 90s |

## 4 levels (mandatory order)

1. code + build  
2. dotnet test (STATUS.yaml count)  
3. live — dist/evidence/ only  
4. external — OPEN_ITEMS.md

## Scenarios

| Request | VERIFY |
|---------|--------|
| Close CEEF wave | CEEF file + test count |
| O-01 ZATCA OTP | external only |
"@

    "brqosh-agent-protocol" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-SESSION` · **Fable:** CLASSIFY from fable-omni-core

## Default path

``````
SKILL → protocol.md → routing.md (1-3 councils) → domain skill → VERIFY
``````

---

### ``SKILL.md`` · L0 · 1/3

| | |
|--|--|
| **TRIGGER** | Every BRQOSH session start |
| **SKIP** | Never in BRQOSH repo |
| **NEXT** | protocol.md |
| **EXIT** | Mode: execute \| analyze \| review \| teach |
| **ANTI** | Skip triage on multi-domain task |
| **TIME** | 20s |

---

### ``protocol.md`` · L1 · 2/3

| | |
|--|--|
| **TRIGGER** | Every message — permanent workflow |
| **NEXT** | routing.md or direct execution |
| **EXIT** | TRIAGE→DISCOVER→ACT→VERIFY pipeline |
| **ANTI** | Suggest without execute when asked |
| **TIME** | 45s |

---

### ``routing.md`` · L2 · 3/3

| | |
|--|--|
| **TRIGGER** | Multi-domain · council selection |
| **EXIT** | 1-3 councils (C01-C14) · skills chosen |
| **ANTI** | More than 3 councils |
| **TIME** | 30s |

## Scenarios

| Signal | councils → skills |
|--------|-------------------|
| test fail | C01+C11 → debug+engineering |
| UI RTL | C03+C06 → ui+engineering |
| ZATCA | C06+C12 → accounting+closure |
| CEEF close | C12 → closure |
"@

    "brqosh-engineering" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-CODE` · **Parent:** [supreme-programming/SKILL.md](../supreme-programming-mastery/SKILL.md#engineering-roadmap)

## Default path

``````
SKILL → engineering.md → [programming|polyglot|tools] → tests → STATUS.yaml
``````

---

### ``SKILL.md`` · L0 · 1/5

| | |
|--|--|
| **TRIGGER** | Any BRQOSH .NET / WPF / EF code |
| **NEXT** | engineering.md |
| **EXIT** | Explore loop + RBAC awareness |
| **TIME** | 20s |

---

### ``engineering.md`` · L1 · 2/5 — foundational

| | |
|--|--|
| **TRIGGER** | **First** read — DOMAIN_MAP · EF · WPF |
| **EXIT** | Entry points + BindSecurityContext understood |
| **ANTI** | Code before DOMAIN_MAP |
| **TIME** | 90s |

---

### ``programming.md`` · L2 · 3/5

| | |
|--|--|
| **TRIGGER** | BRQOSH 12-axis closure on change |
| **TIME** | 45s |

---

### ``polyglot.md`` · L2 · 3b/5

| | |
|--|--|
| **TRIGGER** | SQL / PS / scripts in diff |
| **TIME** | 30s |

---

### ``tools.md`` · L2 · 2b/5

| | |
|--|--|
| **TRIGGER** | DISCOVER phase · parallel grep · MCP |
| **TIME** | 30s |

## Scenarios

| Task | Path |
|------|------|
| New feature | engineering → programming → supreme/testing |
| PG migration | engineering → SchemaSqlGuard → accounting if financial |
"@

    "brqosh-ui" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-UI` · **With:** brqosh-ui-design-system

## Default path

``````
SKILL → reference.md → design-system → XAML → Desktop.Tests
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | Screen · form · DataGrid · RTL · WPF |
| **EXIT** | UI checklist: density · RTL · not marketing landing |
| **ANTI** | Marketing landing patterns in ERP |
| **TIME** | 25s |

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | WPF patterns · empty states · KPI cards |
| **EXIT** | XAML matches design system |
| **VERIFY** | Desktop.Tests or static UI check |
| **TIME** | 90s |
"@

    "brqosh-review" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-REVIEW`

## Default path

``````
SKILL → security.md → code-review.md → standards.md → simplify.md
``````

---

### ``security.md`` · L1 · 1/4 — first on money/auth paths

| | |
|--|--|
| **TRIGGER** | PR · auth · secrets · financial code |
| **EXIT** | Security findings before style |
| **TIME** | 60s |

---

### ``code-review.md`` · L2 · 2/4

| | |
|--|--|
| **TRIGGER** | Any code review |
| **EXIT** | Findings by severity |
| **TIME** | 45s |

## Scenarios

| Request | Path |
|---------|------|
| Financial PR | security → code-review → accounting angle |
"@

    "brqosh-analysis" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-ANALYSIS`

## Default path

``````
SKILL → analysis.md → requirements.md → [deep-cognition|super-dev] → handoff engineering
``````

---

### ``analysis.md`` · L1 · 1/4

| | |
|--|--|
| **TRIGGER** | Ambiguous spec · architecture · "how should we" |
| **EXIT** | Problem framed · options · recommendation |
| **ANTI** | Code before requirements |
| **TIME** | 60s |

---

### ``requirements.md`` · L2 · 2/4

| | |
|--|--|
| **TRIGGER** | New feature scope · user stories |
| **EXIT** | Testable requirements |
| **TIME** | 45s |
"@

    "brqosh-communication" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-COMM`

## Default path

``````
SKILL → communication.md → [human-cognition.md]
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | Explain · teach · bilingual · stakeholder |
| **EXIT** | Mode: tutor \| engineer \| accountant |
| **TIME** | 20s |

---

### ``communication.md`` · L1 · 2/2

| | |
|--|--|
| **TRIGGER** | All user-facing prose |
| **EXIT** | Clear · proportional · Arabic UI copy when product |
| **TIME** | 45s |
"@

    "brqosh-agents" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-AGENTS`

## Default path

``````
SKILL → reference.md → Task subagent → parent VERIFY
``````

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | Wide explore · parallel research |
| **ANTI** | Parallel mutate same files |
| **EXIT** | Parent synthesizes · verifies |
| **TIME** | 60s |
"@

    "brqosh-creative" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-BRQOSH-CREATIVE`

## Default path

``````
SKILL → reference.md → BRIEF → CONCEPT(≤3) → PROTOTYPE → CRITIQUE
``````

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | Games · media · creative deliverables |
| **EXIT** | Honest studio limits stated |
| **TIME** | 60s |
"@

    "universal-coding" = @"
## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** `P-UNIVERSAL-CODE` · **Not BRQOSH**

## Default path

``````
SKILL → reference.md → supreme/languages/* → project verify
``````

---

### ``SKILL.md`` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | Non-BRQOSH repo · unknown stack |
| **EXIT** | Stack manifest detected |
| **WARNING** | If BRQOSH paths → switch to brqosh-* |
| **TIME** | 20s |

---

### ``reference.md`` · L3 · 2/2

| | |
|--|--|
| **EXIT** | Build/test PASS · no CEEF |
| **VERIFY** | Project-native test command |
| **TIME** | 60s |
"@
}

foreach ($skillName in $upgrades.Keys) {
    $skillPath = Join-Path $SkillsBase "$skillName\SKILL.md"
    if (-not (Test-Path $skillPath)) { continue }
    $content = Get-Content $skillPath -Raw -Encoding UTF8
    if ($content -notmatch "## Engineering Roadmap") { continue }
    $body = $upgrades[$skillName] -replace '``````', '```'
    $body = $body -replace '``', '`'
    $newContent = $content -replace '(?s)\r?\n---\r?\n\r?\n## Engineering Roadmap.*\z', "`n---`n`n$body`n"
    if ($newContent -eq $content) { Write-Host "  no change: $skillName"; continue }
    [System.IO.File]::WriteAllText($skillPath, $newContent, [System.Text.UTF8Encoding]::new($false))
    Write-Host "  upgraded: $skillName"
}

Write-Host "Domain roadmap upgrade complete"
