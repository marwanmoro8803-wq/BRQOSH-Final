# Ops — CognitiveQa live smoke + evidence record (M11)
param(
    [string]$TargetSource = "",
    [int]$MaxSteps = 5,
    [string]$OutputDir = "audit_reports_live_smoke"
)

function Write-EvidenceResult {
    param([int]$ExitCode, [string]$Phase)
    $completed = Get-Date -Format "o"
    $yaml = @"
phase: M11-live-smoke
started: $script:Started
completed: $completed
exit_code: $ExitCode
status: $Phase
output_dir: $OutputDir
target: CognitiveQa.Desktop.exe
note: Requires Windows GUI + FlaUI — ⏭ if headless CI
"@
    Set-Content -Path (Join-Path $script:EvidenceDir "RESULT.yaml") -Value $yaml -Encoding UTF8
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cqaNet = Join-Path $repoRoot "CognitiveQa"
$script:EvidenceDir = Join-Path $repoRoot "dist\evidence\cognitive-qa-live-smoke"
$script:Started = Get-Date -Format "o"

if (-not $TargetSource) {
    $TargetSource = $cqaNet
}

New-Item -ItemType Directory -Force -Path $script:EvidenceDir | Out-Null

Push-Location $cqaNet
try {
    dotnet build CognitiveQa.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-EvidenceResult 3 "build_failed"
        exit $LASTEXITCODE
    }

    $desktopExe = Get-ChildItem -Path "src\CognitiveQa.Desktop\bin\Release\net10.0-windows\CognitiveQa.Desktop.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $desktopExe) {
        Write-EvidenceResult 3 "desktop_exe_missing"
        Write-Error "CognitiveQa.Desktop.exe not found after build"
        exit 3
    }

    Write-Host "Live smoke target: $desktopExe"

    dotnet run --project src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --no-build -- `
        --live `
        --target-app $desktopExe `
        --target-source $TargetSource `
        --scan-scope src `
        --max-steps $MaxSteps `
        --output-dir $OutputDir `
        --zip-evidence

    $code = $LASTEXITCODE
    Write-EvidenceResult $code "completed"
    exit $code
}
finally {
    Pop-Location
}
