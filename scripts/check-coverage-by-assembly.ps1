# Reports cobertura line-rate per production assembly (from latest cobertura artifact).
# Complements check-coverage.ps1 aggregate gate — tracks Domain/Application/Infrastructure/Api.

param(
    [string]$ResultsDir = "TestResults",
    [double]$MinimumLineRate = 0.75,
    [string[]]$RequiredAssemblies = @(
        "Brqosh.Application",
        "Brqosh.Infrastructure",
        "Brqosh.Domain",
        "Brqosh.Api"
    )
)

$ErrorActionPreference = "Stop"

$files = @(Get-ChildItem -Path $ResultsDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue)
if ($files.Count -eq 0) {
    Write-Error "No coverage.cobertura.xml under $ResultsDir. Run: dotnet test Brqosh.sln --collect:`"XPlat Code Coverage`""
    exit 1
}

$coverageFile = $files | Sort-Object LastWriteTime -Descending | Select-Object -First 1
[xml]$doc = Get-Content -LiteralPath $coverageFile.FullName

$rows = @()
$failures = @()

foreach ($assemblyName in $RequiredAssemblies) {
    $pkg = $doc.coverage.packages.package | Where-Object { $_.name -like "*$assemblyName*" } | Select-Object -First 1
    if ($null -eq $pkg) {
        $rows += [pscustomobject]@{
            Assembly = $assemblyName
            LineRate = "n/a"
            Lines = "not in report"
            Gate = "SKIP (no tests hit assembly)"
        }
        continue
    }

    $rate = [double]$pkg.'line-rate'
    $pct = [math]::Round($rate * 100, 2)
    $valid = $pkg.'lines-valid'
    $covered = $pkg.'lines-covered'
    $pass = $rate -ge $MinimumLineRate
    if (-not $pass) { $failures += $assemblyName }

    $rows += [pscustomobject]@{
        Assembly = $assemblyName
        LineRate = "$pct%"
        Lines = "$covered / $valid"
        Gate = $(if ($pass) { "PASS" } else { "FAIL" })
    }
}

Write-Host "## Coverage by assembly"
Write-Host ""
$rows | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Report: ``$($coverageFile.FullName)``"
Write-Host "Minimum gate per listed assembly: $([math]::Round($MinimumLineRate * 100, 0))%"

if ($failures.Count -gt 0) {
    Write-Error "Assemblies below gate: $($failures -join ', ')"
    exit 1
}

Write-Host "Per-assembly coverage gate passed for measured assemblies."
