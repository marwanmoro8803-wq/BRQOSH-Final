<#
.SYNOPSIS
  (Phase 7) ZATCA simulation preflight — fully offline. Builds the CSR and every compliance sample
  from your onboarding settings, stamps them with a throw-away self-signed key and verifies them
  locally (CSR fields, certificate template, SAN, hash, signature, QR tags, ICV/PIH chain).
  Nothing is sent to ZATCA and no private key is written to disk.

.DESCRIPTION
  The only step this cannot do is the OTP: generate it on the Fatoora portal on registration and
  put it in Zatca:Onboarding:Otp (appsettings) or BRQOSH_ZATCA_OTP, then call POST /zatca-signer/onboard.

  Optional: if the ZATCA e-invoicing SDK CLI ("fatoora") is on PATH, each sample is also run
  through "fatoora -validate". Its signature/certificate rules will flag the local self-signed
  certificate (expected); the schema and business-rule results are what matter here.

.EXAMPLE
  ./scripts/zatca-simulation-preflight.ps1 -Vat 310000000000003 -OrganizationName "My Co" `
     -Branch "Riyadh" -Address "1234 King Fahd Rd, Riyadh" -BusinessCategory "Trading"
#>
param(
  [string]$Environment = "simulation",
  [Parameter(Mandatory = $true)][string]$Vat,
  [Parameter(Mandatory = $true)][string]$OrganizationName,
  [Parameter(Mandatory = $true)][string]$Address,
  [Parameter(Mandatory = $true)][string]$BusinessCategory,
  [string]$Branch = "",
  [string]$Cr = "",
  [string]$InvoiceType = "1100",
  [string]$SolutionName = "BRQOSH-ERP",
  [string]$OutputDir = (Join-Path ([IO.Path]::GetTempPath()) ("brqosh-zatca-preflight-" + (Get-Date -Format "yyyyMMdd-HHmmss")))
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

$env:BRQOSH_ZATCA_ENVIRONMENT = $Environment
$env:BRQOSH_ZATCA_VAT = $Vat
$env:BRQOSH_ZATCA_ORGANIZATION_NAME = $OrganizationName
$env:BRQOSH_ZATCA_ADDRESS = $Address
$env:BRQOSH_ZATCA_BUSINESS_CATEGORY = $BusinessCategory
$env:BRQOSH_ZATCA_BRANCH = $Branch
$env:BRQOSH_ZATCA_CR = $Cr
$env:BRQOSH_ZATCA_INVOICE_TYPE = $InvoiceType
$env:BRQOSH_ZATCA_SOLUTION_NAME = $SolutionName

dotnet run --project (Join-Path $repo "tools/ZatcaPreflight") -c Release -- $OutputDir
$code = $LASTEXITCODE

$fatoora = Get-Command fatoora -ErrorAction SilentlyContinue
if ($fatoora -and $code -eq 0) {
  Write-Host "`nZATCA SDK found — validating samples (signature/cert findings are expected with the local certificate):"
  Get-ChildItem $OutputDir -Filter *.xml | ForEach-Object {
    Write-Host "== $($_.Name)"
    & $fatoora.Source -validate -invoice $_.FullName
  }
}
exit $code
