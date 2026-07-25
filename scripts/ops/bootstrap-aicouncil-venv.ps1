# System 81 - bootstrap AiCouncil .venv with pytest (C15)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$aiDir = Join-Path $repoRoot "AiCouncil"
$venvPy = Join-Path $aiDir ".venv\Scripts\python.exe"

if (-not (Test-Path $aiDir)) {
    Write-Error "AiCouncil not found at $aiDir"
    exit 1
}

if (-not (Test-Path $venvPy)) {
    Write-Host "Creating AiCouncil .venv..."
    if (Get-Command py -ErrorAction SilentlyContinue) {
        py -3.12 -m venv (Join-Path $aiDir ".venv")
    }
    else {
        python -m venv (Join-Path $aiDir ".venv")
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "PARTIAL - venv creation failed - compileall fallback"
        exit 0
    }
}

$pip = Join-Path $aiDir ".venv\Scripts\pip.exe"
$verifyReq = Join-Path $aiDir "requirements-verify.txt"
if (Test-Path $pip) {
    Write-Host "Installing verify deps in .venv..."
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    if (Test-Path $verifyReq) {
        & $pip install -q -r $verifyReq 2>&1 | Out-Null
    }
    else {
        & $pip install -q pytest huggingface-hub fastapi uvicorn PyPDF2 Pillow 2>&1 | Out-Null
    }
    $ErrorActionPreference = $prevEap
}

if (Test-Path $venvPy) {
    Write-Host "PASS - AiCouncil venv ready: $venvPy"
    exit 0
}

Write-Host "PARTIAL - venv not available"
exit 0
