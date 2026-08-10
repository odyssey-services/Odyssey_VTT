Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $nugetConfig = Join-Path $repoRoot 'NuGet.Config'
    $packagesPath = Join-Path $repoRoot 'artifacts/nuget-packages'
    $nugetHome = Join-Path $repoRoot 'artifacts/nuget-home'
    $httpCache = Join-Path $repoRoot 'artifacts/nuget-http-cache'
    $pluginCache = Join-Path $repoRoot 'artifacts/nuget-plugin-cache'
    $appData = Join-Path $repoRoot 'artifacts/appdata'
    $localAppData = Join-Path $repoRoot 'artifacts/localappdata'

    if (-not (Test-Path -LiteralPath $nugetConfig)) {
        throw "Repository NuGet.Config not found: $nugetConfig"
    }

    New-Item -ItemType Directory -Force -Path $packagesPath | Out-Null
    New-Item -ItemType Directory -Force -Path $nugetHome | Out-Null
    New-Item -ItemType Directory -Force -Path $httpCache | Out-Null
    New-Item -ItemType Directory -Force -Path $pluginCache | Out-Null
    New-Item -ItemType Directory -Force -Path $appData | Out-Null
    New-Item -ItemType Directory -Force -Path $localAppData | Out-Null

    $env:APPDATA = $appData
    $env:LOCALAPPDATA = $localAppData
    $env:DOTNET_CLI_HOME = $nugetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_CLI_HOME = $nugetHome
    $env:NUGET_PACKAGES = $packagesPath
    $env:NUGET_HTTP_CACHE_PATH = $httpCache
    $env:NUGET_PLUGINS_CACHE_PATH = $pluginCache

    dotnet restore .\DotNet\Odyssey.Core.sln --configfile $nugetConfig --packages $packagesPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
