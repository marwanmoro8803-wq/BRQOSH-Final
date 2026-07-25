# Validates cobertura line-rate from dotnet test --collect:"XPlat Code Coverage".
# Default gate 75% on collected assemblies (Application.Tests dominate). Not whole-solution LOC.

param(
    [string]$ResultsDir = "TestResults",
    [double]$MinimumLineRate = 0.75
)

$ErrorActionPreference = "Stop"

$files = @(Get-ChildItem -Path $ResultsDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue)
if ($files.Count -eq 0) {
    Write-Error "No coverage.cobertura.xml found under $ResultsDir. Run tests with --collect:`"XPlat Code Coverage`" first."
    exit 1
}

$coverageFile = $files | Sort-Object LastWriteTime -Descending | Select-Object -First 1
[xml]$doc = Get-Content -LiteralPath $coverageFile.FullName
$lineRate = [double]$doc.coverage.'line-rate'
$linesValid = $doc.coverage.'lines-valid'
$linesCovered = $doc.coverage.'lines-covered'
$pct = [math]::Round($lineRate * 100, 2)

$summary = @"
## Code coverage (cobertura)

| Metric | Value |
|--------|-------|
| Line rate | **$pct%** |
| Lines covered | $linesCovered / $linesValid |
| Minimum gate | $([math]::Round($MinimumLineRate * 100, 0))% |
| Report | ``$($coverageFile.FullName)`` |

> Gate is $($MinimumLineRate * 100)% until full-solution coverage collection is in place.
"@

Write-Host $summary

if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $summary
}

if ($lineRate -lt $MinimumLineRate) {
    Write-Error "Line coverage $pct% is below minimum $($MinimumLineRate * 100)%."
    exit 1
}

Write-Host "Coverage gate passed ($pct% >= $($MinimumLineRate * 100)%)."
