# يبني BRQOSH-Uninstall.exe بمُصرِّف C# المضمَّن في ويندوز (.NET Framework 4.x) — فلا يحتاج الجهازُ وقتَ تشغيلٍ إضافيّاً،
# ويحمل أيقونةَ الشعار وسلّةَ المهملات. الناتجُ يُوقَّع بالشهادة نفسِها إن ضُبط التوقيعُ في build-msi.ps1.
param([Parameter(Mandatory)][string]$OutDir)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "مُصرِّف C# غير موجود: $csc" }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$out = Join-Path $OutDir 'BRQOSH-Uninstall.exe'
& $csc /nologo /target:winexe /optimize+ "/win32icon:$(Join-Path $Root 'assets\brand\brqosh-uninstall.ico')" /r:System.Windows.Forms.dll "/out:$out" (Join-Path $PSScriptRoot 'BrqoshUninstall.cs')
if ($LASTEXITCODE -ne 0) { throw "بناء برنامج الإزالة أخفق (exit=$LASTEXITCODE)." }
Write-Host "برنامجُ الإزالة: $out" -ForegroundColor Cyan
$out
