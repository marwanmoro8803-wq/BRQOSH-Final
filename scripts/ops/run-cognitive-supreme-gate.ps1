# Ops — Supreme holistic gate M16: build + test + supreme audit + perception + Shura
param(
    [string]$TargetSource = "CognitiveQa",
    [switch]$SkipShura,
    [switch]$Live
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cqaNet = Join-Path $repoRoot "CognitiveQa"

Push-Location $cqaNet
try {
    dotnet build CognitiveQa.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet test CognitiveQa.sln -c Release --nologo --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$resolvedTarget = if ([System.IO.Path]::IsPathRooted($TargetSource)) { $TargetSource } else { Join-Path $repoRoot $TargetSource }

$auditParams = @{
    TargetSource     = $resolvedTarget
    ScanScope        = "src"
    MaxSteps         = 8
    ZipEvidence      = $true
    ArchiveEvidence  = $true
    VerifyTests      = $true
    SkipBuild        = $true
}
if ($Live) { $auditParams.Live = $true }

& (Join-Path $PSScriptRoot "run-cognitive-audit.ps1") @auditParams
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$supreme = Join-Path $cqaNet "audit_reports\SUPREME_CLOSURE.md"
if (-not (Test-Path $supreme)) {
    Write-Error "SUPREME_CLOSURE.md missing — M16 holistic gate failed."
    exit 1
}

& (Join-Path $PSScriptRoot "sync-cognitive-status.ps1") -TestCount 49 -ErrorAction SilentlyContinue

if (-not $SkipShura) {
    & (Join-Path $PSScriptRoot "run-shura-council.ps1") -Source "dist/evidence/prompt-governance/CEEF-REPLATFORM-001.md"
    exit $LASTEXITCODE
}
Write-Host "Supreme gate PASS: $supreme"
exit 0
