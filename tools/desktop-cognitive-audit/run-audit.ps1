$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dir = Join-Path $root "tools\desktop-cognitive-audit"
$venvPy = Join-Path $dir ".venv\Scripts\python.exe"
$omniVenv = Join-Path $root "tools\desktop-omni-audit\.venv\Scripts\python.exe"
$python = if (Test-Path $venvPy) { $venvPy } elseif (Test-Path $omniVenv) { $omniVenv } else { "python" }

Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

Push-Location $dir
try {
  if (-not (Test-Path ".venv") -and -not (Test-Path $omniVenv)) {
    python -m venv .venv
    & $python -m pip install -r requirements.txt
  }
  if (-not (Test-Path ".venv") -and (Test-Path $omniVenv)) { $python = $omniVenv }
  Write-Host "=== BRQOSH Cognitive Audit ===" -ForegroundColor Cyan
  & $python audit_orchestrator.py @args
  exit $LASTEXITCODE
} finally { Pop-Location }
