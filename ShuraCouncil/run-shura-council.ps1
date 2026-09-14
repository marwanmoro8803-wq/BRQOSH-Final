# BRQOSH — Run the executable Shura Council on a saved CEEF report.
# Usage:
#   .\ShuraCouncil\run-shura-council.ps1
#   .\ShuraCouncil\run-shura-council.ps1 -Source "تقارير/CEEF-145_Live_UI_Audit_Marwan.md"
param(
    [string]$Source = "dist/evidence/prompt-governance/CEEF-PROMPT-001.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

# ‏(م-٥ · ن-٢٠) **مشروعٌ واحدٌ يُسمّى، ولا يُختار.**
#
# ‏كان هنا `if (Test-Path "tools/ShuraCouncil/ShuraCouncil.csproj")` يُفضّل النسخةَ التي
# ‏تحت `tools` **متى وُجدت** — وهي موجودة. فكان هذا المُشغِّلُ، وهو مُشغِّلُ الجذر، **يبني
# ‏غيرَ الجذر ويُشغّله**: نسخةُ الثمانية ملفّاتٍ بدل محرّك v5 ذي المُدقّق الخصميّ
# ‏والتخصّصات الثمانية والعشرين وبوّابة Supreme. ومقيس: تقاريرُ
# ‏`dist/evidence/shura-council/` تحمل ختمَ `tools/ShuraCouncil` في ٢٢ تشغيلةً من ٣٠،
# ‏و`ShuraCouncil/bin` لم يُنشأ قطّ.
#
# ‏**واختيارٌ داخل مُشغِّلٍ يعني أنّ وجودَ مجلّدٍ على القرص يُقرّر أيُّ محرّكٍ يحكم** — وهو
# ‏قرارٌ لا يراه أحدٌ في مراجعة. فالمسارُ مكتوبٌ صريحاً، وبوّابةُ `OneShuraCouncilTests`
# ‏تُسقط عودةَ أيّ مسارٍ ثانٍ.
$proj = "ShuraCouncil/ShuraCouncil.csproj"

Write-Host "Building ShuraCouncil..." -ForegroundColor Cyan
dotnet build $proj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running council on: $Source" -ForegroundColor Cyan
dotnet run --project $proj -c Release --no-build -- --source $Source
exit $LASTEXITCODE
