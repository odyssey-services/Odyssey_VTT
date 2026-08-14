param(
    [ValidateSet('local', 'pull_request', 'development')]
    [string] $Channel = 'local',
    [string] $GitRef = 'heads/local',
    [long] $BuildNumber = 1,
    [long] $PullRequestNumber = 0,
    [int] $RunAttempt = 1,
    [string] $TimestampUtc,
    [string] $Configuration = 'Development-Debug',
    [string] $Platform = 'WindowsStandalone',
    [string] $Architecture = 'x86_64',
    [string] $ScriptingBackend = 'Mono',
    [string] $ApiCompatibility = 'NETStandard2.1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($TimestampUtc)) {
    $TimestampUtc = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffffffZ', [System.Globalization.CultureInfo]::InvariantCulture)
}

dotnet run --project (Join-Path $repoRoot 'DotNet/Tools/Odyssey.BuildIdentity/Odyssey.BuildIdentity.csproj') -- `
    --root $repoRoot `
    --channel $Channel `
    --git-ref $GitRef `
    --build-number $BuildNumber `
    --pull-request-number $PullRequestNumber `
    --run-attempt $RunAttempt `
    --timestamp-utc $TimestampUtc `
    --configuration $Configuration `
    --platform $Platform `
    --architecture $Architecture `
    --scripting-backend $ScriptingBackend `
    --api-compatibility $ApiCompatibility
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
