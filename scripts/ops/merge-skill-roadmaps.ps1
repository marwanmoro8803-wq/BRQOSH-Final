# Merge ROADMAP.md into SKILL.md — single canonical skill file per package
$ErrorActionPreference = "Stop"
$SkillsBase = (Resolve-Path (Join-Path $PSScriptRoot "..\..\.cursor\skills")).Path
$merged = 0
$stub = 0

function Merge-RoadmapIntoSkill {
    param([string]$SkillDir)
    $skillPath = Join-Path $SkillDir "SKILL.md"
    $roadmapPath = Join-Path $SkillDir "ROADMAP.md"
    if (-not (Test-Path $skillPath)) { return }
    if (-not (Test-Path $roadmapPath)) { return }

    $skill = Get-Content $skillPath -Raw -Encoding UTF8
    if ($skill -match "## Engineering Roadmap") {
        Write-Host "  skip (already merged): $($SkillDir | Split-Path -Leaf)"
        return
    }

    $roadmap = Get-Content $roadmapPath -Raw -Encoding UTF8
    # Strip leading H1 title line from roadmap body
    $roadmapBody = $roadmap -replace "^#\s+ROADMAP[^\r\n]*\r?\n", ""
    $roadmapBody = $roadmapBody.TrimStart()

    # Remove standalone roadmap link from skill header
    $skill = $skill -replace "(?m)^> \*\*Roadmap:\*\* \[ROADMAP\.md\]\(ROADMAP\.md\)[^\r\n]*\r?\n", ""
    $skill = $skill -replace "(?m)^> \*\*Roadmap:\*\* \[ROADMAP\.md\]\(ROADMAP\.md\)\s*\r?\n", ""

    # Cross-skill links: other skills' ROADMAP.md -> SKILL.md#engineering-roadmap
    $roadmapBody = $roadmapBody -replace "\]\(\.\./([^/)]+)/ROADMAP\.md\)", "](../`$1/SKILL.md#engineering-roadmap)"
    $roadmapBody = $roadmapBody -replace "\]\(ROADMAP\.md\)", "](SKILL.md#engineering-roadmap)"

    $skill = $skill.TrimEnd() + "`n`n---`n`n## Engineering Roadmap`n`n" + $roadmapBody + "`n"
    [System.IO.File]::WriteAllText($skillPath, $skill, [System.Text.UTF8Encoding]::new($false))

    $stubContent = @"
# Merged into SKILL.md

> **Canonical roadmap:** [SKILL.md](SKILL.md#engineering-roadmap)

This file is a redirect stub. All load-order contracts live in **SKILL.md**.
"@
    [System.IO.File]::WriteAllText($roadmapPath, $stubContent.TrimEnd() + "`n", [System.Text.UTF8Encoding]::new($false))
    $script:merged++
    $script:stub++
    Write-Host "  merged: $($SkillDir | Split-Path -Leaf)"
}

Get-ChildItem -Path $SkillsBase -Directory | ForEach-Object {
    Merge-RoadmapIntoSkill -SkillDir $_.FullName
}

# Hub: merge MASTER-ROADMAP into brqosh-skills-hub/SKILL.md
$hubDir = Join-Path $SkillsBase "brqosh-skills-hub"
$hubSkill = Join-Path $hubDir "SKILL.md"
$masterRoadmap = Join-Path $hubDir "MASTER-ROADMAP.md"
if ((Test-Path $hubSkill) -and (Test-Path $masterRoadmap)) {
    $skill = Get-Content $hubSkill -Raw -Encoding UTF8
    if ($skill -notmatch "## Engineering Roadmap") {
        $master = Get-Content $masterRoadmap -Raw -Encoding UTF8
        $masterBody = $master -replace "^#\s+MASTER ROADMAP[^\r\n]*\r?\n", ""
        $masterBody = $masterBody.TrimStart()
        $masterBody = $masterBody -replace "\]\(\.\./([^/)]+)/ROADMAP\.md\)", "](../`$1/SKILL.md#engineering-roadmap)"
        $skill = $skill.TrimEnd() + "`n`n---`n`n## Engineering Roadmap`n`n" + $masterBody + "`n"
        [System.IO.File]::WriteAllText($hubSkill, $skill, [System.Text.UTF8Encoding]::new($false))
        $stubMaster = @"
# Merged into SKILL.md

> **Canonical master roadmap:** [SKILL.md](SKILL.md#engineering-roadmap)

Hub load-order index lives in **brqosh-skills-hub/SKILL.md**.
"@
        [System.IO.File]::WriteAllText($masterRoadmap, $stubMaster.TrimEnd() + "`n", [System.Text.UTF8Encoding]::new($false))
        Write-Host "  merged hub: brqosh-skills-hub (MASTER-ROADMAP -> SKILL)"
        $merged++
    }
}

# Update reference file headers: ROADMAP.md -> SKILL.md#engineering-roadmap
$refCount = 0
Get-ChildItem -Path $SkillsBase -Recurse -Filter "*.md" | Where-Object {
    $rel = $_.FullName.Substring($SkillsBase.Length + 1)
    if ($_.Name -in @("SKILL.md", "ROADMAP.md", "MASTER-ROADMAP.md", "claude-fable-5.md")) { return $false }
    if ($rel -match "archive\\") { return $false }
    if ($rel -match "brqosh-ui-design-system\\") { return $false }
    if ($rel -match "Anthropic\\") { return $false }
    $true
} | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -notmatch "\*\*Roadmap:\*\*") { return }
    $newContent = $content -replace "\[ROADMAP\.md\]\(ROADMAP\.md\)", "[SKILL.md](SKILL.md#engineering-roadmap)"
    if ($newContent -ne $content) {
        [System.IO.File]::WriteAllText($_.FullName, $newContent, [System.Text.UTF8Encoding]::new($false))
        $refCount++
    }
}

Write-Host ""
Write-Host "Done: $merged skill packages merged, $refCount reference headers updated"
