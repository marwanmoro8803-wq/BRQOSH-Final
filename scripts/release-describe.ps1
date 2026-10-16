#Requires -Version 7
<#
.SYNOPSIS
    (ح-١٤٢) يطبع قرارَ الإصدار — {Version, Notes} JSON — أو يخرج بـ2 وخطأٍ مسمّى. للاختبار وللمراجعة قبل الإصدار.

.EXAMPLE
    pwsh -File scripts/release-describe.ps1 -BuildNumber 7 -Changelog CHANGELOG.md -Props Directory.Build.props
#>
param(
    [int]$BuildNumber = 0,
    [Parameter(Mandatory)][string]$Changelog,
    [Parameter(Mandatory)][string]$Props
)

# ‏مخرجُ الوحدة الطرفيّة بصفحة النظام افتراضاً — فيصل النصُّ العربيُّ مُشوَّهاً لمن يقرؤه.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
. (Join-Path $PSScriptRoot 'release-lib.ps1')

try {
    $prefix = Get-BrqoshVersionPrefix -Props $Props
    $version = Get-BrqoshReleaseVersion -Prefix $prefix -BuildNumber $BuildNumber
    $notes = Get-BrqoshReleaseNotes -Changelog $Changelog -Version $version
    [pscustomobject]@{ Version = $version; Notes = $notes } | ConvertTo-Json -Compress
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 2
}
