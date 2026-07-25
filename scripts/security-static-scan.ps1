# Static security scan for BRQOSH — fails CI on critical findings in src/.
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

# 3) Soft: posting services should call EnsureCanPost*
$postingServices = @(
    Get-ChildItem -Path (Join-Path $SourceRoot "Brqosh.Infrastructure") -Recurse -Filter "*PostingService.cs" -File
)
$missingGuard = @()
foreach ($file in $postingServices) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -notmatch "EnsureCanPost") {
        $missingGuard += $file.Name
    }
}
if ($missingGuard.Count -gt 0) {
    Add-Warning ("Posting services without EnsureCanPost*: " + ($missingGuard -join ", "))
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

| Check | Result |
|-------|--------|
| admin123 in src/ | $(if ($adminHits.Count -eq 0 -and $adminPairHits.Count -eq 0) { 'PASS' } else { 'FAIL' }) |
| API key guard (Program.cs) | $(if ($apiKeyPass) { 'PASS' } else { 'FAIL' }) |
| Posting EnsureCanPost* (soft) | $(if ($missingGuard.Count -eq 0) { 'PASS' } else { "$($missingGuard.Count) service(s) flagged" }) |
| Possible hardcoded secrets | $($findings.Count) match(es) listed below |

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
