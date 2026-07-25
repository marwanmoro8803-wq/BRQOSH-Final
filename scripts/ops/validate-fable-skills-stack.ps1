# Master gate — Fable tri-source + skills stack + learning index
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ops = Join-Path $repoRoot "scripts\ops"
$failed = 0

$scripts = @(
    "validate-tri-source.ps1",
    "validate-fable-conversion.ps1",
    "validate-merged-claude-index.ps1",
    "validate-learning-index.ps1"
)

foreach ($name in $scripts) {
    $path = Join-Path $ops $name
    Write-Host "=== $name ===" -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File $path
    if ($LASTEXITCODE -ne 0) { $failed++ }
}

$index = Join-Path $repoRoot ".cursor\skills\brqosh-skills-hub\SKILLS-CLOUD-CODE-INDEX.yaml"
if (Test-Path $index) {
    Write-Host "[PASS] SKILLS-CLOUD-CODE-INDEX.yaml" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] SKILLS-CLOUD-CODE-INDEX.yaml" -ForegroundColor Red
    $failed++
}

$shura = Join-Path $repoRoot "dist\evidence\prompt-governance\SHURA-DECISION-FABLE-TRI-SOURCE-003.md"
if (Test-Path $shura) {
    Write-Host "[PASS] SHURA-DECISION-FABLE-TRI-SOURCE-003.md" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] Shura decision doc missing" -ForegroundColor Red
    $failed++
}

$learning = Join-Path $repoRoot ".cursor\LEARNING-INDEX.yaml"
if (Test-Path $learning) {
    $content = Get-Content $learning -Raw
    if ($content -match 'R-FABLE-TRI' -and $content -match 'R-CLOUD-CODE-KNOWLEDGE') {
        Write-Host "[PASS] LEARNING-INDEX R-FABLE-TRI + R-CLOUD-CODE-KNOWLEDGE" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] LEARNING-INDEX missing Fable routes" -ForegroundColor Red
        $failed++
    }
}

$skillCount = (Get-ChildItem (Join-Path $repoRoot ".cursor\skills") -Recurse -Filter "SKILL.md" | Measure-Object).Count
if ($skillCount -ge 26) {
    Write-Host "[PASS] SKILL.md routers ($skillCount >= 26)" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] Expected 26+ SKILL.md, found $skillCount" -ForegroundColor Red
    $failed++
}

if ($failed -gt 0) {
    Write-Error "Fable skills stack validation failed: $failed subsystem(s)"
    exit 1
}

Write-Host "Fable skills stack validation PASS (all gates)"
exit 0
