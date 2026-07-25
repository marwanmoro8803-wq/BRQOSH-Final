#Requires -Version 5.1
<#
.SYNOPSIS
  نسخة احتياطية مخصصة (custom format) لقاعدة PostgreSQL الخاصة بـ BRQOSH.
.PARAMETER ConnectionString
  سلسلة اتصال Npgsql (Host=;Port=;Database=;Username=;Password=).
.PARAMETER OutputDir
  مجلد الحفظ (افتراضي: .\backups).
.PARAMETER PgDumpPath
  مسار pg_dump إن لم يكن في PATH.
#>
param(
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [string]$OutputDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "backups"),
    [string]$PgDumpPath = "pg_dump"
)

$ErrorActionPreference = "Stop"

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

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFile = Join-Path $OutputDir "brqosh_${db}_$stamp.dump"

$env:PGPASSWORD = $pass
try {
    Write-Host "Backing up $db@$hostName:$port → $outFile" -ForegroundColor Cyan
    & $PgDumpPath -h $hostName -p $port -U $user -d $db -Fc -f $outFile
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit $LASTEXITCODE" }
    Write-Host "OK: $outFile ($([math]::Round((Get-Item $outFile).Length/1MB, 2)) MB)" -ForegroundColor Green
    Write-Host "RTO target: restore < 4h — drill monthly on a staging database." -ForegroundColor Yellow
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
