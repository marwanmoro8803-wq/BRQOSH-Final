# Generate thin pulse-{SeatId}.ps1 wrappers from shura-dynamo-registry.json
param([switch]$Force)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_pulse-common.ps1")

$reg = Get-ShuraDynamoRegistry
$pulseDir = Join-Path $PSScriptRoot "pulses"
New-Item -ItemType Directory -Force -Path $pulseDir | Out-Null

$created = 0
$skipped = 0

foreach ($prop in $reg.members.PSObject.Properties) {
    $id = $prop.Name
    $def = $prop.Value
    $path = Join-Path $pulseDir "pulse-$id.ps1"
    if ((Test-Path $path) -and -not $Force) {
        $skipped++
        continue
    }

    $desc = $def.description
    if (-not $desc) { $desc = $def.roleEn }

    @"
# Shura Dynamo pulse - $id ($($def.roleAr))
# Authority: F00 dispatches via run-shura-dynamo.ps1; on-demand via this script
param(
    [switch]`$DryRun,
    [ValidateSet("on_demand", "shura_dispatch", "maintain", "manual")]
    [string]`$Mode = "on_demand"
)

`$ErrorActionPreference = "Stop"
`$inner = Join-Path `$PSScriptRoot "..\Invoke-ShuraPulse.ps1"
& `$inner -SeatId "$id" -Mode `$Mode @PSBoundParameters
exit `$LASTEXITCODE

"@ | Set-Content -Encoding UTF8 $path

    $created++
}

Write-Host "Shura Dynamo pulses: created=$created skipped=$skipped dir=$pulseDir"
exit 0
