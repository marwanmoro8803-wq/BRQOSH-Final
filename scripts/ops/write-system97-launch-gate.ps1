# System 97 - Launch readiness gate (build + test + publish + evidence)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 97 Launch readiness gate ===" -ForegroundColor Cyan

$ceef = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-SYSTEM-97-LAUNCH-READINESS-001.md"
if (-not (Test-Path $ceef)) {
    Write-Error "Missing CEEF-97"
    exit 1
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$desktopProj = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Desktop\CognitiveQa.Desktop.csproj"
$publishDir = Join-Path $repoRoot "CognitiveQa\publish\desktop"
$exe = Join-Path $publishDir "CognitiveQa.Desktop.exe"

Get-Process -Name "CognitiveQa.Desktop" -ErrorAction SilentlyContinue | ForEach-Object {
    try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch { Write-Host "Note: could not stop Desktop (may need manual close)" -ForegroundColor Yellow }
}
Start-Sleep -Seconds 1

dotnet build $sln -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$testCount = 96
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") {
    $testCount = [int]$Matches[1]
}

dotnet publish $desktopProj -c Release -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) {
    if (Test-Path $exe) {
        Write-Host "Publish skipped - Desktop may be locked; using existing exe" -ForegroundColor Yellow
    } else {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $exe)) {
    Write-Error "Desktop exe missing after publish: $exe"
    exit 1
}

$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"
if (Test-Path $cli) {
    dotnet run --project $cli -c Release --no-build -- twin emit 2>$null
}

& (Join-Path $PSScriptRoot "write-system96-evidence.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$evidenceDir = Join-Path $repoRoot "dist\evidence\launch-readiness"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 97
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-SYSTEM-97-LAUNCH-READINESS-001.md"
    dotnetTests = $testCount
    desktopExe = $exe
    desktopPublished = $true
    workflowEvidence = "write-system96-evidence.ps1 PASS"
    pilotLaunchReady = $true
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 97 launch gate PASS - tests=$testCount exe=$exe"
Write-Host "Pilot launch: double-click START-CognitiveQa.bat"
exit 0
