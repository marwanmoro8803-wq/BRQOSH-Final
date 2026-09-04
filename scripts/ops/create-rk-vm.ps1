<#
.SYNOPSIS
    (CEEF-236 · ر-ك) يبني آلةً افتراضيةً نظيفةً لإثبات الإقلاع الإنتاجيّ من المُنصِّب.

.DESCRIPTION
    ر-ك تشترط جهازاً **نظيفاً بلا .NET 9** يُنصَّب عليه dist\BRQOSH-Setup.msi — فتُقاس معه
    بوّابةُ المُنصِّب (T-07، مُعلَّقةٌ NOT TESTED منذ CEEF-234) ثمّ مسارُ العميل كاملاً.

    وهذا السكربت يبني تلك الآلة بـVirtualBox: Windows 11 · EFI · TPM 2.0 · تنصيبٌ غير
    مراقَب. **ولا يُنصِّب .NET عمداً** — غيابُه هو المقيس.

    المراحل (‏-Stage‎): Create ثمّ Install ثمّ Status.
#>
[CmdletBinding()]
param(
    [ValidateSet("Create", "Install", "Status", "Destroy")]
    [string]$Stage = "Create",
    [string]$VmName = "brqosh-rk",
    [string]$BaseFolder = "D:\brqosh-vm\vms",
    [string]$Iso = "D:\brqosh-vm\win11-ent-eval-25h2-x64-en-us.iso",
    [string]$ShareHostPath = "D:\brqosh-vm\share",
    [string]$GuestUser = "brqosh",
    [string]$GuestPassword = "Rk!Pilot2026",
    [int]$MemoryMB = 8192,
    [int]$Cpus = 4,
    [int]$DiskMB = 65536
)

$ErrorActionPreference = "Stop"
$vb = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
if (-not (Test-Path $vb)) { throw "VBoxManage غير موجود: $vb" }

function Invoke-VBox {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    Write-Host "  > VBoxManage $($Args -join ' ')"
    $output = & $vb @Args 2>&1
    if ($LASTEXITCODE -ne 0) { $output | ForEach-Object { Write-Host "    $_" }; throw "أخفق: VBoxManage $($Args -join ' ')" }
    return $output
}
function Test-VmExists { (& $vb list vms) -match "`"$VmName`"" }

switch ($Stage) {

  "Status" {
      if (-not (Test-VmExists)) { Write-Host "الآلةُ $VmName غيرُ موجودة."; break }
      $info = & $vb showvminfo $VmName --machinereadable
      $state = ($info | Select-String '^VMState=' ) -replace 'VMState=|"', ''
      Write-Host "الحالة: $state"
      ($info | Select-String '^(memory|cpus|firmware|GuestAdditionsVersion|GuestOSType)=')
      Write-Host "=== IP الضيف (إن كانت الإضافات تعمل) ==="
      & $vb guestproperty get $VmName "/VirtualBox/GuestInfo/Net/0/V4/IP" 2>&1
  }

  "Destroy" {
      if (Test-VmExists) {
          & $vb controlvm $VmName poweroff 2>&1 | Out-Null
          Start-Sleep -Seconds 3
          Invoke-VBox unregistervm $VmName --delete
          Write-Host "أُزيلت $VmName."
      } else { Write-Host "لا شيء ليُزال." }
  }

  "Create" {
      if (-not (Test-Path $Iso)) { throw "لا توجد نسخةُ التقييم: $Iso" }
      $isoSize = (Get-Item $Iso).Length
      if ($isoSize -lt 5GB) { throw "حجمُ النسخة $([math]::Round($isoSize/1GB,2)) ج.ب — التنزيلُ لم يكتمل." }
      if (Test-VmExists) { throw "الآلةُ $VmName موجودةٌ سلفاً — استعمل -Stage Destroy أولاً." }

      New-Item -ItemType Directory -Force -Path $BaseFolder, $ShareHostPath | Out-Null

      Write-Host "=== إنشاءُ $VmName ==="
      Invoke-VBox createvm --name $VmName --ostype Windows11_64 --register --basefolder $BaseFolder | Out-Null

      # ‏ويندوز ١١ يشترط UEFI و TPM 2.0 — تُضبط صراحةً لا يُعتمد على تجاوز.
      Invoke-VBox modifyvm $VmName `
          --memory $MemoryMB --cpus $Cpus --vram 128 `
          --firmware efi --tpm-type 2.0 `
          --graphicscontroller vboxsvga --usb-xhci on `
          --nic1 nat --audio-driver none `
          --clipboard-mode bidirectional --draganddrop hosttoguest | Out-Null

      $vdi = Join-Path $BaseFolder "$VmName\$VmName.vdi"
      Invoke-VBox createmedium disk --filename $vdi --size $DiskMB --format VDI | Out-Null
      Invoke-VBox storagectl $VmName --name "SATA" --add sata --controller IntelAhci --portcount 2 | Out-Null
      Invoke-VBox storageattach $VmName --storagectl "SATA" --port 0 --device 0 --type hdd --medium $vdi | Out-Null

      # ‏مجلّدٌ مشترك: تدخل منه حزمةُ MSI وتخرج فيه الأدلّة.
      Invoke-VBox sharedfolder add $VmName --name "rk" --hostpath $ShareHostPath --automount | Out-Null

      Write-Host ""
      Write-Host "أُنشئت. التالي:  ./scripts/ops/create-rk-vm.ps1 -Stage Install"
  }

  "Install" {
      if (-not (Test-VmExists)) { throw "الآلةُ $VmName غيرُ موجودة — شغّل -Stage Create أولاً." }
      Write-Host "=== تنصيبٌ غير مراقَب (يستغرق عشرات الدقائق) ==="
      Write-Host "‏**ولا يُنصَّب .NET عمداً** — غيابُه هو ما تقيسه بوّابةُ المُنصِّب."
      Invoke-VBox unattended install $VmName `
          --iso=$Iso `
          --user=$GuestUser --user-password=$GuestPassword `
          --full-user-name="BRQOSH Pilot" `
          --install-additions `
          --locale=en_US --country=US `
          --time-zone="Arab Standard Time" `
          --hostname="$VmName.local" `
          --start-vm=gui | Out-Null
      Write-Host ""
      Write-Host "بدأ التنصيب. تابع بـ:  ./scripts/ops/create-rk-vm.ps1 -Stage Status"
  }
}
