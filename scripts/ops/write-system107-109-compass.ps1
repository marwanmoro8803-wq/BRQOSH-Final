# Systems 107-109 - ADR + covenant + compass
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$required = @(
    "docs\architecture\ADR-OMEGA-001-005.md",
    "docs\OMEGA-DIGITAL-COVENANT-ar.md",
    "CognitiveQa\COMPASS.yaml"
)
foreach ($r in $required) {
    if (-not (Test-Path (Join-Path $repoRoot $r))) { Write-Error "Missing $r"; exit 1 }
}

$out = Join-Path $repoRoot "dist\evidence\omega-compass"
New-Item -ItemType Directory -Force -Path $out | Out-Null
@{ system = 109; compass = "CognitiveQa/COMPASS.yaml"; covenant = "docs/OMEGA-DIGITAL-COVENANT-ar.md" } | ConvertTo-Json | Set-Content (Join-Path $out "latest.json")
Write-Host "System 107-109 compass gate PASS"
exit 0
