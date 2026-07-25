# Ops — Full gate: build + test + audit + optional Shura (M16 — delegates to supreme)
param(
    [string]$TargetSource = "CognitiveQa",
    [switch]$SkipShura,
    [switch]$ArchiveEvidence,
    [switch]$VerifyTests
)

$supremeParams = @{ TargetSource = $TargetSource }
if ($SkipShura) { $supremeParams.SkipShura = $true }
& (Join-Path $PSScriptRoot "run-cognitive-supreme-gate.ps1") @supremeParams
exit $LASTEXITCODE
