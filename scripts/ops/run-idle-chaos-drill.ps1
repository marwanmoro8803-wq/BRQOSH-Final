# System 106 - Idle chaos drill (opt-in via OMEGA_CHAOS_DRILL=1)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$scenarios = @(
    @{ id = "publish_lock"; simulated = "Desktop exe locked during publish" },
    @{ id = "missing_venv"; simulated = "AiCouncil venv absent" },
    @{ id = "shura_fail"; simulated = "Council exit non-zero" }
)

$outDir = Join-Path $repoRoot "dist\evidence\chaos-drill"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
@{ system = 106; scenarios = $scenarios; generatedUtc = (Get-Date).ToUniversalTime().ToString("o"); optIn = $true } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outDir "latest.json")
Write-Host "Chaos drill scenarios recorded (simulation only)"
exit 0
