# Shura Dynamo pulse - D25 (مهندس الأداء)
# Authority: F00 dispatches via run-shura-dynamo.ps1; on-demand via this script
param(
    [switch]$DryRun,
    [ValidateSet("on_demand", "shura_dispatch", "maintain", "manual")]
    [string]$Mode = "on_demand"
)

$ErrorActionPreference = "Stop"
$inner = Join-Path $PSScriptRoot "..\Invoke-ShuraPulse.ps1"
& $inner -SeatId "D25" -Mode $Mode @PSBoundParameters
exit $LASTEXITCODE

