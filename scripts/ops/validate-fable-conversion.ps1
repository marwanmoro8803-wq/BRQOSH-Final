# Validate claude-fable-5.md → skills conversion map
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$mapPath = Join-Path $repoRoot ".cursor\skills\claude-fable-5\CONVERSION-MAP.yaml"
$skillsRoot = Join-Path $repoRoot ".cursor\skills"

if (-not (Test-Path $mapPath)) {
    Write-Error "Missing CONVERSION-MAP.yaml"
    exit 1
}

# Minimal YAML parse without external deps
$lines = Get-Content $mapPath -Raw
$tagSection = $false
$modulePaths = [System.Collections.Generic.List[string]]::new()
$moduleSkills = @(
    "claude-fable-5\modules\01-behavior\SKILL.md",
    "claude-fable-5\modules\02-behavior\SKILL.md",
    "claude-fable-5\modules\02-memory\SKILL.md",
    "claude-fable-5\modules\03-search\SKILL.md",
    "claude-fable-5\modules\04-programming\SKILL.md",
    "claude-fable-5\modules\05-evaluation\SKILL.md"
) | Where-Object { Test-Path (Join-Path $skillsRoot ($_ -replace '02-behavior','01-behavior')) }

$requiredModules = @(
    "human-intelligence-mastery\safety-boundaries.md",
    "human-intelligence-mastery\communication.md",
    "human-intelligence-mastery\wellbeing-mastery.md",
    "human-intelligence-mastery\evenhandedness.md",
    "human-intelligence-mastery\research-intelligence.md",
    "human-intelligence-mastery\copyright-compliance.md",
    "human-intelligence-mastery\universal-understanding.md",
    "fable-omni-core\context-continuity.md",
    "fable-omni-core\preferences-handling.md",
    "fable-omni-core\anti-injection.md",
    "fable-omni-core\operating-manual.md",
    "supreme-programming-mastery\skills-first.md",
    "supreme-programming-mastery\file-vs-inline.md",
    "supreme-programming-mastery\tools-cursor.md"
)

$failed = 0
foreach ($rel in $requiredModules) {
    $full = Join-Path $skillsRoot $rel
    if (Test-Path $full) {
        Write-Host "[PASS] synthesis $rel" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] synthesis $rel" -ForegroundColor Red
        $failed++
    }
}

$routers = @(
    "claude-fable-5\SKILL.md",
    "claude-fable-5\CONVERSION-MAP.yaml",
    "claude-fable-5\modules\01-behavior\SKILL.md",
    "claude-fable-5\modules\02-memory\SKILL.md",
    "claude-fable-5\modules\03-search\SKILL.md",
    "claude-fable-5\modules\04-programming\SKILL.md",
    "claude-fable-5\modules\05-evaluation\SKILL.md"
)
foreach ($rel in $routers) {
    $full = Join-Path $skillsRoot $rel
    if (Test-Path $full) {
        Write-Host "[PASS] router $rel" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] router $rel" -ForegroundColor Red
        $failed++
    }
}

$raw = Join-Path $skillsRoot "raw\claude-fable-5.md"
if (Test-Path $raw) {
    $lineCount = (Get-Content $raw | Measure-Object -Line).Lines
    Write-Host "[PASS] raw source raw/claude-fable-5.md ($lineCount lines)" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] raw source raw/claude-fable-5.md missing" -ForegroundColor Red
    $failed++
}

$triIndex = Join-Path $skillsRoot "claude-fable-5\tri-source-index.yaml"
if (Test-Path $triIndex) {
    Write-Host "[PASS] tri-source-index.yaml" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] tri-source-index.yaml missing" -ForegroundColor Red
    $failed++
}

$cliModule = Join-Path $skillsRoot "supreme-programming-mastery\claude-code-cli.md"
if (Test-Path $cliModule) {
    Write-Host "[PASS] synthesis supreme-programming-mastery\claude-code-cli.md" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] claude-code-cli.md missing" -ForegroundColor Red
    $failed++
}

if ($failed -gt 0) {
    Write-Error "Fable conversion validation failed: $failed"
    exit 1
}

Write-Host "Fable conversion validation PASS"
exit 0
