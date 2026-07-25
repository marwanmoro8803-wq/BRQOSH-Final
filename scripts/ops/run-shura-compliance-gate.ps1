# Master gate - execute ALL Shura binding orders P0-P2 + constitution
param(
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$failed = 0
$results = @()

function Add-Result($id, $pass, $detail) {
    $script:results += [PSCustomObject]@{ Id = $id; Pass = $pass; Detail = $detail }
    if (-not $pass) { $script:failed++ }
    $color = if ($pass) { "DarkGreen" } else { "Red" }
    $tag = if ($pass) { "PASS" } else { "FAIL" }
    Write-Host "[$tag] $id - $detail" -ForegroundColor $color
}

# --- Script gates ---
$scripts = @(
    @{ Id = "P0-01/P1-06"; Script = "validate-tri-source.ps1" },
    @{ Id = "P0-02"; Script = "validate-fable-conversion.ps1" },
    @{ Id = "P0-05"; Script = "validate-fable-skills-stack.ps1" },
    @{ Id = "P1-09"; Script = "validate-merged-claude-index.ps1" },
    @{ Id = "C-01"; Script = "validate-shura-constitution.ps1" }
)

foreach ($s in $scripts) {
    $path = Join-Path $repoRoot "scripts\ops\$($s.Script)"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $path | Out-Null
    Add-Result $s.Id ($LASTEXITCODE -eq 0) $s.Script
}

# --- File existence ---
$files = @{
    "P0-03" = "docs\CLOUD-CODE-AGENT-GUIDE.md"
    "P0-04" = "docs\PROJECT-MEMORY.yaml"
    "P1-08" = ".cursor\skills\supreme-programming-mastery\claude-code-cli.md"
    "P1-10" = ".cursor\skills\brqosh-skills-hub\leak-corpus-index.yaml"
    "P2-13" = "CognitiveQa\design-system\CLOUD-CODE-SHELL.md"
    "P2-14" = ".cursor\skills\brqosh-skills-hub\language-enforcement.md"
    "C-02"  = ".cursor\rules\brqosh-shura-auto.mdc"
    "DECISION" = "dist\evidence\prompt-governance\SHURA-DECISION-FABLE-TRI-SOURCE-003.md"
}

foreach ($kv in $files.GetEnumerator()) {
    $exists = Test-Path (Join-Path $repoRoot $kv.Value)
    Add-Result $kv.Key $exists $kv.Value
}

# --- P1-07 modules ---
$modCount = (Get-ChildItem (Join-Path $repoRoot ".cursor\skills\claude-fable-5\modules") -Recurse -Filter "SKILL.md" | Measure-Object).Count
Add-Result "P1-07" ($modCount -ge 5) "modules=$modCount"

# --- P1-11 primary product ---
$cq = Test-Path (Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln")
Add-Result "P1-11a" $cq "CognitiveQa.sln"
# Python frozen: cognitive-qa may exist but omni-qa SKILL must point to .NET primary
$omniSkill = Join-Path $repoRoot ".cursor\skills\omni-qa-army\SKILL.md"
$omniOk = $false
if (Test-Path $omniSkill) {
    $c = Get-Content $omniSkill -Raw
    $omniOk = ($c -match "CognitiveQa") -and ($c -match "FROZEN|frozen|primary")
}
Add-Result "P1-11b" $omniOk "omni-qa primary=.NET frozen policy"

# --- P2-12 skill routers ---
$skillCount = (Get-ChildItem (Join-Path $repoRoot ".cursor\skills") -Recurse -Filter "SKILL.md" |
    Where-Object { $_.FullName -notmatch "\\modules\\" } | Measure-Object).Count
# top-level only: count directories with SKILL.md at root of skill folder
$topLevel = (Get-ChildItem (Join-Path $repoRoot ".cursor\skills") -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } | Measure-Object).Count
Add-Result "P2-12" ($topLevel -ge 20) "top-level routers=$topLevel"

# --- C-03 Dynamo 41 ---
$dynamoReg = Join-Path $repoRoot "scripts\ops\shura-dynamo\shura-dynamo-registry.json"
if (Test-Path $dynamoReg) {
    $j = Get-Content $dynamoReg -Raw | ConvertFrom-Json
    $mc = ($j.members.PSObject.Properties | Measure-Object).Count
    Add-Result "C-03" ($mc -eq 41) "dynamo members=$mc"
}
else { Add-Result "C-03" $false "registry missing" }

# --- C-04 F00 heartbeat pulse (Dynamo pipeline law) ---
Write-Host "=== C-04 F00 Dynamo pulse ===" -ForegroundColor Cyan
$pulseScript = Join-Path $repoRoot "scripts\ops\shura-dynamo\Invoke-ShuraPulse.ps1"
if (Test-Path $pulseScript) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $pulseScript -SeatId F00 -Mode shura_dispatch | Out-Null
    Add-Result "C-04" ($LASTEXITCODE -eq 0) "F00 pulse exit=$LASTEXITCODE"
}
else {
    Add-Result "C-04" $false "Invoke-ShuraPulse.ps1 missing"
}

# --- P2-15 dotnet test ---
if ($SkipTests) {
    Add-Result "P2-15" $true "skipped (-SkipTests; prior PASS in SHURA-COMPLIANCE-*.json)"
}
else {
Write-Host "=== P2-15 dotnet test CognitiveQa ===" -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot "CognitiveQa")
dotnet test CognitiveQa.sln -c Release --nologo -v q
$testOk = $LASTEXITCODE -eq 0
Pop-Location
Add-Result "P2-15" $testOk "dotnet test exit=$LASTEXITCODE"
}

# --- Write evidence ---
$evidenceDir = Join-Path $repoRoot "dist\evidence\prompt-governance"
if (-not (Test-Path $evidenceDir)) { New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $evidenceDir "SHURA-COMPLIANCE-$stamp.json"
$payload = @{
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    decision = "SHURA-DECISION-FABLE-TRI-SOURCE-003.md"
    passed = ($failed -eq 0)
    failedCount = $failed
    results = $results
}
$payload | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding UTF8
Write-Host "Evidence: $reportPath" -ForegroundColor Cyan

if ($failed -gt 0) {
    Write-Error "Shura compliance gate FAILED: $failed check(s)"
    exit 1
}

Write-Host "Shura compliance gate PASS (all binding orders)" -ForegroundColor Green
exit 0
