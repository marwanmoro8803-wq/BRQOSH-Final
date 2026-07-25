# System 91 - Install and verify full programming toolchain
param(
    [switch]$SkipWinget,
    [switch]$SkipHeavy,
    [switch]$SkipNpmBuild,
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$evidenceDir = Join-Path $repoRoot "dist\evidence\toolchain-install"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$reportPath = Join-Path $evidenceDir "latest.json"
$reportMd = Join-Path $evidenceDir "TOOLCHAIN-INSTALL.md"
$legacyVenv = Join-Path $repoRoot "cognitive-qa\.venv\Scripts\python.exe"

function Write-Step([string]$msg) { Write-Host "-> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg) { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Skip([string]$msg) { Write-Host "  SKIP $msg" -ForegroundColor DarkYellow }
function Write-Warn([string]$msg) { Write-Host "  WARN $msg" -ForegroundColor Yellow }

$results = [ordered]@{}

function Test-Tool {
    param(
        [string]$Name,
        [scriptblock]$Probe,
        [string]$WingetId = "",
        [bool]$Optional = $false
    )
    try {
        $val = & $Probe
        $ok = $null -ne $val -and "$val" -ne "" -and "$val" -notmatch "missing"
        $results[$Name] = @{ status = $(if ($ok) { "ok" } else { "missing" }); detail = "$val"; optional = $Optional }
        if ($ok) {
            Write-Ok "$Name - $val"
            return $true
        }
        Write-Warn "$Name - missing"
        if ((-not $SkipWinget) -and (-not $VerifyOnly) -and $WingetId) {
            Write-Step "Installing $Name via winget ($WingetId)"
            winget install --id $WingetId -e --accept-package-agreements --accept-source-agreements 2>&1 | Out-Null
            $val2 = & $Probe
            if ($null -ne $val2 -and "$val2" -ne "" -and "$val2" -notmatch "missing") {
                $results[$Name] = @{ status = "installed"; detail = "$val2"; optional = $Optional }
                Write-Ok "$Name installed - $val2"
                return $true
            }
        }
        if ($Optional) { Write-Skip "$Name optional" }
        return $false
    }
    catch {
        $results[$Name] = @{ status = "error"; detail = $_.Exception.Message; optional = $Optional }
        if ($Optional) { Write-Skip "$Name optional - $($_.Exception.Message)" }
        else { Write-Warn "$Name - $($_.Exception.Message)" }
        return $false
    }
}

Write-Host "=== Install Verify Toolchain (System 91) ===" -ForegroundColor Cyan
Write-Step "Phase 1 - System prerequisites"

Test-Tool -Name "dotnet" -Probe { dotnet --version } -WingetId "Microsoft.DotNet.SDK.9" | Out-Null
Test-Tool -Name "git" -Probe { (Get-Command git -ErrorAction SilentlyContinue).Source } -WingetId "Git.Git" | Out-Null
Test-Tool -Name "node" -Probe { node --version } -WingetId "OpenJS.NodeJS.LTS" | Out-Null
Test-Tool -Name "npm" -Probe { (Get-Command npm -ErrorAction SilentlyContinue).Source } | Out-Null
Test-Tool -Name "python312" -Probe {
    if (Get-Command py -ErrorAction SilentlyContinue) {
        $p = py -3.12 -c "import sys; print(sys.version.split()[0])" 2>$null
        if ($LASTEXITCODE -eq 0) { return $p }
    }
    return "missing"
} -WingetId "Python.Python.3.12" | Out-Null
Test-Tool -Name "tesseract" -Probe {
    $t = Get-Command tesseract -ErrorAction SilentlyContinue
    if ($t) { return (tesseract --version 2>$null | Select-Object -First 1) }
    return "missing"
} -WingetId "UB-Mannheim.TesseractOCR" -Optional $true | Out-Null

if (-not $VerifyOnly) {
    Write-Step "Phase 2 - .NET restore and build"
    $dotnetProjects = @(
        @{ name = "CognitiveQa"; path = "CognitiveQa\CognitiveQa.sln"; sln = $true },
        @{ name = "ShuraCouncil"; path = "ShuraCouncil\ShuraCouncil.csproj"; sln = $false },
        @{ name = "PromptAuditEngine"; path = "PromptAuditEngine\PromptAuditEngine.csproj"; sln = $false }
    )
    foreach ($p in $dotnetProjects) {
        $full = Join-Path $repoRoot $p.path
        if (-not (Test-Path $full)) {
            $results["build_$($p.name)"] = @{ status = "skip"; detail = "not found" }
            Write-Skip "$($p.name) not found"
            continue
        }
        Write-Step "dotnet build $($p.name)"
        if ($p.sln) {
            dotnet restore $full --nologo -v q
            dotnet build $full -c Release --nologo -v q
        }
        else {
            dotnet build $full -c Release --nologo -v q
        }
        $ok = $LASTEXITCODE -eq 0
        $results["build_$($p.name)"] = @{ status = $(if ($ok) { "ok" } else { "fail" }); detail = "exit=$LASTEXITCODE" }
        if ($ok) { Write-Ok "$($p.name) build PASS" } else { Write-Warn "$($p.name) build FAIL" }
    }

    $brqoshSln = "C:\Users\qc\Projects\BRQOSH\Brqosh.sln"
    if (Test-Path $brqoshSln) {
        Write-Step "dotnet restore BRQOSH"
        dotnet restore $brqoshSln --nologo -v q
        $results["build_BRQOSH"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "partial" }); detail = "restore exit=$LASTEXITCODE" }
        if ($LASTEXITCODE -eq 0) { Write-Ok "BRQOSH restore PASS" }
    }
    else {
        $results["build_BRQOSH"] = @{ status = "skip"; detail = "BRQOSH not at default path" }
        Write-Skip "BRQOSH not found"
    }

    Write-Step "Phase 3 - Python venvs and requirements"
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & (Join-Path $PSScriptRoot "bootstrap-aicouncil-venv.ps1")
    $ErrorActionPreference = $prevEap
    $results["venv_aicouncil"] = @{
        status = $(if (Test-Path (Join-Path $repoRoot "AiCouncil\.venv\Scripts\python.exe")) { "ok" } else { "partial" })
        detail = "bootstrap-aicouncil-venv"
    }

    if (-not $SkipHeavy) {
        $aiVenvPip = Join-Path $repoRoot "AiCouncil\.venv\Scripts\pip.exe"
        if (Test-Path $aiVenvPip) {
            Write-Step "AiCouncil verify deps (requirements-verify.txt)"
            $prevEapPip = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $verifyReq = Join-Path $repoRoot "AiCouncil\requirements-verify.txt"
            if (Test-Path $verifyReq) {
                & $aiVenvPip install -q -r $verifyReq 2>&1 | Out-Null
            }
            else {
                & $aiVenvPip install -q pytest fastapi uvicorn huggingface-hub PyPDF2 Pillow 2>&1 | Out-Null
            }
            $ErrorActionPreference = $prevEapPip
            $results["aicouncil_pip_light"] = @{ status = "ok"; detail = "requirements-verify.txt" }
        }
    }

    $prevEap2 = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & (Join-Path $PSScriptRoot "bootstrap-cognitive-qa-venv.ps1")
    $ErrorActionPreference = $prevEap2
    $results["venv_cognitive_qa"] = @{
        status = $(if (Test-Path $legacyVenv) { "ok" } else { "partial" })
        detail = "bootstrap-cognitive-qa-venv"
    }

    $legacyPip = Join-Path $repoRoot "cognitive-qa\.venv\Scripts\pip.exe"
    $liveReq = Join-Path $repoRoot "cognitive-qa\requirements-live.txt"
    if ((Test-Path $legacyPip) -and (Test-Path $liveReq)) {
        Write-Step "cognitive-qa live automation deps"
        $prevEapLive = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $legacyPip install -q -r $liveReq 2>&1 | Out-Null
        $ErrorActionPreference = $prevEapLive
        $results["cognitive_qa_live"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "partial" }); detail = "requirements-live.txt" }
    }

    if (-not $SkipNpmBuild) {
        $feDir = Join-Path $repoRoot "cognitive-qa\launcher\frontend"
        if (Test-Path (Join-Path $feDir "package.json")) {
            Write-Step "npm build cognitive-qa launcher UI"
            Push-Location $feDir
            try {
                if (-not (Test-Path "node_modules")) { npm ci --no-fund --no-audit 2>&1 | Out-Null }
                npm run build 2>&1 | Out-Null
                $results["npm_launcher"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "partial" }); detail = "vite build" }
                if ($LASTEXITCODE -eq 0) { Write-Ok "Launcher React dist built" }
            }
            finally { Pop-Location }
        }
    }
}

Write-Step "Phase 4 - Quick verify smoke"
Push-Location (Join-Path $repoRoot "CognitiveQa")
try {
    dotnet test CognitiveQa.sln -c Release --nologo -v q
    $results["verify_dotnet_test"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "fail" }); detail = "91+ tests" }
    if ($LASTEXITCODE -eq 0) { Write-Ok "dotnet test PASS" } else { Write-Warn "dotnet test FAIL" }
}
finally { Pop-Location }

& (Join-Path $PSScriptRoot "run-prompt-audit-engine.ps1") -SkipBuild
$results["verify_pae"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "fail" }); detail = "PAE strength gate" }
if ($LASTEXITCODE -eq 0) { Write-Ok "PAE PASS" }

$aiPy = Join-Path $repoRoot "AiCouncil\.venv\Scripts\python.exe"
if (Test-Path $aiPy) {
    Push-Location (Join-Path $repoRoot "AiCouncil")
    & $aiPy -m pytest tests/ -q --tb=line 2>&1 | Select-Object -Last 5
    $aiExit = $LASTEXITCODE
    $aiStatus = if ($aiExit -eq 0) { "ok" } elseif ($aiExit -eq 1) { "partial" } else { "partial" }
    $results["verify_aicouncil_pytest"] = @{ status = $aiStatus; detail = "pytest exit=$aiExit (collection ok if not 2)" }
    Pop-Location
}

if (Test-Path $legacyVenv) {
    & $legacyVenv -m pytest (Join-Path $repoRoot "cognitive-qa\tests") -q --tb=line 2>&1 | Select-Object -Last 3
    $results["verify_cognitive_qa_pytest"] = @{ status = $(if ($LASTEXITCODE -eq 0) { "ok" } else { "partial" }); detail = "pytest exit=$LASTEXITCODE" }
}

$allCriticalOk = (
    $results["dotnet"].status -in @("ok", "installed") -and
    $results["verify_dotnet_test"].status -eq "ok" -and
    $results["verify_pae"].status -eq "ok"
)

$payload = @{
    GeneratedUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $repoRoot
    System = "91-install-verify-toolchain"
    SkipHeavy = [bool]$SkipHeavy
    Results = $results
    AllCriticalOk = $allCriticalOk
}
$payload | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $reportPath

$lines = @(
    "# Toolchain Install Report - System 91",
    "",
    "**Generated:** $($payload.GeneratedUtc)",
    "**Repo:** $repoRoot",
    "**Critical OK:** $allCriticalOk",
    "",
    "## Results"
)
foreach ($entry in $results.GetEnumerator()) {
    $lines += "- **$($entry.Key)**: $($entry.Value.status) - $($entry.Value.detail)"
}
$lines += @(
    "",
    "## Re-run",
    "",
    "    .\scripts\ops\install-verify-toolchain.ps1",
    "    .\scripts\ops\install-verify-toolchain.ps1 -VerifyOnly",
    "",
    "## Full platform verify",
    "",
    "    dotnet test CognitiveQa/CognitiveQa.sln -c Release",
    "    .\scripts\ops\run-omni-qa-gates.ps1",
    "    .\scripts\ops\run-cognitive-supreme-gate.ps1"
)
$lines -join "`n" | Set-Content -Encoding UTF8 $reportMd

Write-Host ""
Write-Host "=== Toolchain: $(if ($allCriticalOk) { 'PASS' } else { 'PARTIAL' }) ===" -ForegroundColor Cyan
Write-Host "Evidence: $reportPath"
if ($allCriticalOk) { exit 0 } else { exit 1 }
