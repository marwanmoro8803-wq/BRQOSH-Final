# System 92 - Shura Dynamo evidence (F00 dispatch + member pulses)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 92 Shura Dynamo evidence ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "shura-dynamo\install-pulse-scripts.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $PSScriptRoot "run-shura-dynamo.ps1") -DryRun -Mode Auto
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $PSScriptRoot "run-shura-dynamo.ps1") -Seat D01 -DryRun
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dispatch = . (Join-Path $PSScriptRoot "shura-dynamo\Resolve-ShuraDispatch.ps1") -Mode Auto
$queueCount = 0
if ($dispatch.Queue) { $queueCount = @($dispatch.Queue).Count }
if ($queueCount -eq 0) {
    Write-Warning "Dispatch queue empty - council may be missing"
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\shura-dynamo"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"
$pulseDir = Join-Path $PSScriptRoot "shura-dynamo\pulses"
$pulseCount = (Get-ChildItem $pulseDir -Filter "pulse-*.ps1" -ErrorAction SilentlyContinue).Count

$payload = [ordered]@{
    system = 92
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    councilPath = $dispatch.CouncilPath
    queueCount = $queueCount
    pulseScripts = $pulseCount
    dryRunPass = $true
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 92 evidence PASS - latest=$latest"
exit 0
