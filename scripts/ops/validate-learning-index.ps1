# Validate LEARNING-INDEX.yaml required routes
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$path = Join-Path $repoRoot ".cursor\LEARNING-INDEX.yaml"

if (-not (Test-Path $path)) {
    Write-Error "LEARNING-INDEX.yaml missing"
    exit 1
}

$content = Get-Content $path -Raw
$required = @(
    "R-FIX",
    "R-FABLE-TRI",
    "R-CLOUD-CODE-KNOWLEDGE",
    "R-SHURA",
    "R-AIDEV",
    "R-OMNIQA"
)

$failed = 0
foreach ($id in $required) {
    if ($content -match $id) {
        Write-Host "[PASS] $id" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] $id missing" -ForegroundColor Red
        $failed++
    }
}

if ($failed -gt 0) { exit 1 }
Write-Host "LEARNING-INDEX validation PASS"
exit 0
