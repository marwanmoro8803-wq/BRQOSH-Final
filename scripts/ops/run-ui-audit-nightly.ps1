# Live FlaUI audit -- nightly or pre-release
# Requires: Windows, PostgreSQL/SQLite configured
# Password: BRQOSH_TEST_PASSWORD env, or FullScenarioAudit sync (in-repo default)
param(
    [string]$Username = "marwan",
    [switch]$SkipPasswordSync
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$root\Brqosh.sln")) { $root = Split-Path -Parent $PSScriptRoot }

if (-not $SkipPasswordSync) {
    Write-Host "[prep] Sync marwan password via FullScenarioAudit..." -ForegroundColor Yellow
    $env:BRQOSH_TEST = "1"
    $env:BRQOSH_TEST_USERNAME = $Username
    $fsa = Join-Path $root "tools\FullScenarioAudit\FullScenarioAudit.csproj"
    dotnet run --project $fsa -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$env:BRQOSH_TEST_USERNAME = $Username
# ‏(م-٥ · ن-٢٢) ‏**لا كلمةَ احتياطيّة.** كان هنا فرعُ `else` بكلمةٍ حرفيّةٍ منشورةٍ في
# ‏المستودع، فتشغيلةٌ ليليّةٌ بلا متغيّرٍ تزرع حساباً مفتوحاً لكلّ من قرأ الشيفرة.
if (-not $env:BRQOSH_TEST_PASSWORD) {
    Write-Error "‏BRQOSH_TEST_PASSWORD غيرُ مضبوط — ولا كلمةَ احتياطيّةَ هنا عمداً. اضبطه لهذه التشغيلة."
    exit 2
}
$env:BRQOSH_AUDIT_ALL_PROFILES = "1"
$env:BRQOSH_UIAUDIT_TIMEOUT_MS = "240000"
# (CEEF-229 · م2-٤) حُذف التخطّي: تشغيلُ هذا النصّ مرشَّحٌ على تدقيق الواجهة، فلا تبلغه
# بوّابتا الأداء أصلاً — ووجودُ المتغيّر يوحي بإقصاءٍ مقصود لا وجود له.
$env:BRQOSH_TEST = "1"

Write-Host "[prep] Build Desktop Release..." -ForegroundColor Yellow
dotnet build (Join-Path $root "src\Brqosh.Desktop\Brqosh.Desktop.csproj") -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$evidenceDir = Join-Path $root "dist\evidence\live-ui-audit"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$logPath = Join-Path $evidenceDir "nightly-$stamp.txt"

$uiAuditDir = Join-Path $root "tools\UiAudit"
Push-Location $uiAuditDir
try {
    dotnet build -c Release -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run -c Release 2>&1 | Tee-Object -FilePath $logPath
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
