# Ops — audit sibling BRQOSH repo when BRQOSH_ROOT or ../BRQOSH exists
param(
    [string]$BrqoshRoot = "",
    [int]$MaxSteps = 15,
    [switch]$Live,
    [switch]$TryBuild,
    [string]$OutputDir = "audit_reports_brqosh"
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cqaNet = Join-Path $repoRoot "CognitiveQa"

if (-not $BrqoshRoot) {
    $BrqoshRoot = $env:BRQOSH_ROOT
}
if (-not $BrqoshRoot) {
    $candidate = Join-Path (Split-Path $repoRoot -Parent) "BRQOSH"
    if (Test-Path $candidate) { $BrqoshRoot = $candidate }
}

if (-not $BrqoshRoot -or -not (Test-Path $BrqoshRoot)) {
    Write-Host "⏭ BRQOSH not found — set BRQOSH_ROOT or clone sibling BRQOSH/"
    Write-Host "In-repo CognitiveQa prep complete; live ERP audit deferred."
    exit 0
}

Write-Host "BRQOSH target: $BrqoshRoot"

Push-Location $cqaNet
try {
    $args = @(
        "run", "--project", "src/CognitiveQa.Cli/CognitiveQa.Cli.csproj",
        "-c", "Release", "--",
        "--target-source", $BrqoshRoot,
        "--scan-scope", "src",
        "--max-steps", $MaxSteps,
        "--output-dir", $OutputDir,
        "--zip-evidence"
    )
    if ($Live) { $args += "--live" }
    if ($TryBuild) { $args += "--try-build" }

    & dotnet @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
