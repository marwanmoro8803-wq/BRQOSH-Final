# OWASP ZAP baseline — O-05 preparation (requires Docker + staging API URL).
# Does NOT claim O-05 closed without executed scan report.

param(
    [string]$TargetUrl = $env:BRQOSH_ZAP_TARGET_URL,
    [string]$ApiKey = $env:BRQOSH_API_KEY,
    [string]$TenantId = $env:BRQOSH_TENANT_ID,
    [string]$ReportDir = "dist/zap-reports"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "=== BRQOSH ZAP Baseline (O-05 prep) ===" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace($TargetUrl)) {
    Write-Host "Set BRQOSH_ZAP_TARGET_URL (e.g. https://staging.example.com)" -ForegroundColor Yellow
    Write-Host "Optional: BRQOSH_API_KEY, BRQOSH_TENANT_ID for authenticated scan" -ForegroundColor Yellow
    exit 2
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Docker required for OWASP ZAP baseline image." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null
$reportHtml = Join-Path $ReportDir "zap-baseline-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"

$dockerArgs = @(
    "run", "--rm",
    "-v", "${root}/${ReportDir}:/zap/wrk:rw",
    "ghcr.io/zaproxy/zaproxy:stable",
    "zap-baseline.py",
    "-t", $TargetUrl,
    "-r", (Split-Path -Leaf $reportHtml),
    "-I"
)

if ($ApiKey) {
    $dockerArgs += @("-z", "apikey=$ApiKey")
}

Write-Host "Target: $TargetUrl"
Write-Host "Report: $reportHtml"

& docker @dockerArgs
$exit = $LASTEXITCODE

if ($exit -eq 0) {
    Write-Host "ZAP baseline completed — review HTML report." -ForegroundColor Green
} else {
    Write-Host "ZAP finished with findings or errors (exit $exit) — review report." -ForegroundColor Yellow
}

Write-Host "O-05 remains EXTERNAL until signed pen-test evidence is archived."
exit $exit
