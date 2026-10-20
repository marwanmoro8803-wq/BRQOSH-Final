#Requires -Version 7
<#
.SYNOPSIS
    (ح-١٤٢ · installer-deployment-ops#2 · commercial-readiness#9) قراراتُ الإصدار النقيّة — تُختبر بلا بناء.

.DESCRIPTION
    ‏دوالُّ وحدَها — يُستورد بالنقطة من scripts/release.ps1 وscripts/release-describe.ps1.
    ‏**ولا param() هنا**: الاستيرادُ بالنقطة يُشغّل كتلةَ الوسائط في نطاق المستدعي، فكانت
    ‏`-BuildNumber` الافتراضيّةُ (0) تدهس رقمَ الإصدار — أمسكه أوّلُ تشغيلٍ حقيقيّ.
#>
function Get-BrqoshVersionPrefix {
    param([Parameter(Mandatory)][string]$Props)

    $match = Select-String -Path $Props -Pattern '<BrqoshVersionPrefix>(\d+\.\d+)</BrqoshVersionPrefix>' |
        Select-Object -First 1
    if (-not $match) { throw "لا BrqoshVersionPrefix في $Props." }
    return $match.Matches[0].Groups[1].Value
}

function Get-BrqoshReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$Prefix,
        [Parameter(Mandatory)][int]$BuildNumber
    )

    # ‏**الصفرُ رقمُ البناء المحلّيّ** (Directory.Build.props) — بناءٌ لا يدّعي أنّه إصدار.
    if ($BuildNumber -le 0) {
        throw "رقمُ البناء $BuildNumber ليس رقمَ إصدار — الصفرُ للبناء المحلّيّ، والإصدارُ يأخذ رقمَ تشغيلة CI."
    }
    return "$Prefix.$BuildNumber"
}

function Get-BrqoshReleaseNotes {
    param(
        [Parameter(Mandatory)][string]$Changelog,
        [Parameter(Mandatory)][string]$Version
    )

    # ‏**لا إصدارَ بلا سطرٍ في سجلّ التغييرات** — والملاحظاتُ الموقَّعةُ في البيان منه، لا من يدٍ يومَ الإصدار.
    if (-not (Test-Path $Changelog)) { throw "سجلُّ التغييرات غير موجود: $Changelog" }

    $lines = Get-Content -Path $Changelog -Encoding utf8
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^##\s+$([regex]::Escape($Version))(\s|$)") { $start = $i; break }
    }
    if ($start -lt 0) {
        throw "لا قسمَ «## $Version» في $Changelog — اكتب ما تغيّر قبل الإصدار."
    }

    $body = New-Object System.Collections.Generic.List[string]
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^##\s') { break }
        $body.Add($lines[$i])
    }

    $notes = (($body | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) -join "`n").Trim()
    if ($notes.Length -eq 0) { throw "قسمُ «## $Version» في $Changelog فارغ." }
    return $notes
}

<#
.SYNOPSIS
  (installer-deployment-ops#13) اتّفاقيّةُ الترخيص نهائيّةٌ قبل الإصدار — المسوّدةُ لا تُشحن.
.DESCRIPTION
  النصُّ يُعرض للمستخدم قبل الإعداد (EulaWindow)، ويبدأ بوسم «DRAFT» حتى يعتمده المستشارُ القانونيّ. فالإصدارُ يرفض
  نصّاً غائباً أو فارغاً أو ما زال يحمل الوسم — ولا يُنشر برنامجٌ يعرض على عميله مسوّدةً عقداً.
#>
function Assert-BrqoshEulaFinal {
    param([Parameter(Mandatory)][string]$Eula)
    if (-not (Test-Path $Eula)) { throw "اتّفاقيّة الترخيص غير موجودة: $Eula" }
    $text = Get-Content -Path $Eula -Raw -Encoding utf8
    if ([string]::IsNullOrWhiteSpace($text)) { throw "اتّفاقيّة الترخيص فارغة: $Eula" }
    if ($text -match '\[DRAFT') { throw "اتّفاقيّة الترخيص ما زالت مسوّدةً (وسم [DRAFT]) — اعتمدها قانونيّاً وأزل الوسم قبل الإصدار." }
}
