# Normalize roadmap links after merge into SKILL.md
$ErrorActionPreference = "Stop"
$SkillsBase = (Resolve-Path (Join-Path $PSScriptRoot "..\..\.cursor\skills")).Path
$count = 0

Get-ChildItem -Path $SkillsBase -Recurse -Include "*.md", "*.yaml" | Where-Object {
    $rel = $_.FullName.Substring($SkillsBase.Length + 1)
    if ($rel -match "archive\\") { return $false }
    if ($rel -match "brqosh-ui-design-system\\") { return $false }
    if ($rel -match "Anthropic\\") { return $false }
    $true
} | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    $orig = $content
    # Link text ROADMAP.md -> SKILL.md where target is engineering-roadmap
    $content = $content -replace '\[ROADMAP\.md\]\(([^)]*SKILL\.md#engineering-roadmap)\)', '[SKILL.md]($1)'
    $content = $content -replace '\[([^]]*)/ROADMAP\.md\]\(([^)]*SKILL\.md#engineering-roadmap)\)', '[$1/SKILL.md]($2)'
  # Hub master index -> embedded in hub SKILL
    $content = $content -replace '\[MASTER-ROADMAP\.md\]\(\.\./brqosh-skills-hub/MASTER-ROADMAP\.md\)', '[SKILL.md](../brqosh-skills-hub/SKILL.md#engineering-roadmap)'
    $content = $content -replace '\[MASTER-ROADMAP\.md\]\(MASTER-ROADMAP\.md\)', '[SKILL.md](SKILL.md#engineering-roadmap)'
    $content = $content -replace 'MASTER-ROADMAP\.md \(BRQOSH\)', 'brqosh-skills-hub/SKILL.md#engineering-roadmap (BRQOSH)'
    $content = $content -replace 'supreme-programming-mastery/ROADMAP\.md', 'supreme-programming-mastery/SKILL.md#engineering-roadmap'
    $content = $content -replace '/ROADMAP\.md#', '/SKILL.md#engineering-roadmap#'
    $content = $content -replace 'MASTER-ROADMAP or target skill ROADMAP', 'hub SKILL#engineering-roadmap or target skill SKILL#engineering-roadmap'
    if ($content -ne $orig) {
        [System.IO.File]::WriteAllText($_.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        $count++
    }
}

Write-Host "Normalized links in $count files"
