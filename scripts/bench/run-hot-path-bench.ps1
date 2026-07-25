# Hot path bench — System 102 (advisory)
$ErrorActionPreference = "Continue"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build (Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln") -c Release --nologo -v q 2>$null | Out-Null
$buildMs = $sw.ElapsedMilliseconds
$sw.Restart()
dotnet test (Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln") -c Release --no-build --filter "FullyQualifiedName~TruthAlignment" --nologo 2>$null | Out-Null
$testMs = $sw.ElapsedMilliseconds

$outDir = Join-Path $repoRoot "dist\evidence\bench"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
@{ buildMs = $buildMs; sampleTestMs = $testMs; generatedUtc = (Get-Date).ToUniversalTime().ToString("o") } | ConvertTo-Json | Set-Content (Join-Path $outDir "latest.json")
Write-Host "Bench: build=${buildMs}ms sampleTest=${testMs}ms"
exit 0
