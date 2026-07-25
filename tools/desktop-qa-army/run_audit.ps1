$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dir = Join-Path $root "tools\desktop-qa-army"
$venvPy = Join-Path $dir ".venv\Scripts\python.exe"
$cogVenv = Join-Path $root "tools\desktop-cognitive-audit\.venv\Scripts\python.exe"
$omniVenv = Join-Path $root "tools\desktop-omni-audit\.venv\Scripts\python.exe"
$python = if (Test-Path $venvPy) { $venvPy } elseif (Test-Path $cogVenv) { $cogVenv } elseif (Test-Path $omniVenv) { $omniVenv } else { "python" }

Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

Push-Location $dir
try {
  if (-not (Test-Path ".venv") -and -not (Test-Path $cogVenv) -and -not (Test-Path $omniVenv)) {
    python -m venv .venv
    & (Join-Path $dir ".venv\Scripts\python.exe") -m pip install -r requirements.txt
    $python = Join-Path $dir ".venv\Scripts\python.exe"
  }
  Write-Host "=== BRQOSH Desktop QA Army (10 roles) ===" -ForegroundColor Cyan
  & $python audit_orchestrator.py @args
  exit $LASTEXITCODE
} finally { Pop-Location }
