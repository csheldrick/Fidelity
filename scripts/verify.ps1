$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

function Invoke-ReplayCase {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [int] $ExpectedExitCode
    )

    $output = (& dotnet run --file $Path --force --verbosity quiet 2>&1 | Out-String)
    $actualExitCode = $LASTEXITCODE
    Write-Host $output.TrimEnd()

    if ($actualExitCode -ne $ExpectedExitCode) {
        throw "$Path returned exit code $actualExitCode; expected $ExpectedExitCode."
    }

    return $output
}

$healthy = Invoke-ReplayCase "examples/HealthyReplay.cs" 0
if ($healthy -notmatch "\[PASS\] required semantic expectations") {
    throw "Healthy replay did not report passing semantic expectations."
}

$lossy = Invoke-ReplayCase "examples/LossyModelReplay.cs" 1
foreach ($requiredLine in @(
    "[PASS] transport/client invocation succeeded",
    "[PASS] typed result produced",
    "[FAIL] required semantic expectation failed",
    'result.status: expected "error", actual <unobservable>',
    'result.error.message: expected "operation_not_allowed", actual <unobservable>',
    'result.error.code: expected -180, actual <unobservable>'
)) {
    if ($lossy -notmatch [regex]::Escape($requiredLine)) {
        throw "Lossy replay did not report the expected Fidelity diagnostic: $requiredLine"
    }
}

$corrected = Invoke-ReplayCase "examples/CorrectedModelReplay.cs" 0
if ($corrected -notmatch "\[PASS\] required semantic expectations") {
    throw "Corrected replay did not report passing semantic expectations."
}

$fixturePath = Join-Path $repoRoot "fixtures/application-error.json"
if (-not (Test-Path -LiteralPath $fixturePath)) {
    throw "The shared application-error fixture is missing."
}
foreach ($demoPath in @(
    (Join-Path $repoRoot "examples/LossyModelReplay.cs"),
    (Join-Path $repoRoot "examples/CorrectedModelReplay.cs")
)) {
    if ((Get-Content -LiteralPath $demoPath -Raw) -notmatch "fixtures/application-error\.json") {
        throw "$demoPath does not replay the shared application-error fixture."
    }
}

foreach ($demoPath in @(
    (Join-Path $repoRoot "examples/LossyModelReplay.cs"),
    (Join-Path $repoRoot "examples/CorrectedModelReplay.cs")
)) {
    $demoSource = Get-Content -LiteralPath $demoPath -Raw
    if ($demoSource -notmatch "RequiredSemantics\.ApplicationError\(expectations\)") {
        throw "$demoPath does not reuse the shared application-error expectation definition."
    }
    if ($demoSource -match "_\s*=>\s*null") {
        throw "$demoPath contains a hard-coded null semantic selector."
    }
}

Write-Host "Repository verification passed."
