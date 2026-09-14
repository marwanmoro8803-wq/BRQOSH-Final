# BRQOSH Desktop Omni-Audit runner
param(
    [switch]$StaticOnly,
    [int]$MaxPages = 0,
    [switch]$NoPayloads,
    [switch]$NoSeed,
    [string]$Username = "marwan"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$root\Brqosh.sln")) { $root = Split-Path -Parent $PSScriptRoot }

$auditDir = Join-Path $root "tools\desktop-omni-audit"
$venvPython = Join-Path $auditDir ".venv\Scripts\python.exe"
$python = if (Test-Path $venvPython) { $venvPython } else { "python" }

$env:BRQOSH_TEST_USERNAME = $Username
# ‏(م-٥ · ن-٢٢) ‏**لا كلمةَ احتياطيّة** — كانت حرفيّةً منشورةً في المستودع.
if (-not $env:BRQOSH_TEST_PASSWORD) {
    Write-Error "‏BRQOSH_TEST_PASSWORD غيرُ مضبوط — اضبطه لهذه التشغيلة."
    exit 2
}

Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

Push-Location $auditDir
try {
    if (-not (Test-Path ".venv")) {
        Write-Host "[setup] Creating venv..." -ForegroundColor Yellow
        python -m venv .venv
        & $venvPython -m pip install -r requirements.txt
    }

    $args = @("audit_orchestrator.py")
    if ($StaticOnly) { $args += "--static-only" }
    if ($MaxPages -gt 0) { $args += @("--max-pages", $MaxPages) }
    if ($NoPayloads) { $args += "--no-payloads" }
    if ($NoSeed) { $args += "--no-seed" }

    Write-Host "=== BRQOSH Desktop Omni-Audit ===" -ForegroundColor Cyan
    & $python @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
