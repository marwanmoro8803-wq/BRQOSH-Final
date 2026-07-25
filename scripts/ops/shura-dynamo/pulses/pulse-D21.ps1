# Shura Dynamo pulse - D21 (مهندس Web Backend)
# Authority: F00 dispatches via run-shura-dynamo.ps1; on-demand via this script
param(
    [switch]$DryRun,
    [ValidateSet("on_demand", "shura_dispatch", "maintain", "manual")]
    [string]$Mode = "on_demand"
)

$ErrorActionPreference = "Stop"
$inner = Join-Path $PSScriptRoot "..\Invoke-ShuraPulse.ps1"
& $inner -SeatId "D21" -Mode $Mode @PSBoundParameters
exit $LASTEXITCODE

