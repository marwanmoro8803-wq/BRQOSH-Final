#Requires -Version 7.0
<#
.SYNOPSIS
  (م-١ · ن-٠١) بوّابةُ بناءٍ ترفض إصداراً سبق شحنُه — يُشغّلها `installer/build-msi.ps1` و`verify-gates.ps1` معاً.

.DESCRIPTION
  `AllowSameVersionUpgrades` يُنقذ الترقيةَ حين يتساوى الرقمان، لكنّه لا يجعل التساويَ صحيحاً:
  إصداران بالرقم نفسِه لا يُميَّزان في «إضافة وإزالة البرامج» ولا في سجلّ عطل ولا في بلاغِ
  عميل. فالتساوي يُمنع هنا، ويُنقذ هناك.

  (ح-٧٣ · evidence-integrity#4) **كانت أسطراً داخل `build-msi.ps1`، و`verify-gates.ps1` يُعيد
  كتابةَ منطقها** بدل أن يُشغّله — **ويُلحق رقماً بدفتر الإصدارات المُتعقَّب نفسِه** ثمّ يُعيده.
  صارت ملفّاً واحداً يأخذ الدفترَ معاملاً، فيُختبر على دفترٍ مؤقّتٍ خارج الشجرة.

.PARAMETER Version
  الإصدارُ المُراد بناؤه (الحقولُ الثلاثة الأولى).

.PARAMETER Ledger
  دفترُ الإصدارات المشحونة — سطرٌ لكلّ إصدار، و`#` تعليق. **وغيابُه لا يُسقط** (لا شحنَ بعد).
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Ledger
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Ledger)) { return }
$shipped = @(Get-Content $Ledger | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
if ($shipped -contains $Version) {
    Write-Host "الإصدار $Version مُسجَّلٌ مشحوناً في $Ledger." -ForegroundColor Red
    Write-Host "ارفع BrqoshVersionPrefix أو مرّر BrqoshBuildNumber مختلفاً — ولا تشحن رقمين متطابقين." -ForegroundColor Red
    throw "‏الإصدار $Version مُسجَّلٌ مشحوناً."
}
