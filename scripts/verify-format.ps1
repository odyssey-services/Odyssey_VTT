Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $artifactsRoot = Join-Path $repoRoot 'artifacts'
    $env:APPDATA = Join-Path $artifactsRoot 'appdata'
    $env:LOCALAPPDATA = Join-Path $artifactsRoot 'localappdata'
    $env:DOTNET_CLI_HOME = Join-Path $artifactsRoot 'nuget-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_CLI_HOME = Join-Path $artifactsRoot 'nuget-home'
    $env:NUGET_PACKAGES = Join-Path $artifactsRoot 'nuget-packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $artifactsRoot 'nuget-http-cache'
    $env:NUGET_PLUGINS_CACHE_PATH = Join-Path $artifactsRoot 'nuget-plugin-cache'

    $textFiles = @(
        Get-ChildItem -Path . -Recurse -File -Include *.cs,*.csproj,*.props,*.json,*.asmdef,*.ps1 |
            Where-Object {
                $_.FullName -notmatch '\\(Library|Temp|Obj|Build|Builds|Logs|UserSettings|artifacts|bin|obj|TestResults|\.git)\\'
            }
    )

    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($file in $textFiles) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -match '\s+$') {
                $relative = Resolve-Path -Relative $file.FullName
                $failures.Add("$relative line $($i + 1) has trailing whitespace.")
            }
        }
    }

    dotnet format .\DotNet\Odyssey.Core.sln --verify-no-changes --verbosity minimal --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format failed with exit code $LASTEXITCODE."
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ }
        exit 1
    }

    Write-Host 'FORMAT-001 PASS repository text formatting checks passed'
}
finally {
    Pop-Location
}
