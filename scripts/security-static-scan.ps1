# Static security scan for BRQOSH — fails CI on critical findings in src/.
#
# (CEEF-229 . م2-6) WHAT THIS SCRIPT CAN AND CANNOT PROVE — read before adding a check here.
#
# This is a TEXT scanner. It proves a string is present or absent in a file. That is the right
# tool for one job only: finding secrets, because a secret IS text. It is the WRONG tool for
# proving that a guard runs, and it used to claim exactly that:
#
#   * It searched each *PostingService.cs for the substring "EnsureCanPost" and reported PASS.
#     A call commented out, unreachable, or placed AFTER the database write all pass that check.
#     Measured: commenting out the guard in SalesInvoicePostingService.PostAsync left this scan
#     reporting "Posting EnsureCanPost* (soft) | PASS" while an unauthorised caller went through.
#
#   * Its strictest check searches for the literal "admin123". A back door under any other name
#     passes untouched.
#
# Guard ENFORCEMENT is now measured by execution, not by text:
#   tests/Brqosh.Application.Tests/Security/EveryPostingServiceRefusesAViewerTests.cs
#       - reflects over every *PostingService, invokes every public operation as a Viewer,
#         and asserts the refusal is an AUTHORISATION refusal rather than an incidental crash.
#   tests/Brqosh.Api.Tests/{ApiKeyGuardTests,ApiRoleEscalationTests,ProductionApiSecurityStartupTests}.cs
#       - send real requests with and without headers.
#
# So: add SECRET patterns here. Add GUARD checks to those test files instead.
param(
    [string]$SourceRoot = "src",
    [string]$ApiProgram = "src/Brqosh.Api/Program.cs"
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$findings = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) { $failures.Add($Message) }
function Add-Warning([string]$Message) { $warnings.Add($Message) }

Write-Host "== BRQOSH security static scan =="

function Get-SourceFiles {
    Get-ChildItem -Path $SourceRoot -Recurse -File -Include *.cs,*.xaml,*.json,*.csproj |
        Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }
}

# 1) Hard fail: default admin credentials in application source
$sourceFiles = @(Get-SourceFiles)
$adminHits = @(
    $sourceFiles | Select-String -Pattern "admin123" |
        Where-Object { $_.Path -notmatch "PasswordPolicy\.cs$" }
)
if ($adminHits.Count -gt 0) {
    foreach ($hit in $adminHits) {
        Add-Failure "admin123 in $($hit.Path):$($hit.LineNumber)"
    }
}

$adminPairHits = @(
    $sourceFiles | Select-String -Pattern "admin\s*/\s*admin123"
)
if ($adminPairHits.Count -gt 0) {
    foreach ($hit in $adminPairHits) {
        Add-Failure "admin/admin123 in $($hit.Path):$($hit.LineNumber)"
    }
}

# 2) List likely hardcoded secrets (warning / informational)
$passwordPatterns = @(
    'password\s*=\s*["''][^"'']{3,}["'']',
    'Password\s*=\s*["''][^"'']{3,}["'']',
    'api[_-]?key\s*=\s*["''][^"'']{8,}["'']',
    'secret\s*=\s*["''][^"'']{8,}["'']'
)
foreach ($pattern in $passwordPatterns) {
    $hits = @($sourceFiles | Select-String -Pattern $pattern)
    foreach ($hit in $hits) {
        $rel = $hit.Path.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, "")
        $findings.Add("possible secret ($pattern): $rel`:$($hit.LineNumber)")
    }
}

# 3) Posting guards: NOT checked here — see the header. A text search cannot tell a live guard
#    from a commented-out one. EveryPostingServiceRefusesAViewerTests invokes every operation as a
#    Viewer and asserts an authorisation refusal; that test is the gate. What remains useful here is
#    the far weaker signal that a posting service does not even MENTION a guard, which is worth a
#    warning line because it is cheap — but it is reported as what it is, not as a PASS.
$postingServices = @(
    Get-ChildItem -Path (Join-Path $SourceRoot "Brqosh.Infrastructure") -Recurse -Filter "*PostingService.cs" -File
)
$noGuardMention = @()
foreach ($file in $postingServices) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -notmatch "EnsureCanPost") {
        $noGuardMention += $file.Name
    }
}
if ($noGuardMention.Count -gt 0) {
    $guardNote = "Posting services that never mention a guard (text only; enforcement is proven by EveryPostingServiceRefusesAViewerTests): "
    Add-Warning ($guardNote + ($noGuardMention -join ", "))
}

# 4) API key middleware must exist when BRQOSH_API_KEY is configured
if (-not (Test-Path -LiteralPath $ApiProgram)) {
    Add-Failure "API Program.cs not found at $ApiProgram"
}
else {
    $program = Get-Content -LiteralPath $ApiProgram -Raw
    if ($program -notmatch "BRQOSH_API_KEY|ApiSecuritySettings\.ApiKeyEnv") {
        Add-Failure "Program.cs missing BRQOSH_API_KEY configuration"
    }
    if ($program -notmatch "X-Api-Key") {
        Add-Failure "Program.cs missing X-Api-Key header check"
    }
    if ($program -notmatch "EnsureProductionApiKey") {
        Add-Failure "Program.cs missing Production API key enforcement (EnsureProductionApiKey)"
    }
    if ($program -notmatch "SecretComparer") {
        Add-Failure "Program.cs missing timing-safe SecretComparer for API keys"
    }
    if ($program -notmatch "X-Provisioning-Key") {
        Add-Failure "Program.cs missing X-Provisioning-Key guard on tenant provision"
    }
}

$tenantGuard = Join-Path $SourceRoot "Brqosh.Infrastructure/Companies/TenantGuard.cs"
if (Test-Path -LiteralPath $tenantGuard) {
    $tg = Get-Content -LiteralPath $tenantGuard -Raw
    if ($tg -notmatch "EnforceTenantIsolation") {
        Add-Failure "TenantGuard missing SaaS default-deny (EnforceTenantIsolation)"
    }
}

$webhookValidator = Join-Path $SourceRoot "Brqosh.Infrastructure/Webhooks/WebhookUrlValidator.cs"
if (-not (Test-Path -LiteralPath $webhookValidator)) {
    Add-Failure "Missing WebhookUrlValidator.cs (SSRF guard)"
}

$apiKeyPass = -not ($failures | Where-Object { $_ -match "Program\.cs" })
$summary = @"
## Security static scan

This scan proves the presence or absence of TEXT. Guard ENFORCEMENT is proven by execution in
EveryPostingServiceRefusesAViewerTests and the Brqosh.Api.Tests security suite - see the header.

| Check | What it proves | Result |
|-------|----------------|--------|
| admin123 in src/ | that one literal is absent | $(if ($adminHits.Count -eq 0 -and $adminPairHits.Count -eq 0) { 'PASS' } else { 'FAIL' }) |
| Program.cs security wiring | the strings are present, not that middleware runs | $(if ($apiKeyPass) { 'present' } else { 'MISSING' }) |
| Posting guard mentioned | text only, not that the guard executes | $(if ($noGuardMention.Count -eq 0) { 'all mention one' } else { "$($noGuardMention.Count) service(s) flagged" }) |
| Possible hardcoded secrets | pattern matches to review | $($findings.Count) match(es) listed below |

### Failures ($($failures.Count))
$(if ($failures.Count -eq 0) { '- none' } else { ($failures | ForEach-Object { "- $_" }) -join "`n" })

### Warnings ($($warnings.Count))
$(if ($warnings.Count -eq 0) { '- none' } else { ($warnings | ForEach-Object { "- $_" }) -join "`n" })

### Possible secrets (informational)
$(if ($findings.Count -eq 0) { '- none' } else { ($findings | Select-Object -First 20 | ForEach-Object { "- $_" }) -join "`n" })
"@

Write-Host $summary

if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}

if ($failures.Count -gt 0) {
    Write-Host "SECURITY SCAN FAILED with $($failures.Count) critical finding(s)."
    exit 1
}

Write-Host "SECURITY SCAN PASSED ($($warnings.Count) warning(s), $($findings.Count) informational match(es))."
exit 0
