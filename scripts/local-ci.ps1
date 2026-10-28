#Requires -Version 7
# (Phase 7 · بديلُ الـCI الخارجيّ) ‏بناءٌ واختبارٌ محلّيّان على جهاز المالك — لا خدمةَ خارجيّة، لا حساب، لا شراء.
# ‏restore → build (Release) بصفر أخطاء → Domain/Application/Api/Desktop كلٌّ على حدة بسجلّ TRX → ملخّص → رمزُ خروجٍ غير صفريّ عند أيّ فشل.
# ‏اختباراتُ PostgreSQL تعمل إن مُرِّرت -PgPort (وإلّا تُتخطّى كما في تعريف [PostgresFact]). لا تلمس قاعدةً حقيقيّة: استعمل خادماً تجريبيّاً.
#   pwsh ./scripts/local-ci.ps1
#   pwsh ./scripts/local-ci.ps1 -PgPort 5433 -PgPassword x -Only Domain,Application
param(
    [string[]]$Only = @('Domain', 'Application', 'Api', 'Desktop'),
    [switch]$NoRestore,
    [int]$PgPort = 0,
    [string]$PgHost = '127.0.0.1',
    [string]$PgPassword = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $root "artifacts/local-ci/$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$watch = [Diagnostics.Stopwatch]::StartNew()
Push-Location $root
try {
    if ($PgPort -gt 0) {
        $env:BRQOSH_PG_HOST = $PgHost; $env:BRQOSH_PG_PORT = "$PgPort"; $env:BRQOSH_PG_PASSWORD = $PgPassword
    }
    if (-not $NoRestore) {
        & dotnet restore Brqosh.sln -nologo | Tee-Object -FilePath (Join-Path $out 'restore.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'restore failed — see restore.log' }
    }
    $buildLog = Join-Path $out 'build.log'
    & dotnet build Brqosh.sln -c Release --no-restore -nologo -clp:ErrorsOnly | Tee-Object -FilePath $buildLog
    $errors = @(Select-String -Path $buildLog -Pattern ': error ' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0 -or $errors.Count -gt 0) { throw "build failed with $($errors.Count) error(s) — see $buildLog" }

    $rows = @()
    foreach ($name in $Only) {
        $proj = "tests/Brqosh.$name.Tests"
        if (-not (Test-Path $proj)) { throw "unknown test project: $proj" }
        $log = Join-Path $out "$name.log"
        & dotnet test $proj -c Release --no-build -nologo --logger "trx;LogFileName=$name.trx" --results-directory $out | Tee-Object -FilePath $log | Out-Null
        $code = $LASTEXITCODE
        $line = Select-String -Path $log -Pattern '(Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)' | Select-Object -Last 1
        if ($line) {
            $m = $line.Matches[0].Groups
            $rows += [pscustomobject]@{ Project = $name; Failed = [int]$m[2].Value; Passed = [int]$m[3].Value; Skipped = [int]$m[4].Value; Total = [int]$m[5].Value; Exit = $code }
        } else {
            $rows += [pscustomobject]@{ Project = $name; Failed = -1; Passed = 0; Skipped = 0; Total = 0; Exit = $code }
        }
    }
    $rows | Format-Table -AutoSize | Out-String | Tee-Object -FilePath (Join-Path $out 'summary.txt')
    $bad = @($rows | Where-Object { $_.Exit -ne 0 -or $_.Failed -ne 0 -or $_.Total -eq 0 })
    "elapsed: {0:mm\:ss} · results: $out" -f $watch.Elapsed | Write-Host
    if ($bad.Count -gt 0) {
        Write-Host "FAILED: $($bad.Project -join ', ') — see *.log / *.trx" -ForegroundColor Red
        exit 1
    }
    Write-Host 'OK: 0 build errors, all selected test projects green.' -ForegroundColor Green
    exit 0
} finally {
    Pop-Location
}
