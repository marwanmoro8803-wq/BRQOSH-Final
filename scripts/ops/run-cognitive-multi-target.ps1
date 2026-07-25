# Ops — Multi-target audit: CognitiveQa dogfood + optional BRQOSH sibling (M14)
param(
    [int]$MaxSteps = 10,
    [switch]$Live,
    [switch]$TryBuild,
    [switch]$ArchiveEvidence
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$results = @()

Write-Host "=== Target 1: CognitiveQa (dogfood) ==="
& (Join-Path $PSScriptRoot "run-cognitive-audit.ps1") `
    -TargetSource (Join-Path $repoRoot "CognitiveQa") `
    -ScanScope src `
    -MaxSteps $MaxSteps `
    -OutputDir (Join-Path $repoRoot "CognitiveQa\audit_reports_multi_cqa") `
    -ZipEvidence `
    -SkipBuild `
    $(if ($ArchiveEvidence) { "-ArchiveEvidence" } )
$results += @{ target = "CognitiveQa"; exit = $LASTEXITCODE }

Write-Host "=== Target 2: BRQOSH (if present) ==="
& (Join-Path $PSScriptRoot "run-cognitive-brqosh-audit.ps1") `
    -MaxSteps $MaxSteps `
    $(if ($Live) { "-Live" } ) `
    $(if ($TryBuild) { "-TryBuild" } ) `
    $(if ($ArchiveEvidence) { "-ArchiveEvidence" } ) `
    -OutputDir "audit_reports_multi_brqosh"
$brqoshCode = $LASTEXITCODE
if ($brqoshCode -ne 0 -and $brqoshCode -ne 3) {
    $results += @{ target = "BRQOSH"; exit = $brqoshCode }
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\cognitive-qa-multi-target"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$yaml = "completed: $(Get-Date -Format o)`nresults:`n"
foreach ($r in $results) {
    $yaml += "  - target: $($r.target)`n    exit_code: $($r.exit)`n"
}
Set-Content -Path (Join-Path $evidenceDir "RESULT.yaml") -Value $yaml -Encoding UTF8

$worst = ($results | ForEach-Object { $_.exit } | Measure-Object -Maximum).Maximum
exit $worst
