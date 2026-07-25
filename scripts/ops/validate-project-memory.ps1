# Validates docs/PROJECT-MEMORY.yaml structure for agent memory governance.
param(
    [string]$RepoRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

$ErrorActionPreference = 'Stop'
$memoryPath = Join-Path $RepoRoot 'docs\PROJECT-MEMORY.yaml'

if (-not (Test-Path $memoryPath)) {
    Write-Error "Missing PROJECT-MEMORY.yaml at $memoryPath"
}

$lines = Get-Content $memoryPath
$content = $lines -join "`n"

if ($content -notmatch 'version:\s*\d+') { Write-Error 'Missing version field' }
if ($content -notmatch 'next_id:\s*\d+') { Write-Error 'Missing next_id field' }
if ($content -notmatch 'entries:') { Write-Error 'Missing entries block' }

$entryStarts = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*-\s*id:\s*(MEM-\d+)\s*$') {
        $entryStarts += [pscustomobject]@{ Index = $i; Id = $Matches[1] }
    }
}

if ($entryStarts.Count -eq 0) { Write-Error 'No MEM entries found' }

$seen = @{}
$requiredFields = @('date', 'type', 'problem', 'affected', 'how', 'verify')

for ($e = 0; $e -lt $entryStarts.Count; $e++) {
    $id = $entryStarts[$e].Id
    if ($seen.ContainsKey($id)) { Write-Error "Duplicate id: $id" }
    $seen[$id] = $true

    $start = $entryStarts[$e].Index
    $end = if ($e + 1 -lt $entryStarts.Count) { $entryStarts[$e + 1].Index - 1 } else { $lines.Count - 1 }
    $block = ($lines[$start..$end] -join "`n")

    foreach ($field in $requiredFields) {
        if ($block -notmatch "(?m)^\s*$field\s*:") {
            Write-Error "$id missing required field: $field"
        }
    }
}

$rulePath = Join-Path $RepoRoot '.cursor\rules\brqosh-deep-memory.mdc'
$impactPath = Join-Path $RepoRoot '.cursor\skills\brqosh-agent-protocol\impact-before-change.md'
foreach ($p in @($rulePath, $impactPath)) {
    if (-not (Test-Path $p)) { Write-Error "Missing governance file: $p" }
}

Write-Host "PASS - PROJECT-MEMORY.yaml ($($entryStarts.Count) entries) + governance files OK"
