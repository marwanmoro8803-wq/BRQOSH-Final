# System 100 - Security gate (package vuln + pentest advisory)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"

$vulnOut = dotnet list $sln package --vulnerable --include-transitive 2>&1 | Out-String
$hasVuln = $vulnOut -match "has the following vulnerable"

$evidenceDir = Join-Path $repoRoot "dist\evidence\security-gate"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 100
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    vulnerablePackagesFound = $hasVuln
    advisoryOnly = $true
    detail = if ($hasVuln) { "Review dotnet list package --vulnerable output" } else { "No vulnerable packages reported" }
    pass = -not $hasVuln
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

if ($hasVuln) {
    Write-Host "System 100 security gate ADVISORY - vulnerabilities reported" -ForegroundColor Yellow
    exit 0
}
Write-Host "System 100 security gate PASS"
exit 0
