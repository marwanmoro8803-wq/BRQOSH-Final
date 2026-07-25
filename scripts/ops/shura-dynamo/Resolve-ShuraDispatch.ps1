# F00 dispatch resolver — reads Shura council JSON and decides which pulses to run
param(
    [string]$CouncilJsonPath = "",
    [ValidateSet("Auto", "FailedOnly", "Maintain", "Manual", "All")]
    [string]$Mode = "Auto",
    [string[]]$Seats = @()
)

. (Join-Path $PSScriptRoot "_pulse-common.ps1")

function Get-SeatFailed {
    param($SeatObj)
    $failed = @($SeatObj.findings | Where-Object { $_.Pass -eq $false })
    return $failed.Count -gt 0
}

function Get-SeatReady {
    param($SeatObj)
    if (-not $SeatObj.findings) { return $true }
    return -not (Get-SeatFailed $SeatObj)
}

$file = if ($CouncilJsonPath) { Get-Item $CouncilJsonPath } else { Get-LatestCouncilJson }
if (-not $file) {
    return [pscustomobject]@{
        CouncilPath = $null
        Facilitator = $null
        Escalation = $false
        Queue = @()
        Reason = "no council JSON - run Shura first or pass -Seats"
    }
}

$council = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
$reg = Get-ShuraDynamoRegistry
$queue = [System.Collections.Generic.List[object]]::new()

function Add-QueueItem {
    param([string]$Id, [string]$Reason, [int]$Priority)
    if ($queue | Where-Object { $_.SeatId -eq $Id }) { return }
    $def = $reg.members.$Id
    $queue.Add([pscustomobject]@{
        SeatId = $Id
        Tier = $def.tier
        RoleAr = $def.roleAr
        Band = $def.band
        Priority = $Priority
        Reason = $Reason
    })
}

# Manual override — F00 explicit dispatch
if ($Seats -and $Seats.Count -gt 0) {
    foreach ($s in $Seats) {
        $id = $s.ToUpper()
        Add-QueueItem -Id $id -Reason "F00 manual dispatch" -Priority 999
    }
}
elseif ($Mode -eq "All") {
    foreach ($prop in $reg.members.PSObject.Properties) {
        if ($prop.Name -eq "F00") { continue }
        $band = $reg.priorityBands.($prop.Value.band)
        Add-QueueItem -Id $prop.Name -Reason "Mode=All full council pulse" -Priority $band
    }
}
elseif ($Mode -eq "Maintain") {
    foreach ($id in @("S08", "S11", "S12", "D11", "D28", "F00")) {
        $band = $reg.priorityBands.($reg.members.$id.band)
        Add-QueueItem -Id $id -Reason "heartbeat maintainers" -Priority $band
    }
}
else {
    # Auto / FailedOnly — Shura evidence drives dispatch
    $escalation = $false
    if ($council.facilitator -and $council.facilitator.EscalationRequired) {
        $escalation = $true
    }

    if ($escalation) {
        foreach ($id in @("S04", "D13", "S05")) {
            $band = $reg.priorityBands.($reg.members.$id.band)
            Add-QueueItem -Id $id -Reason "F00 escalation - security first" -Priority ($band + 50)
        }
    }

    foreach ($seat in $council.seats) {
        $id = $seat.SeatId
        if (Get-SeatFailed $seat) {
            $band = $reg.priorityBands.($reg.members.$id.band)
            if (-not $band) { $band = 40 }
            Add-QueueItem -Id $id -Reason "core seat failed finding" -Priority $band
        }
        elseif ($Mode -eq "Auto" -and -not (Get-SeatReady $seat)) {
            $band = $reg.priorityBands.($reg.members.$id.band)
            Add-QueueItem -Id $id -Reason "core seat conditional" -Priority ($band - 5)
        }
    }

    if ($council.disciplines -and $council.disciplines.seats) {
        foreach ($d in $council.disciplines.seats) {
            $id = $d.SeatId
            if (Get-SeatFailed $d) {
                $band = $reg.priorityBands.($reg.members.$id.band)
                if (-not $band) { $band = 30 }
                Add-QueueItem -Id $id -Reason "discipline not ready" -Priority $band
            }
        }
    }

    if ($queue.Count -eq 0 -and $Mode -eq "Auto") {
        foreach ($id in @("F00", "S11", "S12")) {
            $band = $reg.priorityBands.($reg.members.$id.band)
            Add-QueueItem -Id $id -Reason "consensus OK - F00 closure pulse" -Priority $band
        }
    }
}

$sorted = $queue | Sort-Object -Property Priority -Descending

return [pscustomobject]@{
    CouncilPath = $file.FullName
    Facilitator = $council.facilitator.SummaryAr
    Escalation = [bool]$council.facilitator.EscalationRequired
    OverallConsensus = [bool]$council.overallConsensus
    Queue = $sorted
    Reason = "F00 dispatch via Mode=$Mode"
}
