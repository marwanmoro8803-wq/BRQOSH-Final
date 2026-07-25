# Ops — Compare two audit output directories (M14)
param(
    [Parameter(Mandatory = $true)][string]$DirA,
    [Parameter(Mandatory = $true)][string]$DirB,
    [string]$Output = ""
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cqaNet = Join-Path $repoRoot "CognitiveQa"

Push-Location $cqaNet
try {
    $args = @(
        "run", "--project", "src/CognitiveQa.Cli/CognitiveQa.Cli.csproj",
        "-c", "Release", "--no-build",
        "--", "compare", $DirA, $DirB
    )
    if ($Output) { $args += $Output }

    dotnet @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
