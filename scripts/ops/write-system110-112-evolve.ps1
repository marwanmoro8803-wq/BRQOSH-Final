# Systems 110-112 - Canary + seed + contracts
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$desktop = Join-Path $repoRoot "CognitiveQa\publish\desktop"
$canary = Join-Path $repoRoot "CognitiveQa\publish\canary"
if (Test-Path $desktop) {
    New-Item -ItemType Directory -Force -Path $canary | Out-Null
    Copy-Item -Path "$desktop\*" -Destination $canary -Recurse -Force -ErrorAction SilentlyContinue
}

& (Join-Path $PSScriptRoot "write-system112-contract-gate.ps1")
exit $LASTEXITCODE
