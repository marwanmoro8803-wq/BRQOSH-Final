# Coverlet advisory gate - solution coverage snapshot
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$threshold = 80
$targets = @("CognitiveQa.Core", "CognitiveQa.Orchestrator", "CognitiveQa.Repair")

$outDir = Join-Path $repoRoot "dist\evidence\coverlet\run"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
dotnet test $sln -c Release --collect:"XPlat Code Coverage" --results-directory $outDir --nologo 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$rates = @()
foreach ($name in $targets) {
    $cob = Get-ChildItem -Path $outDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match [regex]::Escape($name) } |
        Select-Object -First 1
    if (-not $cob) {
        $cob = Get-ChildItem -Path $outDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    $rate = 0.0
    if ($cob) {
        [xml]$xml = Get-Content $cob.FullName
        $pkg = $xml.coverage.packages.package | Where-Object { $_.name -like "*$name*" } | Select-Object -First 1
        if ($pkg -and $pkg.'line-rate') {
            $rate = [double]$pkg.'line-rate' * 100.0
        } elseif ($xml.coverage.'line-rate') {
            $rate = [double]$xml.coverage.'line-rate' * 100.0
        }
    }
    $rates += [ordered]@{ project = $name; lineRatePercent = [math]::Round($rate, 1) }
}

$avg = if ($rates.Count -gt 0) { ($rates | ForEach-Object { $_.lineRatePercent } | Measure-Object -Average).Average } else { 0 }
$pass = $avg -ge $threshold

$evidenceDir = Join-Path $repoRoot "dist\evidence\coverlet-gate"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"
@{
    system = 121
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    thresholdPercent = $threshold
    averageLineRatePercent = [math]::Round($avg, 1)
    advisoryOnly = -not $pass
    pass = $pass
    projects = $rates
} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $latest

if (-not $pass) {
    Write-Host "Coverlet gate ADVISORY - avg $([math]::Round($avg,1))% < $threshold%" -ForegroundColor Yellow
    exit 0
}
Write-Host "Coverlet gate PASS - avg $([math]::Round($avg,1))%"
exit 0
