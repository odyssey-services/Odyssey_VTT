Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    & .\scripts\check-repository-policy.ps1
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & .\scripts\verify-test-structure.ps1
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $sdkVersion = dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.302') {
        Write-Error "Expected dotnet SDK 10.0.302, got '$sdkVersion'."
        exit 1
    }

    Write-Host 'REPOSITORY-VERIFY PASS repository checks passed'
}
finally {
    Pop-Location
}
