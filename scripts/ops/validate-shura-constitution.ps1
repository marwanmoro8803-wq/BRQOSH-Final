# Validate Shura Constitution v5 — members · tools · heartbeat · engine
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$analysis = Join-Path $repoRoot ".cursor\skills\brqosh-analysis"
$failed = 0

$required = @(
    "shura-constitution.md",
    "shura-council-protocol.md",
    "shura-intelligence-core.md",
    "shura-heartbeat-core.md",
    "shura-members-registry.yaml",
    "shura-tools-registry.yaml",
    "shura-prompt-governance.md"
)

foreach ($name in $required) {
    $path = Join-Path $analysis $name
    if (Test-Path $path) {
        Write-Host "[PASS] $name" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] $name missing" -ForegroundColor Red
        $failed++
    }
}

$dynamoReg = Join-Path $repoRoot "scripts\ops\shura-dynamo\shura-dynamo-registry.json"
if (Test-Path $dynamoReg) {
    $json = Get-Content $dynamoReg -Raw | ConvertFrom-Json
    $count = ($json.members.PSObject.Properties | Measure-Object).Count
    if ($count -eq 41) {
        Write-Host "[PASS] Dynamo registry 41 members" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] Dynamo registry has $count members, expected 41" -ForegroundColor Red
        $failed++
    }
}
else {
    Write-Host "[FAIL] shura-dynamo-registry.json" -ForegroundColor Red
    $failed++
}

$pulseDir = Join-Path $repoRoot "scripts\ops\shura-dynamo\pulses"
if (Test-Path $pulseDir) {
    $pulseCount = (Get-ChildItem $pulseDir -Filter "pulse-*.ps1" | Measure-Object).Count
    if ($pulseCount -ge 41) {
        Write-Host "[PASS] Pulse scripts ($pulseCount >= 41)" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] Pulse scripts $pulseCount < 41" -ForegroundColor Red
        $failed++
    }
}

$csproj = Join-Path $repoRoot "ShuraCouncil\ShuraCouncil.csproj"
if (Test-Path $csproj) {
    Write-Host "[PASS] ShuraCouncil.csproj" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] ShuraCouncil.csproj" -ForegroundColor Red
    $failed++
}

$membersYaml = Join-Path $analysis "shura-members-registry.yaml"
if (Test-Path $membersYaml) {
    $content = Get-Content $membersYaml -Raw
    if ($content -match 'total_members:\s*41' -and $content -match 'S01' -and $content -match 'F00') {
        Write-Host "[PASS] Members registry structure" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] Members registry incomplete" -ForegroundColor Red
        $failed++
    }
}

$autoLaw = Join-Path $repoRoot ".cursor\rules\brqosh-shura-auto.mdc"
if (Test-Path $autoLaw) {
    Write-Host "[PASS] brqosh-shura-auto.mdc" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] brqosh-shura-auto.mdc" -ForegroundColor Red
    $failed++
}

if ($failed -gt 0) {
    Write-Error "Shura constitution validation failed: $failed"
    exit 1
}

Write-Host "Shura constitution validation PASS"
exit 0
