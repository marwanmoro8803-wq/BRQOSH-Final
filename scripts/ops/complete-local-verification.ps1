#Requires -Version 7
param([switch]$NoRestore, [switch]$AllowUnsigned)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$oldEnv = $env:BRQOSH_ENV
$oldRoot = $env:BRQOSH_DATA_ROOT
Push-Location $ProjectRoot
try {
    $env:BRQOSH_ENV = 'development'
    $env:BRQOSH_DATA_ROOT = Join-Path ([IO.Path]::GetTempPath()) ('brqosh-final-check-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $env:BRQOSH_DATA_ROOT -Force | Out-Null
    if (-not $NoRestore) {
        & dotnet restore Brqosh.sln -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    }
    & dotnet test Brqosh.sln -c Release --no-restore -m:1 -nr:false --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. No installer will be released.' }
    if (-not $env:BRQOSH_SIGNING_THUMBPRINT -and -not $env:BRQOSH_SIGNING_PFX) {
        $certs = @(Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object {
            $_.Subject -eq 'CN=BRQOSH ERP' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date)
        })
        if ($certs.Count -eq 1) {
            $env:BRQOSH_SIGNING_THUMBPRINT = $certs[0].Thumbprint
            if ($certs[0].Issuer -eq $certs[0].Subject) { $env:BRQOSH_SIGNING_SELFSIGNED = '1' }
        } elseif (-not $AllowUnsigned) {
            throw 'No unique existing BRQOSH code-signing certificate. Configure signing, or explicitly use -AllowUnsigned for a review build.'
        }
    }
    $number = [int](Get-Content version.txt -Raw).Trim()
    & ./installer/build-msi.ps1 -BuildNumber $number -NoRestore:$NoRestore
    if ($LASTEXITCODE -ne 0) { throw 'Installer build/validation failed. See dist/ice-validation.txt.' }
    Write-Host 'Build and automated checks passed. Live GUI acceptance remains required.' -ForegroundColor Green
    Write-Host (Join-Path $ProjectRoot 'dist\BRQOSH-Setup.msi')
} finally {
    $env:BRQOSH_ENV = $oldEnv
    $env:BRQOSH_DATA_ROOT = $oldRoot
    Pop-Location
}
