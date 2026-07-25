# Dead-code advisory scan - build warnings CS0168 CS0219 IDE0051
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"

$buildOut = dotnet build $sln -c Release --nologo 2>&1 | Out-String
$patterns = @("CS0168", "CS0219", "CS0414", "IDE0051", "IDE0052")
$hits = @{}
foreach ($p in $patterns) {
    $hits[$p] = ([regex]::Matches($buildOut, $p)).Count
}
$total = ($hits.Values | Measure-Object -Sum).Sum

$evidenceDir = Join-Path $repoRoot "dist\evidence\dead-code-scan"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"
@{
    system = 121
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    warningHits = $hits
    totalWarningHits = $total
    advisoryOnly = $true
    pass = $true
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "Dead-code scan advisory - total warning hits=$total"
exit 0
