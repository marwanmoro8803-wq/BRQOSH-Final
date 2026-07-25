# Ops — Cognitive QA audit (Shura · CEEF-REPLATFORM-001)

param(

    [string]$TargetSource = "",

    [string]$TargetApp = "",

    [string]$ScanScope = "src",

    [int]$MaxSteps = 10,

    [switch]$Live,

    [switch]$PerfectionLoop,

    [switch]$TryBuild,

    [switch]$AutoApply,

    [switch]$ZipEvidence,

    [switch]$VerifyTests,

    [switch]$ScanVulnerabilities,
    [switch]$ArchiveEvidence,

    [string]$Language = "en",

    [string]$OutputDir = "audit_reports",

    [switch]$SkipBuild,

    [switch]$SkipInstall,

    [switch]$LegacyPython

)



$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$cqaNet = Join-Path $repoRoot "CognitiveQa"

$cqaPy = Join-Path $repoRoot "cognitive-qa"



if (-not $TargetSource) {

    $TargetSource = $repoRoot

}



if (-not [System.IO.Path]::IsPathRooted($OutputDir)) {

    if ($OutputDir -eq "audit_reports") {

        $OutputDir = Join-Path $cqaNet "audit_reports"

    }

    else {

        $OutputDir = Join-Path $repoRoot $OutputDir

    }

}



if ($LegacyPython) {

    if (-not (Test-Path $cqaPy)) {

        Write-Error "cognitive-qa (legacy Python) not found at $cqaPy"

        exit 3

    }

    Push-Location $cqaPy

    try {

        if (-not $SkipInstall) {

            $prevEap = $ErrorActionPreference

            $ErrorActionPreference = "Continue"

            & py -3.12 -m pip install -q -r requirements.txt *> $null

            if ($Live -or $TargetApp) {

                & py -3.12 -m pip install -q -r requirements-live.txt *> $null

            }

            $ErrorActionPreference = $prevEap

        }

        $pyArgs = @(

            "-m", "cognitive_qa",

            "--target-source", $TargetSource,

            "--max-steps", $MaxSteps,

            "--output-dir", $OutputDir,

            "--language", $Language

        )

        if ($TargetApp) { $pyArgs += @("--target-app", $TargetApp) }

        if ($Live) { $pyArgs += "--live" }

        if ($PerfectionLoop) { $pyArgs += "--perfection-loop" }

        & py -3.12 @pyArgs

        exit $LASTEXITCODE

    }

    finally {

        Pop-Location

    }

}



if (-not (Test-Path $cqaNet)) {

    Write-Error "CognitiveQa not found at $cqaNet"

    exit 3

}



Push-Location $cqaNet

try {

    if (-not $SkipBuild) {

        dotnet build CognitiveQa.sln -c Release --nologo

        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    }



    $dotnetArgs = @(

        "run", "--project", "src/CognitiveQa.Cli/CognitiveQa.Cli.csproj",

        "-c", "Release", "--no-build",

        "--",

        "--target-source", $TargetSource,

        "--max-steps", $MaxSteps,

        "--output-dir", $OutputDir,

        "--language", $Language

    )

    if ($TargetApp) { $dotnetArgs += @("--target-app", $TargetApp) }

    if ($Live) { $dotnetArgs += "--live" }

    if ($PerfectionLoop) { $dotnetArgs += "--perfection-loop" }

    if ($TryBuild) { $dotnetArgs += "--try-build" }

    if ($AutoApply) { $dotnetArgs += "--auto-apply" }

    if ($ScanScope) { $dotnetArgs += @("--scan-scope", $ScanScope) }

    if ($ZipEvidence) { $dotnetArgs += "--zip-evidence" }

    if ($VerifyTests) { $dotnetArgs += "--verify-tests" }

    if ($ScanVulnerabilities) { $dotnetArgs += "--scan-vulnerabilities" }
    if ($ArchiveEvidence) { $dotnetArgs += "--archive-evidence" }

    & dotnet @dotnetArgs

    exit $LASTEXITCODE

}

finally {

    Pop-Location

}

