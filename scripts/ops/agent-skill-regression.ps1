# Agent skill v11 structure regression checks
$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$SkillsBase = Join-Path $RepoRoot ".cursor\skills"
$SkillRoot = Get-ChildItem -Path $SkillsBase -Directory |
    Where-Object { $_.Name -eq "brqosh-skills-hub" } |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $SkillRoot) {
    $SkillRoot = Get-ChildItem -Path $SkillsBase -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "STATUS.yaml") } |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $SkillRoot) {
    Write-Error "Skill folder with STATUS.yaml not found"
}

$scenariosPath = Join-Path $PSScriptRoot "agent-skill-scenarios.json"
@(
    @{
        id = "S1-accounting-external"
        prompt = "Close O-01 ZATCA OTP production now"
        expect = @("external", "OTP", "dist/evidence", "not closed from code only")
        councils = "C06 + C12"
    }
    @{
        id = "S2-bug-minimal-diff"
        prompt = "Code broken - dotnet test failed on GoldenPosting"
        expect = @("rapid-diagnosis", "grep", "hypothesis", "verify", "minimal diff")
        councils = "C01 + C11"
    }
    @{
        id = "S3-ui-rtl"
        prompt = "Design dense RTL invoice screen for BRQOSH"
        expect = @("design", "brqosh-ui-design-system", "RTL", "not marketing landing")
        councils = "C03 + C06"
    }
    @{
        id = "S4-shura-auto"
        prompt = "Complete live UI audit and give final launch decision"
        expect = @("SAVE CEEF", "agent runs run-shura-council", "not user manual", "council evidence", "exit 0")
        councils = "C08 + C12 + Shura S01-S12"
    }
) | ConvertTo-Json -Depth 4 | Set-Content $scenariosPath -Encoding UTF8

Write-Host "Scenarios written: $scenariosPath"
Write-Host ""
Write-Host "Manual checklist (agent session):"
Write-Host "  S1 - O-01: refuse false closure, document external OTP"
Write-Host "  S2 - bug: evidence-first, minimal diff, dotnet test verify"
Write-Host "  S3 - UI: RTL ERP density, design system"
Write-Host "  S4 - Shura: agent auto-runs council tool after SAVE - user never"
Write-Host ""

$statusPath = Join-Path $SkillRoot "STATUS.yaml"
if (-not (Test-Path $statusPath)) { Write-Error "Missing STATUS.yaml" }
$status = Get-Content $statusPath -Raw
if ($status -notmatch "version: v1[3-6][a-z]?") { Write-Error "STATUS.yaml version not v13..v16" }
if ($status -notmatch "tests:\s*\r?\n\s*total:") { Write-Error "STATUS.yaml missing tests.total" }
Write-Host "STATUS.yaml OK"

$skillPackages = @(
    "brqosh-skills-hub\SKILL.md",
    "claude-fable-5\SKILL.md",
    "fable-omni-core\SKILL.md",
    "human-intelligence-mastery\SKILL.md",
    "supreme-programming-mastery\SKILL.md",
    "brqosh-agent-protocol\SKILL.md",
    "brqosh-engineering\SKILL.md",
    "brqosh-debug\SKILL.md",
    "brqosh-closure\SKILL.md",
    "brqosh-accounting\SKILL.md",
    "brqosh-ui\SKILL.md",
    "brqosh-ui-design-system\SKILL.md",
    "brqosh-review\SKILL.md",
    "brqosh-analysis\SKILL.md",
    "brqosh-communication\SKILL.md",
    "brqosh-agents\SKILL.md",
    "brqosh-creative\SKILL.md",
    "universal-coding\SKILL.md"
)
Write-Host ""
Write-Host "Merged skill roadmaps (SKILL.md#engineering-roadmap):"
foreach ($sp in $skillPackages) {
    $p = Join-Path $SkillsBase $sp
    if (-not (Test-Path $p)) { Write-Error "Missing $sp" }
    $body = Get-Content $p -Raw
    if ($body -notmatch "## Engineering Roadmap") { Write-Error "$sp missing ## Engineering Roadmap section" }
    Write-Host "  OK $sp"
}

$arabicHubSkill = Get-ChildItem -Path $SkillsBase -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
    Where-Object {
        $sk = Get-Content (Join-Path $_.FullName "SKILL.md") -Raw -ErrorAction SilentlyContinue
        $sk -match "(?m)^name:\s*supreme-intelligence-hub\s*$"
    } |
    Select-Object -First 1
if ($arabicHubSkill) {
    $ahp = Join-Path $arabicHubSkill.FullName "SKILL.md"
    $ah = Get-Content $ahp -Raw
    if ($ah -notmatch "## Engineering Roadmap") { Write-Error "Arabic hub SKILL missing engineering roadmap" }
    $ahCount = (Get-ChildItem -Path $arabicHubSkill.FullName -Recurse -File).Count
    if ($ahCount -ne 1) { Write-Error "Redirect hub should contain only SKILL.md (found $ahCount files)" }
    Write-Host "  OK supreme-intelligence-hub/SKILL.md (redirect only)"
} else {
    Write-Error "Missing supreme-intelligence-hub SKILL.md"
}

# v14: ROADMAP.md stubs must NOT exist (single SKILL.md contract)
Write-Host ""
Write-Host "No duplicate ROADMAP stubs (v14):"
$roadmapStubs = Get-ChildItem -Path $SkillsBase -Recurse -Filter "ROADMAP.md" -ErrorAction SilentlyContinue
if ($roadmapStubs) {
    foreach ($rs in $roadmapStubs) { Write-Error "Stale ROADMAP stub: $($rs.FullName)" }
}
$masterStub = Join-Path $SkillsBase "brqosh-skills-hub\MASTER-ROADMAP.md"
if (Test-Path $masterStub) { Write-Error "Stale MASTER-ROADMAP.md stub" }
Write-Host "  OK no ROADMAP.md stubs"

$constitution = Join-Path $SkillsBase "brqosh-agent-protocol\constitution.md"
if (-not (Test-Path $constitution)) { Write-Error "Missing brqosh-agent-protocol/constitution.md" }
Write-Host "  OK constitution.md (MASTER merged)"

$rawFable = Join-Path $SkillsBase "claude-fable-5.md"
if (-not (Test-Path $rawFable)) { Write-Error "Missing canonical claude-fable-5.md" }
$rawSize = (Get-Item $rawFable).Length
if ($rawSize -lt 100000) { Write-Error "claude-fable-5.md too small ($rawSize) - stub not canonical" }
Write-Host ("  OK claude-fable-5.md canonical (" + $rawSize + " bytes)")

$fableSkill = Join-Path $SkillsBase "claude-fable-5\SKILL.md"
if (-not (Test-Path $fableSkill)) { Write-Error "Missing claude-fable-5/SKILL.md" }
Write-Host "  OK claude-fable-5/SKILL.md"

Write-Host "FILE-ROADMAP.yaml (v12):"
$fileRoadmaps = @(
    "fable-omni-core\FILE-ROADMAP.yaml",
    "human-intelligence-mastery\FILE-ROADMAP.yaml",
    "supreme-programming-mastery\FILE-ROADMAP.yaml",
    "brqosh-skills-hub\fable-tag-registry.yaml",
    "brqosh-skills-hub\leak-corpus-index.yaml",
    "brqosh-skills-hub\ROADMAP-SPEC.md"
)
foreach ($fr in $fileRoadmaps) {
    $p = Join-Path $SkillsBase $fr
    if (-not (Test-Path $p)) { Write-Error "Missing $fr" }
    Write-Host "  OK $fr"
}

$core = @(
    @{ Path = "claude-fable-5\SKILL.md"; Label = "claude-fable-5" },
    @{ Path = "fable-omni-core\SKILL.md"; Label = "fable-omni" },
    @{ Path = "fable-omni-core\operating-manual.md"; Label = "operating-manual" },
    @{ Path = "human-intelligence-mastery\universal-understanding.md"; Label = "universal-understanding" },
    @{ Path = "human-intelligence-mastery\research-intelligence.md"; Label = "research-intelligence" },
    @{ Path = "human-intelligence-mastery\copyright-compliance.md"; Label = "copyright-compliance" },
    @{ Path = "human-intelligence-mastery\evenhandedness.md"; Label = "evenhandedness" },
    @{ Path = "supreme-programming-mastery\skills-first.md"; Label = "skills-first" },
    @{ Path = "brqosh-skills-hub\SKILL.md"; Label = "hub" },
    @{ Path = "brqosh-agent-protocol\SKILL.md"; Label = "protocol" },
    @{ Path = "brqosh-agent-protocol\constitution.md"; Label = "constitution" },
    @{ Path = "brqosh-engineering\SKILL.md"; Label = "engineering" },
    @{ Path = "brqosh-debug\SKILL.md"; Label = "debug" },
    @{ Path = "supreme-programming-mastery\SKILL.md"; Label = "supreme-programming" },
    @{ Path = "human-intelligence-mastery\SKILL.md"; Label = "human-intelligence" }
)
Write-Host ""
Write-Host "Core skill packages:"
foreach ($item in $core) {
    $p = Join-Path $SkillsBase $item.Path
    if (-not (Test-Path $p)) { Write-Error "Missing $($item.Label) at $($item.Path)" }
    Write-Host "  OK $($item.Label)"
}

$mergedModules = @(
    "brqosh-agent-protocol\constitution.md",
    "brqosh-agents\session-protocol.md",
    "brqosh-agents\council-intelligence-matrix.md",
    "brqosh-closure\zero-gap-matrix.md",
    "brqosh-communication\human-parity.md",
    "fable-omni-core\ai-supremacy.md",
    "brqosh-skills-hub\elite-models.md",
    "brqosh-skills-hub\leak-synthesis.md",
    "brqosh-skills-hub\leak-audit-history.md",
    "brqosh-skills-hub\leak-corpus-index.yaml"
)
Write-Host ""
Write-Host "Archive-merged modules (v14):"
foreach ($mm in $mergedModules) {
    $p = Join-Path $SkillsBase $mm
    if (-not (Test-Path $p)) { Write-Error "Missing merged module $mm" }
    Write-Host "  OK $mm"
}

# v14 final: archive/ removed
Write-Host ""
Write-Host "Archive removed (v14 final):"
$archiveDir = Get-ChildItem -Path $SkillsBase -Directory -Recurse |
    Where-Object { $_.Name -eq "archive" } |
    Select-Object -First 1
if ($archiveDir) { Write-Error "archive/ should be deleted: $($archiveDir.FullName)" }
Write-Host "  OK no archive/ folder"

$uiDs = Join-Path $SkillsBase "brqosh-ui-design-system\SKILL.md"
if (-not (Test-Path $uiDs)) { Write-Error "Missing brqosh-ui-design-system/SKILL.md" }
Write-Host "  OK brqosh-ui-design-system/SKILL.md"

$shuraAuto = Join-Path $RepoRoot ".cursor\rules\brqosh-shura-auto.mdc"
if (-not (Test-Path $shuraAuto)) { Write-Error "Missing brqosh-shura-auto.mdc" }
$shuraBody = Get-Content $shuraAuto -Raw
if ($shuraBody -notmatch "run-shura-council\.ps1") { Write-Error "brqosh-shura-auto.mdc missing run-shura-council.ps1" }
if ($shuraBody -notmatch "alwaysApply:\s*true") { Write-Error "brqosh-shura-auto.mdc must be alwaysApply" }
Write-Host "  OK brqosh-shura-auto.mdc (always-on)"

# ‏(م-٥ · ن-٢٠) كان هنا `tools\ShuraCouncil\Program.cs` — **وهي النسخةُ المحذوفة**.
# ‏و`$ErrorActionPreference = "Stop"` أعلى الملفّ يجعل `Write-Error` يُنهي النصَّ فعلاً،
# ‏فتركُ المسار القديم كان يُسقط ما بعده لا يُنبّه فحسب.
$shuraTool = Join-Path $RepoRoot "ShuraCouncil\Program.cs"
if (-not (Test-Path $shuraTool)) { Write-Error "Missing ShuraCouncil" }
Write-Host "  OK ShuraCouncil"

$councilMatrix = Join-Path $SkillsBase "brqosh-agents\council-intelligence-matrix.md"
if (-not (Test-Path $councilMatrix)) { Write-Error "Missing council-intelligence-matrix.md" }
Write-Host "  OK council-intelligence-matrix.md"

$runShura = Join-Path $RepoRoot "scripts\ops\run-shura-council.ps1"
if (-not (Test-Path $runShura)) { Write-Error "Missing run-shura-council.ps1" }
Write-Host "  OK run-shura-council.ps1"

Write-Host ""
Write-Host "PASS - structure regression checks complete."
