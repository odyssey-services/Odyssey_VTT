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

    $globalJsonPath = Join-Path $repoRoot 'global.json'
    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    $configuredVersion = [string] $globalJson.sdk.version
    $rollForward = [string] $globalJson.sdk.rollForward
    $allowPrerelease = [bool] $globalJson.sdk.allowPrerelease

    if ($configuredVersion -ne '10.0.302' -or $rollForward -ne 'latestPatch' -or $allowPrerelease -ne $false) {
        Write-Error "Invalid global.json SDK config: version=$configuredVersion rollForward=$rollForward allowPrerelease=$allowPrerelease."
        exit 1
    }

    $sdkVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'dotnet --version failed.'
        exit 1
    }

    if ($sdkVersion -match '-') {
        Write-Error "Selected dotnet SDK must be stable. Configured=$configuredVersion selected=$sdkVersion."
        exit 1
    }

    $configured = [version] $configuredVersion
    $selected = [version] $sdkVersion
    $configuredFeatureBand = [math]::Floor($configured.Build / 100)
    $selectedFeatureBand = [math]::Floor($selected.Build / 100)

    if ($selected -lt $configured -or $selected.Major -ne $configured.Major -or $selected.Minor -ne $configured.Minor -or $selectedFeatureBand -ne $configuredFeatureBand) {
        Write-Error "Selected dotnet SDK is outside allowed global.json roll-forward. Configured=$configuredVersion selected=$sdkVersion."
        exit 1
    }

    Write-Host "REPOSITORY-VERIFY SDK configured=$configuredVersion selected=$sdkVersion rollForward=$rollForward allowPrerelease=$allowPrerelease"

    Write-Host 'REPOSITORY-VERIFY PASS repository checks passed'
}
finally {
    Pop-Location
}
