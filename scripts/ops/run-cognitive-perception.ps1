# Ops — Supreme cognitive perception gate (M15)
# Build + test + audit + verify COGNITIVE_PERCEPTION.md

param(
    [string]$RepoRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [int]$MaxSteps = 10,
    [switch]$Live,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

if (-not $SkipBuild) {
    dotnet build CognitiveQa/CognitiveQa.sln -c Release --nologo
    dotnet test CognitiveQa/CognitiveQa.sln -c Release --nologo
}

$auditArgs = @(
    "-MaxSteps", $MaxSteps,
    "-ScanScope", "src"
)
if ($Live) { $auditArgs += "-Live" }

& "$RepoRoot/scripts/ops/run-cognitive-audit.ps1" @auditArgs

$outDir = Join-Path $RepoRoot "CognitiveQa/audit_reports"
$perception = Join-Path $outDir "COGNITIVE_PERCEPTION.md"
if (-not (Test-Path $perception)) {
    Write-Error "COGNITIVE_PERCEPTION.md missing — perception mind not activated."
    exit 1
}

$content = Get-Content $perception -Raw
if ($content -notmatch "Perception confidence") {
    Write-Error "COGNITIVE_PERCEPTION.md incomplete."
    exit 1
}

Write-Host "Perception mind active: $perception"
exit 0
