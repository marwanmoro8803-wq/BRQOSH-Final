# ينشئ اختصار «ترخيص برقوش» على سطح المكتب يشير إلى برنامج الترخيص المنشور.
# يُشغَّل من جذر المستودع (أو يُمرَّر له مسارُ الـexe صراحةً).
param(
    [string]$TargetExe = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($TargetExe)) {
    $TargetExe = Join-Path $repoRoot "dist\BrqoshLicenseIssuer\BrqoshLicenseIssuer.exe"
}

$TargetExe = [System.IO.Path]::GetFullPath($TargetExe)
if (-not (Test-Path $TargetExe)) {
    throw "لم يُعثر على برنامج الترخيص: $TargetExe — انشره أولاً: dotnet publish tools\VendorLicenseIssuer\VendorLicenseIssuer.csproj -c Release -o dist\BrqoshLicenseIssuer"
}

$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "ترخيص برقوش.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $TargetExe
$shortcut.WorkingDirectory = Split-Path -Parent $TargetExe
$shortcut.IconLocation = "$TargetExe,0"
$shortcut.Description = "BRQOSH — برنامج إصدار تراخيص برقوش"
$shortcut.Save()

Write-Host "أُنشئ الاختصار: $shortcutPath"
Write-Host "يشير إلى: $TargetExe"
