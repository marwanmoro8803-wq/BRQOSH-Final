# System 96 - Workflow audit evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 96 Workflow audit evidence ===" -ForegroundColor Cyan

$ceef = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-SYSTEM-96-WORKFLOW-AUDIT-001.md"
if (-not (Test-Path $ceef)) {
    Write-Error "Missing CEEF-96"
    exit 1
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$testOut = dotnet test $sln -c Release --nologo --filter "FullyQualifiedName~Workflow|FullyQualifiedName~Dynamo|FullyQualifiedName~Preset|FullyQualifiedName~EvaluatePipeline" 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$evidenceDir = Join-Path $repoRoot "dist\evidence\workflow-audit"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 96
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-SYSTEM-96-WORKFLOW-AUDIT-001.md"
    workflowFilterTestsPass = $true
    findingsClosed = 9
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 96 evidence PASS - $latest"
exit 0
