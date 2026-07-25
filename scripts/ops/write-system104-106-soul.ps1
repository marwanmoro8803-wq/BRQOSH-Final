# Systems 104-106 - Awareness + event mesh + chaos drill (opt-in)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet run --project $cli -c Release -- omega suggest 2>$null
if ($LASTEXITCODE -gt 1) { exit $LASTEXITCODE }

$mesh = Join-Path $repoRoot "dist\evidence\event-mesh\events.jsonl"
if (-not (Test-Path $mesh)) {
    "" | Set-Content $mesh
}

if ($env:OMEGA_CHAOS_DRILL -eq "1") {
    & (Join-Path $PSScriptRoot "run-idle-chaos-drill.ps1")
}

$out = Join-Path $repoRoot "dist\evidence\omega-soul"
New-Item -ItemType Directory -Force -Path $out | Out-Null
@{ system = 106; awareness = $true; eventMesh = (Test-Path $mesh) } | ConvertTo-Json | Set-Content (Join-Path $out "latest.json")
Write-Host "System 104-106 soul layer PASS"
exit 0
