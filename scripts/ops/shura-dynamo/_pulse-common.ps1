# Shura Dynamo — shared pulse utilities (System 92)
$script:ShuraDynamoRoot = $PSScriptRoot
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$script:OpsRoot = Join-Path $RepoRoot "scripts\ops"
$script:EvidenceRoot = Join-Path $RepoRoot "dist\evidence\shura-dynamo"

function Get-ShuraDynamoRegistry {
    $path = Join-Path $ShuraDynamoRoot "shura-dynamo-registry.json"
    if (-not (Test-Path $path)) { throw "Registry not found: $path" }
    return Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-ShuraMemberDef {
    param([string]$SeatId)
    $reg = Get-ShuraDynamoRegistry
    $key = $SeatId.ToUpper()
    if (-not $reg.members.PSObject.Properties.Name.Contains($key)) {
        throw "Unknown seat: $SeatId"
    }
    return $reg.members.$key
}

function Get-LatestCouncilJson {
    $dir = Join-Path $RepoRoot "dist\evidence\shura-council"
    if (-not (Test-Path $dir)) { return $null }
    return Get-ChildItem $dir -Filter "council-*.json" |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Test-MarkerPresence {
    param([string[]]$Markers)
    if (-not $Markers -or $Markers.Count -eq 0) { return @{ found = 0; total = 0; ok = $true } }
    $found = 0
    foreach ($m in $Markers) {
        $hits = @(Get-ChildItem -Path $RepoRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(node_modules|\.git|bin|obj|\.venv)\\' } |
            Where-Object { $_.Name -like "*$m*" -or $_.FullName -like "*$m*" })
        if ($hits.Count -gt 0) { $found++ }
    }
    return @{ found = $found; total = $Markers.Count; ok = ($found -gt 0) }
}

function Write-PulseEvidence {
    param(
        [string]$SeatId,
        [bool]$Passed,
        [string]$Mode,
        [string]$Summary,
        [object]$Extra = $null
    )
    $dir = Join-Path $EvidenceRoot "pulses"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $path = Join-Path $dir "pulse-$SeatId-$stamp.json"
    $payload = [ordered]@{
        seatId = $SeatId
        passed = $Passed
        mode = $Mode
        summary = $Summary
        generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
        extra = $Extra
    }
    $payload | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 $path
    return $path
}

function Write-PulseHeartbeat {
    param(
        [string]$SeatId,
        [string]$RoleAr,
        [string]$Phase,
        [string]$Thought = ""
    )
    $beat = [char]0x2668
    $line = "$beat $SeatId $RoleAr - $Phase"
    if ($Thought) { $line += " | $Thought" }
    Write-Host $line -ForegroundColor Cyan
}

function Invoke-DotnetPulse {
    param([string]$CommandArgs)
    Push-Location $RepoRoot
    try {
        $parts = $CommandArgs -split '\s+'
        $all = @("dotnet") + $parts
        & $all[0] $all[1..($all.Length - 1)]
        return $LASTEXITCODE
    }
    finally { Pop-Location }
}

function Invoke-NpmPulse {
    param([string]$Cwd, [string]$CommandArgs)
    $full = Join-Path $RepoRoot ($Cwd -replace '/', '\')
    if (-not (Test-Path $full)) { return 2 }
    Push-Location $full
    try {
        $parts = $CommandArgs -split '\s+'
        & npm @parts
        return $LASTEXITCODE
    }
    finally { Pop-Location }
}

function Invoke-OpsScriptPulse {
    param([string]$ScriptName, [string[]]$Args)
    $scriptPath = Join-Path $OpsRoot $ScriptName
    if (-not (Test-Path $scriptPath)) { return 2 }
    & $scriptPath @Args | Out-Null
    return $LASTEXITCODE
}
