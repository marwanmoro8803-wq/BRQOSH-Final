# Ops — Sync CognitiveQa STATUS.yaml test count (M16)
param(
    [switch]$DryRun,
    [int]$TestCount = 0
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cqaNet = Join-Path $repoRoot "CognitiveQa"
$statusPath = Join-Path $cqaNet "STATUS.yaml"

if ($TestCount -le 0) {
    Push-Location $cqaNet
    try {
        $output = dotnet test CognitiveQa.sln -c Release --nologo 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Tests failed — STATUS not updated"
            exit $LASTEXITCODE
        }
        if ($output -match "Total:\s+(\d+)") { $TestCount = [int]$Matches[1] }
        else { Write-Error "Could not parse test count"; exit 1 }
    }
    finally { Pop-Location }
}

Write-Host "Test count: $TestCount"
if ($DryRun) { exit 0 }

$text = Get-Content $statusPath -Raw -Encoding UTF8
$text = $text -replace '(?m)^  count: \d+', "  count: $TestCount"
$text = $text -replace '(?m)^phase: M\d+', "phase: M16"
$text = $text -replace '(?m)^updated: .+', "updated: $(Get-Date -Format 'yyyy-MM-dd')"
Set-Content $statusPath -Value $text.TrimEnd() -Encoding UTF8 -NoNewline
Add-Content $statusPath -Value "`n" -Encoding UTF8
Write-Host "Updated $statusPath"
exit 0
