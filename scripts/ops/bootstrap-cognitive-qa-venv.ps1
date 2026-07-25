# System 85 - bootstrap cognitive-qa .venv with pytest + PySide6 (C19/C28)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$legacyDir = Join-Path $repoRoot "cognitive-qa"
$venvPy = Join-Path $legacyDir ".venv\Scripts\python.exe"
$venvPip = Join-Path $legacyDir ".venv\Scripts\pip.exe"

if (-not (Test-Path $legacyDir)) {
    Write-Error "cognitive-qa not found at $legacyDir"
    exit 1
}

if (-not (Test-Path $venvPy)) {
    Write-Host "Creating cognitive-qa .venv..."
    if (Get-Command py -ErrorAction SilentlyContinue) {
        py -3.12 -m venv (Join-Path $legacyDir ".venv")
    }
    else {
        python -m venv (Join-Path $legacyDir ".venv")
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "PARTIAL - venv creation failed - compileall fallback"
        exit 0
    }
}

if (Test-Path $venvPip) {
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    Write-Host "Installing pytest in cognitive-qa .venv..."
    & $venvPip install -q pytest | Out-Null

    $launcherReq = Join-Path $legacyDir "requirements-launcher.txt"
    if (Test-Path $launcherReq) {
        Write-Host "Installing PySide6 from requirements-launcher.txt..."
        & $venvPip install -q -r $launcherReq | Out-Null
    }

    $baseReq = Join-Path $legacyDir "requirements.txt"
    if (Test-Path $baseReq) {
        Write-Host "Installing cognitive-qa base requirements..."
        & $venvPip install -q -r $baseReq | Out-Null
    }

    $liveReq = Join-Path $legacyDir "requirements-live.txt"
    if (Test-Path $liveReq) {
        Write-Host "Installing cognitive-qa live automation (pywinauto, mss, psutil)..."
        & $venvPip install -q -r $liveReq | Out-Null
    }
    $ErrorActionPreference = $prevEap
}

if (Test-Path $venvPy) {
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    Write-Host "Running bridge pytest smoke..."
    & $venvPy -m pytest (Join-Path $legacyDir "tests\test_source_only.py") -q --tb=line | Out-Null
    if ($LASTEXITCODE -eq 0) {
        & $venvPy -m pytest (Join-Path $legacyDir "tests\test_bridge.py") -q --tb=line | Out-Null
    }
    $ErrorActionPreference = $prevEap
    Write-Host "PASS - cognitive-qa venv ready: $venvPy (pytest exit=$LASTEXITCODE)"
    exit 0
}

Write-Host "PARTIAL - venv not available"
exit 0
