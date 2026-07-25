# Inject roadmap headers into skill reference files (post-merge: SKILL.md#engineering-roadmap)
$ErrorActionPreference = "Stop"
$skillRoot = Join-Path $PSScriptRoot "..\..\.cursor\skills" | Resolve-Path
$skip = @("SKILL.md", "ROADMAP.md", "MASTER-ROADMAP.md", "claude-fable-5.md")
$count = 0

Get-ChildItem -Path $skillRoot -Recurse -Filter "*.md" | Where-Object {
    $rel = $_.FullName.Substring($skillRoot.Path.Length + 1)
    if ($skip -contains $_.Name) { return $false }
    if ($rel -match "archive\\") { return $false }
    if ($rel -match "brqosh-ui-design-system\\") { return $false }
    if ($rel -match "Anthropic\\") { return $false }
    $dir = $_.Directory.FullName
    Test-Path (Join-Path $dir "SKILL.md")
} | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match "\*\*Roadmap:\*\*") { return }
    $skillDir = $_.Directory.Name
    $roadmapLine = "`n> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: $skillDir | see per-file load order`n"
    if ($content -match "^(# .+?\r?\n)") {
        $newContent = $content -replace "^(# .+?\r?\n)", ('$1' + $roadmapLine)
    } else {
        $newContent = $roadmapLine + $content
    }
    [System.IO.File]::WriteAllText($_.FullName, $newContent, [System.Text.UTF8Encoding]::new($false))
    $count++
}

Write-Host "Injected roadmap header into $count files"
