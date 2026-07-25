# Validate merged_claude_files.md index and synthesis modules
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$skillsRoot = Join-Path $repoRoot ".cursor\skills"

$required = @(
    "brqosh-skills-hub\merged-claude-index.yaml",
    "supreme-programming-mastery\claude-code-cli.md",
    "omni-qa-army\cloud-code-desktop.md",
    "ai-dev-platform\cognitiveqa-module.md",
    "claude-fable-5\cursor-adaptations.md"
)

$failed = 0
foreach ($rel in $required) {
    $full = Join-Path $skillsRoot $rel
    if (Test-Path $full) {
        Write-Host "[PASS] $rel" -ForegroundColor DarkGreen
    }
    else {
        Write-Host "[FAIL] $rel" -ForegroundColor Red
        $failed++
    }
}

$raw = Join-Path $skillsRoot "raw\merged_claude.md"
if (Test-Path $raw) {
    $lines = (Get-Content $raw | Measure-Object -Line).Lines
    Write-Host "[PASS] raw/merged_claude.md ($lines lines)" -ForegroundColor DarkGreen
}
elseif (Test-Path (Join-Path $repoRoot "merged_claude_files.md")) {
    $lines = (Get-Content (Join-Path $repoRoot "merged_claude_files.md") | Measure-Object -Line).Lines
    Write-Host "[PASS] merged_claude_files.md legacy ($lines lines)" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] merged_claude raw missing" -ForegroundColor Red
    $failed++
}

$guide = Join-Path $repoRoot "docs\CLOUD-CODE-AGENT-GUIDE.md"
if (Test-Path $guide) {
    Write-Host "[PASS] docs/CLOUD-CODE-AGENT-GUIDE.md" -ForegroundColor DarkGreen
}
else {
    Write-Host "[FAIL] agent guide missing" -ForegroundColor Red
    $failed++
}

if ($failed -gt 0) {
    Write-Error "Merged Claude validation failed: $failed"
    exit 1
}

Write-Host "Merged Claude validation PASS"
exit 0
