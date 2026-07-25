# Validate Fable 5 tri-source raw files and indexes
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$skillsRoot = Join-Path $repoRoot ".cursor\skills"
$rawDir = Join-Path $skillsRoot "raw"

$requiredRaw = @(
    "claude-fable-5.md",
    "claude-code-2.1.172-fable-5.md",
    "merged_claude.md"
)

$requiredIndex = @(
    "claude-fable-5\SKILL.md",
    "claude-fable-5\tri-source-index.yaml",
    "claude-fable-5\CONVERSION-MAP.yaml",
    "claude-fable-5\source-index.md",
    "claude-fable-5\claude-code-fable-index.md",
    "claude-fable-5\cursor-adaptations.md"
)

$failed = 0

foreach ($name in $requiredRaw) {
    $full = Join-Path $rawDir $name
    if (Test-Path $full) {
        $lines = (Get-Content $full | Measure-Object -Line).Lines
        Write-Host "[PASS] raw $name ($lines lines)" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] raw $name missing" -ForegroundColor Red
        $failed++
    }
}

foreach ($rel in $requiredIndex) {
    $full = Join-Path $skillsRoot $rel
    if (Test-Path $full) {
        Write-Host "[PASS] index $rel" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] index $rel" -ForegroundColor Red
        $failed++
    }
}

$skillMd = Join-Path $skillsRoot "claude-fable-5\SKILL.md"
if (Test-Path $skillMd) {
    $skillLines = (Get-Content $skillMd | Measure-Object -Line).Lines
    if ($skillLines -le 500) {
        Write-Host "[PASS] SKILL.md line count ($skillLines <= 500)" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[WARN] SKILL.md exceeds 500 lines ($skillLines)" -ForegroundColor Yellow
    }
}

if ($failed -gt 0) {
    Write-Error "Tri-source validation failed: $failed"
    exit 1
}

Write-Host "Tri-source validation PASS"
exit 0
