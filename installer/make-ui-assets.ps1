#Requires -Version 5.1
<#
.SYNOPSIS
    (واجهةُ التنصيب) يولّد من مصدرٍ واحدٍ أصولَ واجهة المُنصِّب: اتفاقيةُ الترخيص RTF من assets\legal\eula.txt، ولافتةُ الأعلى، وصورةُ الترحيب.

.DESCRIPTION
    النصُّ لا يُكرَّر: اتفاقيّةُ المُنصِّب هي ملفُّ `eula.txt` نفسُه الذي يقبله البرنامجُ عند الإقلاع، فلا يُقبل نصٌّ ثمّ يُشغَّل آخر.
    والعربيُّ يُكتب RTL والإنجليزيُّ LTR، والحروفُ غيرُ اللاتينيّة تُهرَّب بـ\uN.
    والصورتان بألوان الهويّة (كحليّ #0B3150 وذهبيّ #C9A227) وبدرع BRQOSH من assets\brand.
#>
param(
    [Parameter(Mandatory)][string]$OutDir,
    [string]$ProductVersion = ''
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# ── ١) اتفاقيّةُ الترخيص RTF ───────────────────────────────────────────────────
function ConvertTo-RtfText([string]$text) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $text.ToCharArray()) {
        $code = [int]$ch
        if ($ch -eq '\') { [void]$sb.Append('\\') }
        elseif ($ch -eq '{') { [void]$sb.Append('\{') }
        elseif ($ch -eq '}') { [void]$sb.Append('\}') }
        elseif ($code -gt 127) {
            $signed = if ($code -gt 32767) { $code - 65536 } else { $code }
            # ‏`${signed}` بأقواس: في PowerShell 7 يُقرأ `$signed?` متغيّراً اسمُه «signed?» فيخرج `\u` بلا رقم وتضيع الحروفُ العربيّة.
            [void]$sb.Append("\u${signed}?")
        }
        else { [void]$sb.Append($ch) }
    }
    $sb.ToString()
}

$eula = Get-Content -Path (Join-Path $Root 'assets\legal\eula.txt') -Encoding utf8
$rtf = New-Object System.Text.StringBuilder
[void]$rtf.AppendLine('{\rtf1\ansi\ansicpg1256\deff0\deflang1025{\fonttbl{\f0\fnil\fcharset178 Segoe UI;}}')
[void]$rtf.AppendLine('\viewkind4\uc1\f0\fs20')
if ($ProductVersion) {
    # ‏سطرُ إصدار البرنامج في أعلى الاتفاقيّة بالمُنصِّب (للمُنصِّب فقط؛ eula.txt لا يتغيّر مع كلّ إصدار فلا يُعاد قبولُه بلا سبب).
    $line = "إصدار البرنامج $ProductVersion"
    [void]$rtf.AppendLine("\pard\rtlpar\qr\b $(ConvertTo-RtfText $line)\b0\par")
    [void]$rtf.AppendLine("\pard\ltrpar\ql\b Software version $ProductVersion\b0\par\pard\par")
}
$arabic = [regex]'[؀-ۿ]'
foreach ($line in $eula) {
    if ([string]::IsNullOrWhiteSpace($line)) { [void]$rtf.AppendLine('\pard\par'); continue }
    $isHeading = $line -match '^\d+\.\s' -or $line -match 'اتّفاقيّة ترخيص|End-User Licence'
    $dir = if ($arabic.IsMatch($line)) { '\rtlpar\qr' } else { '\ltrpar\ql' }
    $weight = if ($isHeading) { '\b' } else { '' }
    $off = if ($isHeading) { '\b0' } else { '' }
    [void]$rtf.AppendLine("\pard\rtlpar\qr\b $(ConvertTo-RtfText $line)\b0\par")
}
[void]$rtf.AppendLine('}')
[System.IO.File]::WriteAllText((Join-Path $OutDir 'license.rtf'), $rtf.ToString(), [System.Text.Encoding]::ASCII)

# ── ٢) صورتا الواجهة ───────────────────────────────────────────────────────────
# ‏تُولَّدان مع بقيّة الهويّة بـtools/brand/build_brand.py (درعٌ كريميّ على بنّيّ #8B5A2B كما في تصميم المالك) — لا رسمَ هنا فلا تنحرف النسختان.
$brand = Join-Path $Root 'assets\brand\installer'
foreach ($f in 'banner.bmp', 'dialog.bmp') {
    $src = Join-Path $brand $f
    if (-not (Test-Path $src)) { throw "صورةُ المُنصِّب غير موجودة: $src — شغّل tools\brand\build_brand.py" }
    Copy-Item $src (Join-Path $OutDir $f) -Force
}

Write-Host "أصولُ واجهة المُنصِّب في $OutDir" -ForegroundColor DarkGray
