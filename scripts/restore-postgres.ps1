#Requires -Version 5.1
<#
.SYNOPSIS
  استعادة نسخة pg_dump -Fc إلى قاعدة PostgreSQL (محرك التجربة/التعافي).
.NOTES
  استهدف قاعدة فارغة أو مخصصة للاستعادة. لا تعمل على الإنتاج الحي دون خطة صيانة.
#>
param(
    [Parameter(Mandatory = $true)][string]$DumpFile,
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [string]$PgRestorePath = "pg_restore",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $DumpFile)) { throw "Dump file not found: $DumpFile" }

function Get-PgParam([string]$cs, [string]$name) {
    if ($cs -match "(?i)$name=([^;]+)") { return $Matches[1].Trim() }
    return $null
}

$hostName = Get-PgParam $ConnectionString "Host"
$port     = Get-PgParam $ConnectionString "Port"; if (-not $port) { $port = "5432" }
$db       = Get-PgParam $ConnectionString "Database"
$user     = Get-PgParam $ConnectionString "Username"
$pass     = Get-PgParam $ConnectionString "Password"

if (-not $hostName -or -not $db -or -not $user) {
    throw "ConnectionString يجب أن يحتوي Host و Database و Username."
}

$env:PGPASSWORD = $pass
try {
    $args = @("-h", $hostName, "-p", $port, "-U", $user, "-d", $db, "--no-owner", "--no-acl")
    if ($Clean) { $args += "--clean"; $args += "--if-exists" }
    $args += $DumpFile

    Write-Host "Restoring $DumpFile → $db@${hostName}:$port" -ForegroundColor Cyan
    & $PgRestorePath @args
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1) {
        # pg_restore may return 1 for non-fatal warnings
        throw "pg_restore failed with exit $LASTEXITCODE"
    }
    Write-Host "Restore completed. Verify with FullScenarioAudit / smoke login." -ForegroundColor Green
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
