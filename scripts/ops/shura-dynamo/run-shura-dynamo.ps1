# Shura Dynamo orchestrator — F00 decides which member pulses run (System 92)
param(
    [string]$CouncilJson = "",
    [ValidateSet("Auto", "FailedOnly", "Maintain", "Manual", "All")]
    [string]$Mode = "Auto",
    [string[]]$Seats = @(),
    [switch]$DryRun,
    [switch]$RunCouncilFirst,
    [string]$CouncilSource = "dist/evidence/prompt-governance/CEEF-AUDIT-LATEST.md",
    [int]$PulseDelayMs = 400
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_pulse-common.ps1")

Write-Host "=== Shura Dynamo (F00 dispatch) ===" -ForegroundColor Cyan

if ($RunCouncilFirst) {
    Write-Host "-> F00: running Shura council before dispatch..." -ForegroundColor Cyan
    $shura = Join-Path $OpsRoot "run-shura-council.ps1"
    & $shura -Source $CouncilSource
    if ($LASTEXITCODE -gt 1) {
        Write-Warning "Shura exit=$LASTEXITCODE - continuing dispatch with latest JSON"
    }
}

$dispatch = . (Join-Path $PSScriptRoot "Resolve-ShuraDispatch.ps1") `
    -CouncilJsonPath $CouncilJson `
    -Mode $Mode `
    -Seats $Seats

Write-Host "Council: $($dispatch.CouncilPath)" -ForegroundColor DarkGray
Write-Host "F00: $($dispatch.Facilitator)" -ForegroundColor DarkGray
Write-Host "Consensus: $($dispatch.OverallConsensus) | Escalation: $($dispatch.Escalation)" -ForegroundColor DarkGray
Write-Host "Queue: $($dispatch.Queue.Count) pulse(s)" -ForegroundColor Cyan

$manifestDir = Join-Path $EvidenceRoot "dispatch"
New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
$manifestPath = Join-Path $manifestDir ("dispatch-{0}.json" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
$dispatch | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $manifestPath

if ($dispatch.Queue.Count -eq 0) {
    Write-Host "No pulses queued." -ForegroundColor Yellow
    exit 0
}

foreach ($item in $dispatch.Queue) {
    Write-Host ""
    Write-Host ">> F00 dispatches $($item.SeatId) [$($item.Reason)]" -ForegroundColor Green
    if ($DryRun) {
        & (Join-Path $PSScriptRoot "Invoke-ShuraPulse.ps1") -SeatId $item.SeatId -Mode shura_dispatch -DryRun
    }
    else {
        & (Join-Path $PSScriptRoot "Invoke-ShuraPulse.ps1") -SeatId $item.SeatId -Mode shura_dispatch
        if ($PulseDelayMs -gt 0) { Start-Sleep -Milliseconds $PulseDelayMs }
    }
}

# F00 always closes with chronicle when real run
if (-not $DryRun) {
    Write-Host ""
    Write-Host ">> F00 closure pulse" -ForegroundColor Green
    & (Join-Path $PSScriptRoot "Invoke-ShuraPulse.ps1") -SeatId F00 -Mode shura_dispatch
}

Write-Host ""
Write-Host "=== Shura Dynamo complete ===" -ForegroundColor Cyan
Write-Host "Manifest: $manifestPath"
exit 0
