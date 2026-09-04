<#
.SYNOPSIS
    (CEEF-236 · ر-ك · الدرز) الشقُّ المُضيفيّ: يوقّع ترخيصَ الآلة النظيفة **على جهاز المورِّد** حين يصل طلبُها.

.DESCRIPTION
    rk-prodboot-guest.ps1 داخل الآلة يكتب `prodboot-<stamp>-request.json` (اسمُ المنشأة + معرّفُ الجهاز)
    في المجلّد المشترك ثمّ ينتظر. هذا السكربتُ يراقب المجلّد، يوقّع بـ LicenseIssuer بالمفتاح الخاصّ
    الذي **لا يغادر المُضيف**، ويكتب `prodboot-<stamp>-license.json`. صمتُه إخفاق: إن انتهت المهلةُ
    بلا طلبٍ خرج بغير صفر.
#>
[CmdletBinding()]
param(
    [string]$Share = "D:\brqosh-vm\share",
    [int]$WaitMinutes = 40,
    [int]$ValidDays = 365
)
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path -LiteralPath (Split-Path -Parent $PSCommandPath) | Split-Path -Parent | Split-Path -Parent
$vendorKey = Join-Path $env:APPDATA "BRQOSH\vendor-signing\license-private.pem"
if (-not (Test-Path $vendorKey)) { throw "لا مفتاحَ مورِّدٍ خاصّاً: $vendorKey" }
$issuer = Join-Path $repoRoot "tools\LicenseIssuer\bin\Debug\net9.0\LicenseIssuer.exe"
if (-not (Test-Path $issuer)) {
    & dotnet build (Join-Path $repoRoot "tools\LicenseIssuer") -c Debug --nologo | Out-Null
    if (-not (Test-Path $issuer)) { throw "تعذّر بناءُ LicenseIssuer: $issuer" }
}

$deadline = (Get-Date).AddMinutes($WaitMinutes)
$done = @{}
Write-Host "يراقب $Share حتى $($deadline.ToString('HH:mm:ss')) …"
while ((Get-Date) -lt $deadline) {
    foreach ($req in Get-ChildItem -LiteralPath $Share -Filter "prodboot-*-request.json" -ErrorAction SilentlyContinue) {
        $licPath = $req.FullName -replace "-request\.json$", "-license.json"
        if ($done.ContainsKey($req.Name) -or (Test-Path $licPath)) { continue }
        Start-Sleep -Seconds 1   # الملفُّ قد يكون نصفَ مكتوب
        $r = Get-Content -LiteralPath $req.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($r.MachineId) -or [string]::IsNullOrWhiteSpace($r.CompanyName)) {
            Write-Host "طلبٌ ناقص: $($req.Name)"; continue
        }
        Write-Host "طلب $($req.Name): company='$($r.CompanyName)' machine='$($r.MachineId)'"
        # ‏عبر الـexe مباشرةً — `dotnet run -- sign` يبتلع الوسائط (ثبت في المسبار المُضيفيّ).
        $signed = (& $issuer sign $r.CompanyName $r.MachineId $vendorKey $ValidDays 2>&1 |
                   Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1)
        if ([string]::IsNullOrWhiteSpace($signed)) { Write-Host "تعذّر التوقيع لـ $($req.Name)"; exit 2 }
        # ‏يُكتب إلى اسمٍ مؤقّتٍ ثمّ يُسمّى — فلا يقرأ الضيفُ ملفاً نصفَ مكتوب.
        $tmp = "$licPath.tmp"
        [System.IO.File]::WriteAllText($tmp, $signed, (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $tmp -Destination $licPath -Force
        $done[$req.Name] = $true
        Write-Host "وُقِّع ⟶ $(Split-Path $licPath -Leaf) ($($signed.Length) محرفاً)"
        $sha = (Get-FileHash $licPath -Algorithm SHA256).Hash
        Write-Host "SHA-256: $sha"
        if ($env:RK_PRODBOOT_ONE_SHOT) { exit 0 }
    }
    Start-Sleep -Seconds 3
}
if ($done.Count -eq 0) { Write-Host "انتهت المهلةُ بلا طلب."; exit 1 }
exit 0
