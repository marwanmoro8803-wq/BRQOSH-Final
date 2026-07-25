# v14 skills consolidation — remove redundant stubs and duplicate layers
$ErrorActionPreference = "Stop"
$SkillsBase = (Resolve-Path (Join-Path $PSScriptRoot "..\..\.cursor\skills")).Path
$removed = 0

$toRemove = @(
    "brqosh-skills-hub\MASTER-ROADMAP.md",
    "العقل-الذكي\claude-fable-5.md",
    "العقل-الذكي\ROADMAP.md"
)

Get-ChildItem -Path $SkillsBase -Recurse -Filter "ROADMAP.md" | ForEach-Object {
    $toRemove += $_.FullName.Substring($SkillsBase.Length + 1)
}

foreach ($rel in $toRemove) {
    $p = Join-Path $SkillsBase $rel
    if (Test-Path $p) {
        Remove-Item $p -Force
        $removed++
        Write-Host "removed: $rel"
    }
}

Write-Host ""
Write-Host "Removed $removed redundant files. Active contract: SKILL.md#engineering-roadmap only."
