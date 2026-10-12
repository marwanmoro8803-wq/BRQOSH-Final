# (جاهزيّةُ الإطلاق · ح-١٢٨ · installer-deployment-ops#3) ‏**طريقةُ توقيع الكود — من البيئة، وبالطريقة التي تُصدَر بها الشهاداتُ اليوم.**
#
# ‏كان build-msi.ps1 لا يقبل إلّا ملفَّ PFX (`signtool /f /p`). **والجهاتُ المُصدِرةُ لم تعد تُصدر شهادةَ توقيع كودٍ بمفتاحٍ يُصدَّر إلى ملفّ**
# ‏(متطلّباتُ CA/Browser Forum منذ يونيو 2023: المفتاحُ على رمزِ أمانٍ أو HSM) — فعبارةُ «الخطّافُ جاهزٌ ينتظر الشهادة» لم تكن صحيحة:
# ‏الشهادةُ التي ستصل لا يقبلها الخطّاف.
#
# ‏**الطريقتان:**
#   ‏BRQOSH_SIGNING_THUMBPRINT — شهادةٌ في مخزن ويندوز (المستخدم أو الجهاز) ومفتاحُها على رمزٍ أو HSM بمزوّد KSP: `signtool /sha1`.
#   ‏BRQOSH_SIGNING_PFX + BRQOSH_SIGNING_PASSWORD — ملفُّ PFX (قديمٌ، يبقى لمن عنده): `signtool /f /p`.
# ‏**وضبطُ الاثنين معاً خطأٌ يُسقط البناء** — لا يُختار بينهما صامتاً. والختمُ الزمنيُّ BRQOSH_SIGNING_TIMESTAMP (افتراضُه DigiCert).
#
# ‏**ويُقاس** بـ`-Describe`: يطبع القرارَ JSON ولا يوقّع شيئاً — **وكلمةُ المرور لا تُطبع أبداً** (`***`).
param([switch]$Describe)

function Resolve-BrqoshSigning {
    $thumb = $env:BRQOSH_SIGNING_THUMBPRINT
    $pfx = $env:BRQOSH_SIGNING_PFX
    $timestamp = if ($env:BRQOSH_SIGNING_TIMESTAMP) { $env:BRQOSH_SIGNING_TIMESTAMP } else { 'http://timestamp.digicert.com' }
    $common = @('/fd', 'SHA256', '/tr', $timestamp, '/td', 'SHA256')

    if ($thumb -and $pfx) {
        return @{ Mode = 'Error'; Error = 'ضُبط BRQOSH_SIGNING_THUMBPRINT وBRQOSH_SIGNING_PFX معاً — اختر طريقةً واحدة.'; Args = @() }
    }

    if ($thumb) {
        $clean = ($thumb -replace '\s', '').ToUpperInvariant()
        if ($clean -notmatch '^[0-9A-F]{40}$') {
            return @{ Mode = 'Error'; Error = 'BRQOSH_SIGNING_THUMBPRINT ليست بصمةَ SHA-1 (أربعون رمزاً ست عشريّاً).'; Args = @() }
        }
        return @{ Mode = 'CertificateStore'; Error = $null; Args = @('/sha1', $clean) + $common }
    }

    if ($pfx) {
        if (-not (Test-Path $pfx)) {
            return @{ Mode = 'Error'; Error = "BRQOSH_SIGNING_PFX يشير إلى ملفٍّ غير موجود: $pfx"; Args = @() }
        }
        return @{ Mode = 'Pfx'; Error = $null; Args = @('/f', $pfx, '/p', $env:BRQOSH_SIGNING_PASSWORD) + $common }
    }

    return @{ Mode = 'None'; Error = $null; Args = @() }
}

if ($Describe) {
    $r = Resolve-BrqoshSigning
    $shown = @()
    for ($i = 0; $i -lt $r.Args.Count; $i++) {
        # ‏كلمةُ المرور بعد `/p` لا تُطبع أبداً.
        if ($i -gt 0 -and $r.Args[$i - 1] -eq '/p') { $shown += '***' } else { $shown += $r.Args[$i] }
    }
    [pscustomobject]@{ Mode = $r.Mode; Error = $r.Error; Args = $shown } | ConvertTo-Json -Compress
}
