#Requires -Version 7.0
<#
.SYNOPSIS
  (م-١ · ن-٠١ · ن-٤٠) الادّعاءُ الرابع: **ترقيةٌ ناجحةٌ فوق تنصيبٍ قائم.**

.DESCRIPTION
  «‏بناءٌ أخضرُ ≠ حزمةٌ صحيحة ≠ تنصيبٌ ناجحٌ على آلةٍ نظيفة ≠ **ترقيةٌ ناجحة**» —
  أربعةُ ادّعاءاتٍ ولكلٍّ دليلُه. والثلاثةُ الأولى مُثبَتةٌ في هذا المستودع؛ والرابعُ
  لم يُثبت قطّ: كلُّ أدلّة ر-ك على **آلةٍ نظيفة**، وعُدّتُها تمسح قبل التنصيب عمداً.

  وهذا السكربتُ يُثبت ما يمكن إثباتُه **بلا تنصيبٍ حيّ** — بقراءة ما تقوله الحزمتان عن
  نفسيهما. وما لا يُثبَت هنا يُعلَن صراحةً في المخرج، فلا يُقرأ نجاحُه أوسعَ ممّا هو.

  يُقاس على حزمتين تُبنيان من المصدر نفسِه برقمَي إصدارٍ مختلفين:

    ١) `UpgradeCode` واحدٌ في الحزمتين      ⟸ وإلّا فهما منتجان لا إصداران
    ٢) `ProductCode` مختلفٌ                 ⟸ وإلّا فهو تحديثٌ صغيرٌ لا ترقيةٌ كبرى
    ٣) `ProductVersion` مختلفٌ في الحقول الثلاثة الأولى ⟸ الرابعُ يُهمله المُثبِّت
    ٤) صفُّ الترقية في (ب) **يشمل** إصدار (أ) ⟸ وهو العطلُ الذي كان: يستثنيه
    ٥) صفرُ أخطاء ICE في الحزمتين           ⟸ عدُّ المراجع سليم

.PARAMETER PackageA
  الحزمةُ المُنصَّبةُ افتراضاً (الأقدم).

.PARAMETER PackageB
  الحزمةُ التي تُثبَّت فوقها (الأحدث).

.EXAMPLE
  ./scripts/ops/verify-upgrade-path.ps1 -PackageA dist\v1.msi -PackageB dist\v2.msi
#>
param(
    [Parameter(Mandatory)][string]$PackageA,
    [Parameter(Mandatory)][string]$PackageB
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Get-MsiFacts([string]$Path) {
    if (-not (Test-Path $Path)) { throw "لا حزمةَ عند $Path" }
    $installer = New-Object -ComObject WindowsInstaller.Installer
    try {
        $db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, [object[]]@([string]$Path, [int]0))
        $facts = @{}
        foreach ($p in 'ProductCode', 'ProductVersion', 'UpgradeCode', 'ProductName') {
            $v = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, [object[]]@("SELECT Value FROM Property WHERE Property='$p'"))
            $v.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $v, $null)
            $r = $v.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $v, $null)
            $facts[$p] = if ($r) { [string]$r.GetType().InvokeMember('StringData', 'GetProperty', $null, $r, [object[]]@([int]1)) } else { $null }
        }
        # ‏صفوفُ جدول الترقية — وهي موضعُ العطل الأصليّ.
        $rows = @()
        $v = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, [object[]]@("SELECT UpgradeCode,VersionMin,VersionMax,Attributes,ActionProperty FROM Upgrade"))
        $v.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $v, $null)
        while ($r = $v.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $v, $null)) {
            $get = { param($i) [string]$r.GetType().InvokeMember('StringData', 'GetProperty', $null, $r, [object[]]@([int]$i)) }
            $rows += [pscustomobject]@{
                UpgradeCode = & $get 1; VersionMin = & $get 2; VersionMax = & $get 3
                Attributes  = [int](& $get 4); ActionProperty = & $get 5
            }
        }
        $facts['Upgrade'] = $rows
        return $facts
    }
    finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($installer) }
}

# ‏المقارنةُ بثلاثة حقولٍ لا أربعة: Windows Installer يُهمل الرابع.
function ConvertTo-Comparable([string]$v) {
    $p = @($v -split '\.')
    while ($p.Count -lt 3) { $p += '0' }
    return [version]($p[0..2] -join '.')
}

function Test-InRange([string]$installed, $row) {
    $iv = ConvertTo-Comparable $installed
    $minInclusive = ($row.Attributes -band 256) -ne 0
    $maxInclusive = ($row.Attributes -band 512) -ne 0
    if ($row.VersionMin) {
        $mn = ConvertTo-Comparable $row.VersionMin
        if ($minInclusive) { if ($iv -lt $mn) { return $false } } else { if ($iv -le $mn) { return $false } }
    }
    if ($row.VersionMax) {
        $mx = ConvertTo-Comparable $row.VersionMax
        if ($maxInclusive) { if ($iv -gt $mx) { return $false } } else { if ($iv -ge $mx) { return $false } }
    }
    return $true
}

function Assert([string]$Name, [bool]$Ok, [string]$Detail) {
    $mark = if ($Ok) { '[✔]' } else { '[✘]'; }
    $color = if ($Ok) { 'Green' } else { 'Red' }
    Write-Host ("  {0} {1,-34} {2}" -f $mark, $Name, $Detail) -ForegroundColor $color
    if (-not $Ok) { $failures.Add($Name) }
}

Write-Host "=== إثباتُ مسار الترقية (قراءةٌ من الحزمتين) ===" -ForegroundColor Cyan
$a = Get-MsiFacts $PackageA
$b = Get-MsiFacts $PackageB
Write-Host ("  أ: $($a.ProductVersion)  →  ب: $($b.ProductVersion)") -ForegroundColor DarkGray
Write-Host ""

Assert 'UpgradeCode واحد' ($a.UpgradeCode -eq $b.UpgradeCode) "$($b.UpgradeCode)"
Assert 'ProductCode مختلف' ($a.ProductCode -ne $b.ProductCode) "أ=$($a.ProductCode.Substring(1,8))… ب=$($b.ProductCode.Substring(1,8))…"

$va = ConvertTo-Comparable $a.ProductVersion
$vb = ConvertTo-Comparable $b.ProductVersion
Assert 'الإصدار يتقدّم (٣ حقول)' ($vb -gt $va) "$va → $vb"

# ‏لبُّ الأمر: هل يرى صفُّ الترقيةِ في (ب) إصدارَ (أ)؟
$detect = @($b.Upgrade | Where-Object { $_.ActionProperty -notmatch 'DOWNGRADE' -and ($_.Attributes -band 2) -eq 0 })
$seen = @($detect | Where-Object { $_.UpgradeCode -eq $a.UpgradeCode -and (Test-InRange $a.ProductVersion $_) })
Assert 'صفُّ الترقية يشمل (أ)' ($seen.Count -gt 0) `
    $(if ($seen.Count) { "$($seen[0].ActionProperty) · المدى [$($seen[0].VersionMin)…$($seen[0].VersionMax)] Attr=$($seen[0].Attributes)" }
      else { "لا صفَّ يشمل $($a.ProductVersion) — المنتجُ القديم لن يُزال" })

$down = @($b.Upgrade | Where-Object { $_.ActionProperty -match 'DOWNGRADE' })
$blocked = @($down | Where-Object { Test-InRange $a.ProductVersion $_ })
Assert 'صفُّ التنزيل لا يحجب (أ)' ($blocked.Count -eq 0) `
    $(if ($blocked.Count) { "‏(أ) يقع في مدى التنزيل — التثبيتُ سيُرفض" } else { "سليم" })

foreach ($pair in @(@{ n = 'أ'; p = $PackageA }, @{ n = 'ب'; p = $PackageB })) {
    $log = [IO.Path]::GetTempFileName()
    & wix msi validate $pair.p > $log 2>&1
    $errs = @(Select-String -Path $log -Pattern 'error WIX\d+' -ErrorAction SilentlyContinue)
    Assert "صفرُ أخطاء ICE في ($($pair.n))" ($errs.Count -eq 0) "$($errs.Count) خطأ"
    Remove-Item $log -Force -ErrorAction SilentlyContinue
}

Write-Host ""
# ‏ما لا يُثبته هذا السكربت — يُقال، ولا يُقرأ نجاحُه أوسعَ ممّا هو.
Write-Host "‏ما لا يُثبَت هنا (يلزمه تنصيبٌ حيّ في VM):" -ForegroundColor Yellow
Write-Host "   · أنّ RemoveExistingProducts نُفِّذ فعلاً وأزال القديم" -ForegroundColor DarkGray
Write-Host "   · أنّ «إضافة وإزالة البرامج» تُظهر إدخالاً واحداً بعد الترقية" -ForegroundColor DarkGray
Write-Host "   · أنّ FileVersion على القرص صار إصدارَ (ب)" -ForegroundColor DarkGray
Write-Host "   · أنّ بيانات المستخدم نجت من الترقية" -ForegroundColor DarkGray

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "[FAIL] $($failures.Count) شرطاً لم يتحقّق: $($failures -join ' · ')" -ForegroundColor Red
    exit 1
}
Write-Host "[PASS] الحزمتان تُشكّلان مسارَ ترقيةٍ صحيحاً — بقدر ما تقوله قاعدتاهما." -ForegroundColor Green
exit 0
