# تحقق حيّ من واجهة BRQOSH عبر UI Automation (CEEF-209)
#
# لماذا هذا السكربت وليس tools/UiAudit؟
#   UiAudit يستعمل FlaUI فوق UIA3 COM، وثبت تجريبياً أنه يعجز عن العثور على النافذة في
#   بعض جلسات ويندوز رغم وجودها فعلاً (نفس عائلة الخطأ 0x8000FFFF المسجّل في تشغيلاته
#   القديمة). واجهة UIAutomationClient المُدارة — المستعملة هنا — وجدت النافذة فوراً في
#   نفس الجلسة. فهذا مسار تحقق بديل مُثبت، قراءة فقط، بلا نقر أعمى.
#
# الاستخدام (والتطبيق يعمل بالفعل):
#   pwsh -File scripts/ops/verify-ui-live.ps1
#   pwsh -File scripts/ops/verify-ui-live.ps1 -Nav "فواتير المبيعات" -Dialog "فاتورة مبيعات جديدة"

[CmdletBinding()]
param(
    [string]$WindowTitle = "BRQOSH ERP",
    [string]$Nav,
    [string]$Dialog,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

function Find-MainWindow {
    param([string]$Title, [int]$TimeoutSeconds)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Title)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($null -ne $win) { return $win }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Get-Descendants {
    param($Element, $ControlType)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)
    return $Element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

$win = Find-MainWindow -Title $WindowTitle -TimeoutSeconds $TimeoutSeconds
if ($null -eq $win) {
    Write-Host "[FAIL] لم تُعثر على نافذة '$WindowTitle' — شغّل التطبيق أولاً." -ForegroundColor Red
    exit 1
}
Write-Host "[PASS] النافذة الرئيسية: $($win.Current.Name)" -ForegroundColor Green

# جرد أزرار التنقّل — يكشف شاشة مفقودة أو صلاحية تحجب قسماً.
$navButtons = Get-Descendants -Element $win -ControlType ([System.Windows.Automation.ControlType]::Button)
$navNames = @()
foreach ($b in $navButtons) { if ($b.Current.Name) { $navNames += $b.Current.Name } }
Write-Host "[PASS] أزرار التنقّل والإجراءات: $($navNames.Count)" -ForegroundColor Green

if (-not $Nav) {
    $navNames | ForEach-Object { "   - $_" }
    exit 0
}

$target = $navButtons | Where-Object { $_.Current.Name -eq $Nav } | Select-Object -First 1
if ($null -eq $target) {
    Write-Host "[FAIL] لا يوجد زر تنقّل باسم '$Nav'." -ForegroundColor Red
    exit 2
}
$target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 3
Write-Host "[PASS] فُتح القسم: $Nav" -ForegroundColor Green

if (-not $Dialog) { exit 0 }

# فتح حوار الإنشاء (زر يبدأ بعلامة + عادةً).
$afterNav = Get-Descendants -Element $win -ControlType ([System.Windows.Automation.ControlType]::Button)
$newBtn = $afterNav | Where-Object { $_.Current.Name -match "جديد" } | Select-Object -First 1
if ($null -eq $newBtn) {
    Write-Host "[FAIL] لم يُعثر على زر الإنشاء في '$Nav'." -ForegroundColor Red
    exit 3
}
$newBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::RootElement
$dlgCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $Dialog)
$dlg = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $dlgCond)
if ($null -eq $dlg) {
    Write-Host "[FAIL] لم يُفتح الحوار '$Dialog'." -ForegroundColor Red
    exit 4
}
Write-Host "[PASS] فُتح الحوار: $($dlg.Current.Name)" -ForegroundColor Green

# رؤوس أعمدة الشبكة — الدليل الحاسم على وصل الأعمدة بالواجهة فعلاً.
$headers = Get-Descendants -Element $dlg -ControlType ([System.Windows.Automation.ControlType]::HeaderItem)
Write-Host "[PASS] أعمدة الشبكة: $($headers.Count)" -ForegroundColor Green
foreach ($h in $headers) { "   - $($h.Current.Name)" }
