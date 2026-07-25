# Promote canary to desktop — System 110
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$canary = Join-Path $repoRoot "CognitiveQa\publish\canary"
$desktop = Join-Path $repoRoot "CognitiveQa\publish\desktop"
if (-not (Test-Path $canary)) { Write-Error "Canary missing"; exit 1 }
Get-Process -Name "CognitiveQa.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Copy-Item -Path "$canary\*" -Destination $desktop -Recurse -Force
Write-Host "Canary promoted to desktop"
exit 0
