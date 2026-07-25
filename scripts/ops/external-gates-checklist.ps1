# External gate preparation — O-01…O-05 (does NOT replace live execution).
# Prints actionable checklist with repo script entry points.

$ErrorActionPreference = "Continue"

Write-Host "=== BRQOSH External Gates Checklist (O-01 to O-05) ==="
Write-Host ""

Write-Host "[O-01] ZATCA Production OTP + 10 accepted invoices"
Write-Host "  - docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md"
Write-Host "  - docs/ZATCA_LIVE_CSID.md"
Write-Host "  - Desktop: Zatca Settings > CSID Wizard (live OTP from authority)"
Write-Host "  - Evidence: 10 production invoice UUIDs + acceptance screenshots"
Write-Host ""

Write-Host "[O-02] SOCPA third-party review"
Write-Host "  - docs/ops/O-02_SOCPA_REVIEWER_PACKAGE.md"
Write-Host "  - docs/SECURITY_CHECKLIST.md + accounting scenario pack"
Write-Host "  - Engage independent reviewer; attach signed report"
Write-Host ""

Write-Host "[O-03] MSI on clean VM + field signing"
Write-Host "  - docs/ops/O-03_MSI_VM_RUNBOOK.md"
Write-Host "  - scripts/ops/verify-msi-build.ps1"
Write-Host "  - VM: install MSI, run FullScenarioAudit, capture screenshot"
Write-Host ""

Write-Host "[O-04] Load test 1M journal lines"
Write-Host "  - docs/ops/O-04_STRESS_LOAD_RUNBOOK.md"
Write-Host "  - scripts/ops/run-stress-benchmark.ps1"
Write-Host ""

Write-Host "[O-05] OWASP ZAP live pen test"
Write-Host "  - scripts/ops/run-zap-baseline.ps1 (Docker + staging URL)"
Write-Host "  - scripts/security-static-scan.ps1 (static PASS in CI)"
Write-Host "  - Optional tenant header: X-Tenant-Id: <company-tenant-guid>"
Write-Host ""

Write-Host "=== End checklist - items remain EXTERNAL until evidence captured ==="
