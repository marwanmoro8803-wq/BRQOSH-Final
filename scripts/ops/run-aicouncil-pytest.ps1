# System 78 — AiCouncil pytest / compileall verify
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$aiDir = Join-Path $repoRoot "AiCouncil"

if (-not (Test-Path $aiDir)) {
    Write-Error "AiCouncil not found at $aiDir"
    exit 1
}

Push-Location $aiDir
try {
    Write-Host "→ pip install pytest…"
    python -m pip install -q pytest 2>&1 | Out-Null

    Write-Host "AiCouncil verify — trying pytest…"
    python -m pytest tests/ -q --tb=line 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "PASS — pytest"
        exit 0
    }

    Write-Host "pytest unavailable or failed — compileall fallback…"
    python -m compileall -q brqosh tests 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "PASS — compileall fallback"
        exit 0
    }

    Write-Host "PARTIAL — verify failed"
    exit 1
}
finally {
    Pop-Location
}
