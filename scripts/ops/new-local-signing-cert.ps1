#Requires -Version 7
# (Phase 7 · توقيعٌ بشهادةٍ محلّيّة ذاتيّة — بلا شراء) ‏يُشغَّل مرّةً على جهاز المالك (ويندوز).
# ‏ينشئ شهادةَ توقيع كودٍ «CN=BRQOSH ERP» في Cert:\CurrentUser\My — **المفتاحُ الخاصّ يبقى في مخزن ويندوز غيرَ قابلٍ للتصدير**،
# ‏فلا يقع في المستودع ولا في أيّ ملفّ. يُصدَّر الجزءُ العامّ فقط (.cer) إلى مجلّدٍ خارج المستودع ليُثبَّت عند العملاء إن أرادوا الثقة.
# ‏بعدها يلتقطها scripts/ops/complete-local-verification.ps1 تلقائيّاً (أو اضبط BRQOSH_SIGNING_THUMBPRINT) ويوقّع installer/build-msi.ps1
# ‏عبر installer/signing.ps1 (`signtool /sha1`). شهادةٌ ذاتيّةٌ لا تُزيل تحذيرَ SmartScreen عند الغرباء — تُثبت فقط أنّ الملفّ لم يتغيّر بعد التوقيع.
#   pwsh ./scripts/ops/new-local-signing-cert.ps1                  # إنشاء + تصدير .cer + ثقةٌ على هذا الجهاز
#   pwsh ./scripts/ops/new-local-signing-cert.ps1 -NoTrust -Years 3
param(
    [int]$Years = 5,
    [string]$PublicCertDir = (Join-Path $env:USERPROFILE 'BRQOSH-signing'),
    [switch]$NoTrust,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows only (Cert: drive + signtool).' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$full = [IO.Path]::GetFullPath($PublicCertDir)
if ($full.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) { throw "PublicCertDir must be outside the repository: $full" }

$subject = 'CN=BRQOSH ERP'
$existing = @(Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) })
if ($existing.Count -gt 0 -and -not $Force) {
    throw "A valid '$subject' code-signing certificate already exists ($($existing[0].Thumbprint)). Use -Force to create another (complete-local-verification needs exactly one)."
}

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -FriendlyName 'BRQOSH ERP local code signing' `
    -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
    -KeyExportPolicy NonExportable -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears($Years)

New-Item -ItemType Directory -Force -Path $full | Out-Null
$cer = Join-Path $full 'brqosh-erp-codesign.cer'
Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null

if (-not $NoTrust) {
    # ‏ثقةٌ على هذا الجهاز وحده (المستخدم الحاليّ) ليُقرأ التوقيعُ «صالحاً» عند التحقّق.
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\CurrentUser\TrustedPublisher | Out-Null
}

Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Public certificate (share with customers if needed): $cer"
Write-Host 'Private key: Cert:\CurrentUser\My, non-exportable — never in the repo.'
Write-Host "Sign: `$env:BRQOSH_SIGNING_THUMBPRINT='$($cert.Thumbprint)'; pwsh ./scripts/ops/complete-local-verification.ps1"
Write-Host 'Verify: Get-AuthenticodeSignature dist\BRQOSH-Setup.msi | Format-List Status,SignerCertificate'
