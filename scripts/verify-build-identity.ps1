param(
    [string] $BuildIdentityPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BuildIdentityPath)) {
    $repoRootForSearch = Resolve-Path (Join-Path $PSScriptRoot '..')
    $candidates = @(Get-ChildItem -LiteralPath (Join-Path $repoRootForSearch 'artifacts/build-identity') -Recurse -File -Filter 'build-identity.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
    if ($candidates.Count -eq 0) {
        throw 'No build-identity.json found under artifacts/build-identity.'
    }

    $BuildIdentityPath = $candidates[0].FullName
}

if (-not (Test-Path -LiteralPath $BuildIdentityPath)) {
    throw "BuildIdentity file does not exist: $BuildIdentityPath"
}

$directory = Split-Path -Parent (Resolve-Path -LiteralPath $BuildIdentityPath)
$checksumsPath = Join-Path $directory 'checksums.sha256'
if (-not (Test-Path -LiteralPath $checksumsPath)) {
    throw "Missing checksums.sha256 next to BuildIdentity."
}

$json = Get-Content -LiteralPath $BuildIdentityPath -Raw | ConvertFrom-Json
if ($json.schemaVersion -ne 1) { throw 'BuildIdentity schemaVersion must be 1.' }
if ($json.productName -ne 'Odyssey VTT') { throw 'ProductName mismatch.' }
if ($json.applicationVersion -ne '0.1.0') { throw 'ApplicationVersion must be 0.1.0.' }
if ($json.release -ne $false) { throw 'ODY-S00-008 BuildIdentity must not be Release.' }
if ($json.channel -notin @('local', 'pull_request', 'development')) { throw 'Unsupported channel.' }
if ($json.gitCommitSha -notmatch '^[0-9a-f]{40}$') { throw 'Full SHA is invalid.' }
if ($json.gitShortSha -notmatch '^[0-9a-f]{12,40}$' -or -not ([string]$json.gitCommitSha).StartsWith([string]$json.gitShortSha)) { throw 'Short SHA is invalid.' }
if ($json.buildId -notmatch '^odyssey-[a-z0-9.-]+$') { throw 'BuildId is not filename-safe.' }
if ($json.displayVersion -notmatch '^0\.1\.0-(local|pr|dev)\.') { throw 'DisplayVersion is invalid.' }
if ($json.compatibilityConfigDigest -notmatch '^[0-9a-f]{64}$') { throw 'CompatibilityConfigDigest is invalid.' }
if ($json.contractRegistryDigest -notmatch '^[0-9a-f]{64}$') { throw 'ContractRegistryDigest is invalid.' }

$digest = (Get-FileHash -LiteralPath $BuildIdentityPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = Get-Content -LiteralPath $checksumsPath -Raw
if ($checksumLine -notmatch "^$digest\s+build-identity\.json") {
    throw 'checksums.sha256 does not match build-identity.json.'
}

$generatedJson = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'Assets/StreamingAssets/Odyssey/build-identity.json'
$generatedCsharp = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'Assets/Odyssey/Generated/BuildIdentity.g.cs'
if (-not (Test-Path -LiteralPath $generatedJson)) {
    throw 'Generated StreamingAssets build-identity.json is missing.'
}
if (-not (Test-Path -LiteralPath $generatedCsharp)) {
    throw 'Generated C# BuildIdentity.g.cs is missing.'
}
if ((Get-FileHash -LiteralPath $generatedJson -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digest) {
    throw 'Generated StreamingAssets build-identity.json does not match evidence JSON.'
}
$generatedText = Get-Content -LiteralPath $generatedCsharp -Raw
foreach ($needle in @([string] $json.buildId, [string] $json.displayVersion, [string] $json.gitCommitSha, [string] $json.compatibilityConfigDigest, [string] $json.contractRegistryDigest)) {
    if ($generatedText -notlike "*$needle*") {
        throw "Generated C# BuildIdentity.g.cs is missing value: $needle"
    }
}

Write-Host "TC-BUILDID-009 PASS generated JSON parity verified for $($json.buildId)"
Write-Host "TC-PROVENANCE-002 PASS checksum=$digest"
Write-Host "TC-PROVENANCE-003 PASS BuildId and commit are linked: $($json.buildId) $($json.gitCommitSha)"
