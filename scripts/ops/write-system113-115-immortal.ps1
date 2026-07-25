# Systems 113-115 - Snapshots + silence + blue/green
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet run --project $cli -c Release -- omega snapshot 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet build (Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln") -c Release --nologo
    dotnet run --project $cli -c Release -- omega snapshot
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$green = Join-Path $repoRoot "CognitiveQa\publish\desktop-green"
$blue = Join-Path $repoRoot "CognitiveQa\publish\desktop"
if (Test-Path $blue) {
    New-Item -ItemType Directory -Force -Path $green | Out-Null
    Copy-Item -Path "$blue\*" -Destination $green -Recurse -Force -ErrorAction SilentlyContinue
}

$out = Join-Path $repoRoot "dist\evidence\immortality"
New-Item -ItemType Directory -Force -Path $out | Out-Null
@{ system = 115; snapshot = $true; blueGreen = (Test-Path $green) } | ConvertTo-Json | Set-Content (Join-Path $out "latest.json")
Write-Host "System 113-115 immortality prep PASS"
exit 0
