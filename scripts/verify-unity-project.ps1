Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Read-Json([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing JSON file: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$projectVersionPath = Join-Path $repoRoot 'ProjectSettings/ProjectVersion.txt'
$projectVersion = Get-Content -LiteralPath $projectVersionPath -Raw
if ($projectVersion -notmatch 'm_EditorVersion:\s*6000\.4\.0f1' -or $projectVersion -notmatch 'm_EditorVersionWithRevision:\s*6000\.4\.0f1\s+\(8cf496087c8f\)') {
    throw 'ProjectSettings/ProjectVersion.txt must pin Unity 6000.4.0f1 (8cf496087c8f).'
}

$manifest = Read-Json (Join-Path $repoRoot 'Packages/manifest.json')
$dependencies = $manifest.dependencies
$expectedPackages = @{
    'com.unity.render-pipelines.high-definition' = '17.4.0'
    'com.unity.inputsystem' = '1.19.0'
    'com.unity.test-framework' = '1.6.0'
    'com.unity.nuget.newtonsoft-json' = '3.2.2'
}
foreach ($entry in $expectedPackages.GetEnumerator()) {
    if (-not ($dependencies.PSObject.Properties.Name -contains $entry.Key)) {
        throw "Unity manifest missing required package $($entry.Key)."
    }
    $actualVersion = [string] $dependencies.PSObject.Properties[$entry.Key].Value
    if ($actualVersion -ne $entry.Value) {
        throw "Unity package $($entry.Key) must be $($entry.Value), got $actualVersion."
    }
}

foreach ($forbidden in @('com.unity.render-pipelines.universal', 'com.unity.2d.sprite', 'com.unity.2d.tilemap')) {
    if ($dependencies.PSObject.Properties.Name -contains $forbidden) {
        throw "Forbidden Unity package present: $forbidden"
    }
}

$lock = Read-Json (Join-Path $repoRoot 'Packages/packages-lock.json')
foreach ($entry in $expectedPackages.GetEnumerator()) {
    if (-not ($lock.dependencies.PSObject.Properties.Name -contains $entry.Key)) {
        throw "Unity package lock missing required package $($entry.Key)."
    }
    $actualVersion = [string] $lock.dependencies.PSObject.Properties[$entry.Key].Value.version
    if ($actualVersion -ne $entry.Value) {
        throw "Unity package lock $($entry.Key) must be $($entry.Value), got $actualVersion."
    }
}

foreach ($path in @(
    'Assets/Odyssey/Generated/.gitkeep',
    'Assets/Odyssey/Generated/Odyssey.Unity.Client.asmref',
    'Assets/StreamingAssets/Odyssey/.gitkeep',
    'scripts/generate-build-identity.ps1',
    'scripts/verify-build-identity.ps1'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $path))) {
        throw "Missing Unity/build identity support path: $path"
    }
}

$gitignore = Get-Content -LiteralPath (Join-Path $repoRoot '.gitignore') -Raw
foreach ($ignored in @('Assets/Odyssey/Generated/BuildIdentity.g.cs', 'Assets/StreamingAssets/Odyssey/build-identity.json')) {
    if ($gitignore -notlike "*$ignored*") {
        throw ".gitignore must ignore generated path: $ignored"
    }
}

Write-Host 'TC-CI-006 PASS static Unity project/package/toolchain source validation passed; Unity Editor compile is not claimed'
