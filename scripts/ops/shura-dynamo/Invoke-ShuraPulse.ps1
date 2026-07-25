# Invoke one Shura Dynamo pulse for a council seat (S01-S12, D01-D28, F00)
param(
    [Parameter(Mandatory = $true)]
    [string]$SeatId,
    [ValidateSet("on_demand", "shura_dispatch", "maintain", "manual")]
    [string]$Mode = "on_demand",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_pulse-common.ps1")

$seat = $SeatId.ToUpper()
$def = Get-ShuraMemberDef -SeatId $seat
$reg = Get-ShuraDynamoRegistry
$band = $reg.priorityBands.$($def.band)
if (-not $band) { $band = 30 }

Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase "START" -Thought $def.roleEn

if ($DryRun) {
    Write-Host "  DRY-RUN preview: ops=$($def.opsScript) dotnet=$($def.dotnetCommand)" -ForegroundColor DarkYellow
    Write-PulseEvidence -SeatId $seat -Passed $false -Mode $Mode -Summary "dry-run - no work executed" -Extra @{ dryRun = $true }
    exit 0
}

$exit = 0
$summary = ""
$extra = @{}

# 1) Ops script pulse (primary action)
if ($def.opsScript) {
    Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase "OPS" -Thought $def.opsScript
    $opsArgs = @()
    if ($def.opsArgs) { $opsArgs = @($def.opsArgs) }
    $exit = Invoke-OpsScriptPulse -ScriptName $def.opsScript -Args $opsArgs
    $summary = "ops:$($def.opsScript) exit=$exit"
}

# 2) dotnet command
elseif ($def.dotnetCommand) {
    Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase "DOTNET" -Thought $def.dotnetCommand
    $exit = Invoke-DotnetPulse -CommandArgs $def.dotnetCommand
    $summary = "dotnet exit=$exit"
}

# 3) npm command
elseif ($def.npmCommand) {
    Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase "NPM" -Thought $def.npmCommand
    $exit = Invoke-NpmPulse -Cwd $def.npmCwd -CommandArgs $def.npmCommand
    $summary = "npm exit=$exit"
}

# 4) Marker scan fallback (disciplines without local stack)
else {
    Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase "SCAN" -Thought "marker scan"
    $scan = Test-MarkerPresence -Markers @($def.markerScan)
    $extra.scan = $scan
    if ($scan.total -eq 0) {
        $exit = 0
        $summary = "noop - no markers configured"
    }
    elseif ($scan.ok) {
        $exit = 0
        $summary = "markers $($scan.found)/$($scan.total)"
    }
    else {
        $exit = 1
        $summary = "markers missing 0/$($scan.total)"
    }
}

# Always verify markers as secondary signal when ops ran
if ($def.markerScan -and $def.markerScan.Count -gt 0 -and ($def.opsScript -or $def.dotnetCommand)) {
    $scan2 = Test-MarkerPresence -Markers @($def.markerScan)
    $extra.markerCheck = $scan2
}

$passed = $exit -eq 0
$evidence = Write-PulseEvidence -SeatId $seat -Passed $passed -Mode $Mode -Summary $summary -Extra $extra

Write-PulseHeartbeat -SeatId $seat -RoleAr $def.roleAr -Phase $(if ($passed) { "DONE" } else { "PARTIAL" }) -Thought $summary
Write-Host "  evidence: $evidence" -ForegroundColor DarkGray

if ($passed) { exit 0 }
exit 1
