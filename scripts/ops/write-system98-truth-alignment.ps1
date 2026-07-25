# System 98 - Truth alignment gate
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 98 Truth alignment ===" -ForegroundColor Cyan

$required = @(
    "docs\OPEN_ITEMS.md",
    "STATUS.yaml",
    "CognitiveQa\STATUS.yaml",
    "dist\evidence\prompt-governance\CEEF-SYSTEM-98-TRUTH-ALIGNMENT-001.md"
)
foreach ($rel in $required) {
    $p = Join-Path $repoRoot $rel
    if (-not (Test-Path $p)) {
        Write-Error "Missing required truth file: $rel"
        exit 1
    }
}

$hubStatus = Join-Path $repoRoot ".cursor\skills\brqosh-skills-hub\STATUS.yaml"
if (-not (Test-Path $hubStatus)) {
    Write-Error "Missing skills hub STATUS"
    exit 1
}
$hubText = Get-Content $hubStatus -Raw
if ($hubText -notmatch "sibling-reference") {
    Write-Error "Skills hub STATUS must declare sibling-reference"
    exit 1
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
dotnet build $sln -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testOut = dotnet test $sln -c Release --nologo --filter "FullyQualifiedName~TruthAlignment" 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$evidenceDir = Join-Path $repoRoot "dist\evidence\truth-alignment"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 98
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-SYSTEM-98-TRUTH-ALIGNMENT-001.md"
    openItems = "docs/OPEN_ITEMS.md"
    rootStatus = "STATUS.yaml"
    cognitiveQaStatus = "CognitiveQa/STATUS.yaml"
    siblingReference = $true
    truthAligned = $true
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 98 truth alignment PASS"
exit 0
