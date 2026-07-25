# One-time: add Cloud Code pointer to SKILL.md routers missing it
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$line = '> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)'
$modLine = '> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../../../docs/CLOUD-CODE-AGENT-GUIDE.md)'

$dirs = Get-ChildItem (Join-Path $repoRoot ".cursor\skills") -Directory
foreach ($d in $dirs) {
    $path = Join-Path $d.FullName "SKILL.md"
    if (-not (Test-Path $path)) { continue }
    $c = Get-Content $path -Raw -Encoding UTF8
    if ($c -match 'CLOUD-CODE-AGENT-GUIDE') {
        Write-Host "skip $($d.Name)"
        continue
    }
    if ($c -match 'language-enforcement\.md') {
        $c = $c -replace '(language-enforcement\.md[^\r\n]*)', "`$1`r`n$line"
    }
    elseif ($c -match '(?m)^# ') {
        $c = $c -replace '(?m)^(# [^\r\n]+)', "`$1`r`n`r`n$line"
    }
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $c, $utf8)
    Write-Host "updated $($d.Name)"
}

$modDirs = Get-ChildItem (Join-Path $repoRoot ".cursor\skills\claude-fable-5\modules") -Directory -ErrorAction SilentlyContinue
foreach ($d in $modDirs) {
    $path = Join-Path $d.FullName "SKILL.md"
    if (-not (Test-Path $path)) { continue }
    $c = Get-Content $path -Raw -Encoding UTF8
    if ($c -match 'CLOUD-CODE-AGENT-GUIDE') { continue }
    $c = $c -replace '(?m)^(# [^\r\n]+)', "`$1`r`n`r`n$modLine"
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $c, $utf8)
    Write-Host "updated module $($d.Name)"
}

Write-Host "Cloud Code footers done"
