param([string]$SnapshotDir)
$ErrorActionPreference = "Stop"
if (-not $SnapshotDir) {
    $repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    $base = Join-Path $repo "dist\evidence\consciousness-snapshots"
    $SnapshotDir = Get-ChildItem $base -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path $SnapshotDir)) { Write-Error "No snapshot"; exit 1 }
$twin = Join-Path $SnapshotDir "twin.json"
if (-not (Test-Path $twin)) { Write-Error "twin.json missing"; exit 1 }
Write-Host "Restore reference: $SnapshotDir (manual merge if needed)"
exit 0
