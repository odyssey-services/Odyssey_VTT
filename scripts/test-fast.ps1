Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    & .\scripts\verify-test-structure.ps1
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & .\scripts\restore.ps1
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet build .\DotNet\Odyssey.Core.sln --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resultsDir = Join-Path $repoRoot 'Logs/ODY-S00-008/dotnet'
    $packagesPath = Join-Path $repoRoot 'artifacts/nuget-packages'
    $nugetHome = Join-Path $repoRoot 'artifacts/nuget-home'
    $httpCache = Join-Path $repoRoot 'artifacts/nuget-http-cache'
    $pluginCache = Join-Path $repoRoot 'artifacts/nuget-plugin-cache'

    $env:APPDATA = Join-Path $repoRoot 'artifacts/appdata'
    $env:LOCALAPPDATA = Join-Path $repoRoot 'artifacts/localappdata'
    $env:DOTNET_CLI_HOME = $nugetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_CLI_HOME = $nugetHome
    $env:NUGET_PACKAGES = $packagesPath
    $env:NUGET_HTTP_CACHE_PATH = $httpCache
    $env:NUGET_PLUGINS_CACHE_PATH = $pluginCache

    New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
    Get-ChildItem -LiteralPath $resultsDir -Filter '*.trx' -File -ErrorAction SilentlyContinue | Remove-Item -Force

    dotnet test .\DotNet\Odyssey.Core.sln --no-build --no-restore --logger 'trx;LogFilePrefix=ody-s00-008' --results-directory $resultsDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resultFiles = @(Get-ChildItem -LiteralPath $resultsDir -Filter '*.trx' -File)
    if ($resultFiles.Count -eq 0) {
            Write-Error "No .NET TRX result files were created under Logs/ODY-S00-008/dotnet."
        exit 1
    }

    foreach ($resultFile in $resultFiles) {
        [xml] $result = Get-Content -LiteralPath $resultFile.FullName -Raw
        $counters = $result.TestRun.ResultSummary.Counters
        $total = [int] $counters.total
        $failed = [int] $counters.failed
        $executed = [int] $counters.executed
        if ($total -le 0 -or $executed -le 0) {
            Write-Error "Zero-test .NET result file is not accepted: $($resultFile.FullName)"
            exit 1
        }
        if ($failed -ne 0) {
            Write-Error ".NET result file has $failed failed tests: $($resultFile.FullName)"
            exit 1
        }

        $relative = Resolve-Path -Relative $resultFile.FullName
        Write-Host "TC-DOTNET-001 PASS $relative total=$total executed=$executed failed=$failed"
    }
}
finally {
    Pop-Location
}
