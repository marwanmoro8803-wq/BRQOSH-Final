# Fix mojibake from add-cloud-code-skill-footers.ps1 UTF-8 write
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$files = Get-ChildItem (Join-Path $repoRoot ".cursor\skills") -Recurse -Filter "SKILL.md"
$count = 0
$utf8 = New-Object System.Text.UTF8Encoding $false

foreach ($f in $files) {
    $c = [System.IO.File]::ReadAllText($f.FullName)
    $orig = $c
    $c = $c -replace 'â€"', '—'
    $c = $c -replace 'â€”', '—'
    $c = $c -replace 'آ·', '·'
    if ($c -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $c, $utf8)
        $count++
        Write-Host "fixed $($f.Name)"
    }
}
Write-Host "Fixed $count files"
