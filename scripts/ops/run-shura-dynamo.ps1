# Ops wrapper - Shura Dynamo (F00 dispatches member pulses)
param(
    [string]$CouncilJson = "",
    [ValidateSet("Auto", "FailedOnly", "Maintain", "Manual", "All")]
    [string]$Mode = "Auto",
    [string[]]$Seats = @(),
    [string]$Seat = "",
    [switch]$DryRun,
    [switch]$RunCouncilFirst,
    [string]$CouncilSource = "dist/evidence/prompt-governance/CEEF-AUDIT-LATEST.md"
)

$ErrorActionPreference = "Stop"
$inner = Join-Path $PSScriptRoot "shura-dynamo"

if ($Seat) {
    $pulseArgs = @{ SeatId = $Seat; Mode = "manual" }
    if ($DryRun) { $pulseArgs.DryRun = $true }
    & (Join-Path $inner "Invoke-ShuraPulse.ps1") @pulseArgs
    exit $LASTEXITCODE
}

$orchArgs = @{
    Mode = $Mode
    CouncilSource = $CouncilSource
}
if ($CouncilJson) { $orchArgs.CouncilJson = $CouncilJson }
if ($Seats -and $Seats.Count -gt 0) { $orchArgs.Seats = $Seats }
if ($DryRun) { $orchArgs.DryRun = $true }
if ($RunCouncilFirst) { $orchArgs.RunCouncilFirst = $true }

& (Join-Path $inner "run-shura-dynamo.ps1") @orchArgs
exit $LASTEXITCODE
