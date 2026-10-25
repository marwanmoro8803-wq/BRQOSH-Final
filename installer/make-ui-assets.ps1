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
    $line = "إصدار البرنامج $ProductVersion · Software version $ProductVersion"
    [void]$rtf.AppendLine("\pard\rtlpar\qr\b $(ConvertTo-RtfText $line)\b0\par\pard\par")
}
$arabic = [regex]'[؀-ۿ]'
foreach ($line in $eula) {
    if ([string]::IsNullOrWhiteSpace($line)) { [void]$rtf.AppendLine('\pard\par'); continue }
    $isHeading = $line -match '^\d+\.\s' -or $line -match 'اتّفاقيّة ترخيص|End-User Licence'
    $dir = if ($arabic.IsMatch($line)) { '\rtlpar\qr' } else { '\ltrpar\ql' }
    $weight = if ($isHeading) { '\b' } else { '' }
    $off = if ($isHeading) { '\b0' } else { '' }
    [void]$rtf.AppendLine("\pard$dir$weight $(ConvertTo-RtfText $line)$off\par")
}
[void]$rtf.AppendLine('}')
[System.IO.File]::WriteAllText((Join-Path $OutDir 'license.rtf'), $rtf.ToString(), [System.Text.Encoding]::ASCII)

# ── ٢) صورتا الواجهة ───────────────────────────────────────────────────────────
Add-Type -AssemblyName System.Drawing
$navy = [System.Drawing.ColorTranslator]::FromHtml('#0B3150')
$gold = [System.Drawing.ColorTranslator]::FromHtml('#C9A227')
$mark = [System.Drawing.Image]::FromFile((Join-Path $Root 'assets\brand\brqosh-mark-gold.png'))

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAliasGridFit'
    return @($bmp, $g)
}

# لافتةُ الأعلى 493×58: بيضاءُ بخطٍّ كحليٍّ وذهبيٍّ سفليّ ودرعٍ عند الطرف.
$bmp, $g = New-Canvas 493 58
$g.Clear([System.Drawing.Color]::White)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $navy), 0, 54, 493, 3)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $gold), 0, 57, 493, 1)
$g.DrawImage($mark, 440, 6, 34, 42)
$bmp.Save((Join-Path $OutDir 'banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose()

# صورةُ الترحيب 493×312: لوحةٌ كحليّةٌ يساراً (164 بكسل — هي ما يعرضه WixUI) والباقي أبيض.
$bmp, $g = New-Canvas 493 312
$g.Clear([System.Drawing.Color]::White)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $navy), 0, 0, 164, 312)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $gold), 160, 0, 4, 312)
$g.DrawImage($mark, 42, 72, 80, 100)
$font = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Bold)
$fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
$g.DrawString('BRQOSH', $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 188, 164, 30), $fmt)
$small = New-Object System.Drawing.Font 'Segoe UI', 8
$g.DrawString('ERP', $small, (New-Object System.Drawing.SolidBrush $gold), (New-Object System.Drawing.RectangleF 0, 216, 164, 18), $fmt)
$bmp.Save((Join-Path $OutDir 'dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose(); $mark.Dispose()

Write-Host "أصولُ واجهة المُنصِّب في $OutDir" -ForegroundColor DarkGray
