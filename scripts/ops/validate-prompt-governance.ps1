# Validate prompt-source-law PS01–PS06
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$fail = 0

function Assert($cond, $msg) {
    if (-not $cond) {
        Write-Host "FAIL: $msg" -ForegroundColor Red
        $script:fail++
    } else {
        Write-Host "OK: $msg" -ForegroundColor Green
    }
}

# PS04 — prompt-source-law exists
$law = ".cursor/skills/omni-qa-army/prompt-source-law.md"
Assert (Test-Path $law) "PS law file exists"

# PS01 — skills-only vs implementation active
$lawText = Get-Content $law -Raw -ErrorAction SilentlyContinue
$released = $lawText -match "PS01-RELEASED"
if ($released) {
    Assert (Test-Path "cognitive-qa/cognitive_qa/audit_orchestrator.py") "PS01-RELEASED cognitive-qa orchestrator"
} else {
    Assert (-not (Test-Path "cognitive-qa")) "PS03 cognitive-qa absent (skills-only)"
}

# PS06 — PAE exists
Assert (Test-Path "PromptAuditEngine/PromptAuditEngine.csproj") "PS06 PromptAuditEngine present"

# P2 modules
@('chaos-exploration','empathetic-ux','deep-audit','forensic-resilience','chronicle-protocol') | ForEach-Object {
    Assert (Test-Path ".cursor/skills/omni-qa-army/$_.md") "P2 module $_"
}

# P3 modules
@('launcher-architecture','orchestrator-law') | ForEach-Object {
    Assert (Test-Path ".cursor/skills/omni-qa-army/$_.md") "P3 module $_"
}

# 10 roles
$roles = Get-ChildItem ".cursor/skills/omni-qa-army/roles/role-*.md" -ErrorAction SilentlyContinue
Assert ($roles.Count -ge 10) "P1 ten roles (count=$($roles.Count))"

if ($fail -gt 0) {
    Write-Host "`nvalidate-prompt-governance: $fail failure(s)" -ForegroundColor Red
    exit 1
}

Write-Host "`nvalidate-prompt-governance: PASS" -ForegroundColor Cyan
exit 0
