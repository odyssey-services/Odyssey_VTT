param(
    [long] $BuildNumber = 0,
    [int] $RunAttempt = 1,
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH,
    [switch] $PassThru,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$expectedUnityVersion = '6000.4.0f1'
$fallbackUnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'

if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $UnityEditorPath = $fallbackUnityEditorPath
}

function Invoke-Git([string] $Arguments) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git'
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "git $Arguments failed: $($stderr.Trim())"
    }

    return $stdout.Trim()
}

function Assert-CleanTrackedWorktree {
    $status = Invoke-Git 'status --porcelain=v1 --untracked-files=all --ignore-submodules=none'
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw 'Repository has staged, unstaged, submodule, or non-ignored untracked changes; build provenance requires a clean repository state.'
    }
}

function Join-ProcessArguments([string[]] $Arguments) {
    $quoted = @()
    foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            $quoted += '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $quoted += $argument
        }
    }

    return $quoted -join ' '
}

function Invoke-Process([string] $FileName, [string[]] $Arguments, [string] $WorkingDirectory) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FileName
    $startInfo.Arguments = Join-ProcessArguments $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        if (-not [string]::IsNullOrWhiteSpace($stdout)) { Write-Host $stdout.Trim() }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) { Write-Host $stderr.Trim() }
        throw "$FileName failed with exit code $($process.ExitCode)."
    }

    return $stdout
}

function Test-UnityEditorVersion {
    if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
        throw "Required Unity Editor not found: $UnityEditorPath. Pass -UnityEditorPath or set UNITY_EDITOR_PATH to Unity $expectedUnityVersion."
    }

    $output = Invoke-Process $UnityEditorPath @('-version') $repoRoot
    if ($output -notmatch [regex]::Escape($expectedUnityVersion)) {
        throw "Selected Unity Editor must be $expectedUnityVersion. Output: $output"
    }
}

function Get-IdentityOutput([string[]] $Lines, [string] $Name) {
    foreach ($line in $Lines) {
        if ($line -match "^$([regex]::Escape($Name))=(.+)$") {
            return $Matches[1].Trim()
        }
    }

    throw "BuildIdentity generator did not emit $Name."
}

function Write-ArtifactChecksums([string] $BuildRoot) {
    $checksumPath = Join-Path $BuildRoot 'checksums.sha256'
    $rootFull = [System.IO.Path]::GetFullPath($BuildRoot).TrimEnd('\') + '\'
    $rows = @()
    foreach ($file in Get-ChildItem -LiteralPath $BuildRoot -Recurse -File) {
        if ($file.FullName -eq $checksumPath) {
            continue
        }

        $relative = [System.IO.Path]::GetFullPath($file.FullName).Substring($rootFull.Length).Replace('\', '/')
        if ([System.IO.Path]::IsPathRooted($relative) -or $relative.Contains('..')) {
            throw "Unsafe checksum path: $relative"
        }

        $rows += [pscustomobject]@{
            Path = $relative
            Hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $content = (($rows | Sort-Object Path | ForEach-Object { "$($_.Hash)  $($_.Path)" }) -join "`n") + "`n"
    [System.IO.File]::WriteAllText($checksumPath, $content, (New-Object System.Text.UTF8Encoding($false)))
}

function Protect-EvidenceLogText([string] $Text, [string] $RepoRoot) {
    # TC-PLAYER-008: redact categories ADR-010 requires from retained build/Player evidence
    # (local username, machine name, absolute local paths). Only literal, known-sensitive
    # substrings are replaced; diagnostic content (errors, stack traces) is left intact.
    if ($null -eq $Text) { return $Text }
    $result = $Text

    $repoRootFull = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\')
    if (-not [string]::IsNullOrWhiteSpace($repoRootFull)) {
        $result = $result -replace [regex]::Escape($repoRootFull), '<REPO_ROOT>'
        $result = $result -replace [regex]::Escape($repoRootFull.Replace('\', '/')), '<REPO_ROOT>'
    }

    $userProfile = $env:USERPROFILE
    if (-not [string]::IsNullOrWhiteSpace($userProfile)) {
        $userProfileFull = [System.IO.Path]::GetFullPath($userProfile).TrimEnd('\')
        $result = $result -replace [regex]::Escape($userProfileFull), '<USER_PROFILE>'
        $result = $result -replace [regex]::Escape($userProfileFull.Replace('\', '/')), '<USER_PROFILE>'
    }

    $username = [System.Environment]::UserName
    if (-not [string]::IsNullOrWhiteSpace($username) -and $username.Length -ge 2) {
        $result = $result -replace [regex]::Escape($username), '<REDACTED_USER>'
    }

    $machineName = [System.Environment]::MachineName
    if (-not [string]::IsNullOrWhiteSpace($machineName) -and $machineName.Length -ge 2) {
        $result = $result -replace [regex]::Escape($machineName), '<REDACTED_HOST>'
    }

    return $result
}

function Protect-EvidenceLogFile([string] $Path, [string] $RepoRoot) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $raw = [System.IO.File]::ReadAllText($Path)
    $redacted = Protect-EvidenceLogText -Text $raw -RepoRoot $RepoRoot
    [System.IO.File]::WriteAllText($Path, $redacted, (New-Object System.Text.UTF8Encoding($false)))
}

function Assert-NoDistributionOutputs([string] $BuildRoot) {
    # TC-PLAYER-010: fail-closed if the Development-Debug artifact contains anything
    # characteristic of Release/RC/signing/installer/updater/distribution/SBOM/telemetry
    # outputs. This is a thin, extension/naming-based check; it does not duplicate the
    # existing keyword-path containment already enforced by
    # scripts/test-player-smoke.ps1 Assert-ArtifactSafety (release|rc|installer|updater|sbom|telemetry
    # as literal path segments) or by the Editor-side single canonical executable path.
    $rootFull = [System.IO.Path]::GetFullPath($BuildRoot).TrimEnd('\') + '\'
    $forbiddenExtension = '(?i)\.(msi|msix|msixbundle|appx|appxbundle|pfx|p12|snk|cat|spdx|spdx\.json|cdx|cdx\.json)$'
    $forbiddenInstallerExe = '(?i)(^|[\\/])(setup|installer|uninstall|updater)[^\\/]*\.exe$'
    $forbiddenKeyword = '(?i)(sbom|telemetry)'

    foreach ($file in Get-ChildItem -LiteralPath $BuildRoot -Recurse -File) {
        $relative = [System.IO.Path]::GetFullPath($file.FullName).Substring($rootFull.Length).Replace('\', '/')
        if ($relative -match $forbiddenExtension -or $relative -match $forbiddenInstallerExe -or $relative -match $forbiddenKeyword) {
            throw "TC-PLAYER-010 forbidden distribution/signing/telemetry output detected: $relative"
        }
    }
}

function Invoke-SelfTest {
    # Controlled positive/negative proof for TC-PLAYER-008 redaction and TC-PLAYER-010
    # forbidden-output detection, using synthetic fixtures only. No Unity build is run.
    $testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ody-s00-009-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
    try {
        $username = [System.Environment]::UserName
        $machineName = [System.Environment]::MachineName
        $repoRootFull = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\')

        $fixtureLog = Join-Path $testRoot 'fixture-build.log'
        $fixtureText = @"
Build started by $username on $machineName
Loaded project from $repoRootFull
Loaded project from $($repoRootFull.Replace('\','/'))
Created GICache directory at $($env:USERPROFILE.Replace('\','/'))/AppData/LocalLow/Unity/Caches/GiCache
Fatal error: NullReferenceException at Odyssey.Unity.Client.Editor.OdysseyDevelopmentBuild.Build
"@
        [System.IO.File]::WriteAllText($fixtureLog, $fixtureText, (New-Object System.Text.UTF8Encoding($false)))

        $redacted = Protect-EvidenceLogText -Text ([System.IO.File]::ReadAllText($fixtureLog)) -RepoRoot $repoRoot
        if ($redacted -match [regex]::Escape($username)) { throw 'SELFTEST-008 FAIL synthetic username marker survived redaction.' }
        if ($redacted -match [regex]::Escape($machineName)) { throw 'SELFTEST-008 FAIL synthetic machine-name marker survived redaction.' }
        if ($redacted -match [regex]::Escape($repoRootFull)) { throw 'SELFTEST-008 FAIL synthetic absolute repo path marker survived redaction.' }
        if ($redacted -notmatch [regex]::Escape('<REDACTED_USER>') -or $redacted -notmatch [regex]::Escape('<REDACTED_HOST>') -or $redacted -notmatch [regex]::Escape('<REPO_ROOT>') -or $redacted -notmatch [regex]::Escape('<USER_PROFILE>')) {
            throw 'SELFTEST-008 FAIL expected redaction placeholders are missing.'
        }
        if ($redacted -notmatch 'Fatal error: NullReferenceException') {
            throw 'SELFTEST-008 FAIL unrelated diagnostic content was altered by redaction.'
        }
        Write-Host 'SELFTEST-008 PASS synthetic username/machine-name/absolute-path markers removed; diagnostic content preserved.'

        $cleanRoot = Join-Path $testRoot 'clean-artifact'
        New-Item -ItemType Directory -Force -Path $cleanRoot | Out-Null
        Set-Content -LiteralPath (Join-Path $cleanRoot 'Odyssey.exe') -Value 'stub' -NoNewline
        Set-Content -LiteralPath (Join-Path $cleanRoot 'UnityCrashHandler64.exe') -Value 'stub' -NoNewline
        Assert-NoDistributionOutputs $cleanRoot
        Write-Host 'SELFTEST-010 PASS clean Development-Debug artifact layout does not trigger forbidden-output detection.'

        $dirtyRoot = Join-Path $testRoot 'dirty-artifact'
        New-Item -ItemType Directory -Force -Path $dirtyRoot | Out-Null
        Set-Content -LiteralPath (Join-Path $dirtyRoot 'Odyssey.exe') -Value 'stub' -NoNewline
        Set-Content -LiteralPath (Join-Path $dirtyRoot 'Setup.msi') -Value 'stub' -NoNewline
        $threw = $false
        try { Assert-NoDistributionOutputs $dirtyRoot } catch { $threw = $true }
        if (-not $threw) { throw 'SELFTEST-010 FAIL synthetic Setup.msi was not detected.' }
        Write-Host 'SELFTEST-010 PASS synthetic Setup.msi correctly triggers forbidden-output detection.'
    }
    finally {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Push-Location $repoRoot
try {
    if ($SelfTest) {
        Invoke-SelfTest
        return
    }

    if ($BuildNumber -lt 0) { throw 'BuildNumber must be positive when supplied.' }
    if ($BuildNumber -eq 0) { $BuildNumber = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() }
    if ($BuildNumber -le 0) { throw 'BuildNumber must be positive.' }
    if ($RunAttempt -le 0) { throw 'RunAttempt must be positive.' }

    Assert-CleanTrackedWorktree
    Test-UnityEditorVersion

    $head = Invoke-Git 'rev-parse HEAD'
    $branch = Invoke-Git 'branch --show-current'
    if ($head -notmatch '^[0-9a-f]{40}$') { throw 'Git HEAD is not a full SHA.' }
    if ([string]::IsNullOrWhiteSpace($branch)) { $branch = 'detached' }
    $gitRef = "refs/heads/$branch"
    $timestampUtc = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ', [System.Globalization.CultureInfo]::InvariantCulture)

    $identityOutput = & (Join-Path $repoRoot 'scripts/generate-build-identity.ps1') `
        -Channel development `
        -GitRef $gitRef `
        -BuildNumber $BuildNumber `
        -RunAttempt $RunAttempt `
        -TimestampUtc $timestampUtc `
        -Configuration 'Development-Debug' `
        -Platform 'WindowsStandalone' `
        -Architecture 'x86_64' `
        -ScriptingBackend 'Mono' `
        -ApiCompatibility 'NETStandard2.1'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $identityLines = @($identityOutput -split "`r?`n")
    $buildId = Get-IdentityOutput $identityLines 'BuildId'
    $identityJson = Get-IdentityOutput $identityLines 'BuildIdentityJson'
    $buildRoot = Join-Path $repoRoot "artifacts/builds/$buildId/Windows-x64"
    $exePath = Join-Path $buildRoot 'Odyssey.exe'
    if (Test-Path -LiteralPath $buildRoot) {
        throw "Build artifact already exists: $buildRoot. Use a new BuildNumber or RunAttempt."
    }

    New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
    $unityLog = Join-Path $repoRoot "Logs/ODY-S00-009/build-dev-$buildId.log"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $unityLog) | Out-Null
    Invoke-Process $UnityEditorPath @(
        '-batchmode',
        '-quit',
        '-projectPath', $repoRoot,
        '-executeMethod', 'Odyssey.Unity.Client.Editor.OdysseyDevelopmentBuild.Build',
        '-logFile', $unityLog,
        '-odysseyBuildOutput', $exePath
    ) $repoRoot | Out-Null

    Protect-EvidenceLogFile -Path $unityLog -RepoRoot $repoRoot

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Development build did not create Odyssey.exe: $exePath"
    }

    Copy-Item -LiteralPath $identityJson -Destination (Join-Path $buildRoot 'build-identity.json') -Force
    $embeddedIdentity = Join-Path $buildRoot 'Odyssey_Data/StreamingAssets/Odyssey/build-identity.json'
    if (-not (Test-Path -LiteralPath $embeddedIdentity)) {
        throw 'Built Player is missing embedded StreamingAssets BuildIdentity.'
    }
    if ((Get-FileHash -LiteralPath $identityJson -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $embeddedIdentity -Algorithm SHA256).Hash) {
        throw 'Embedded BuildIdentity does not match generated sidecar identity.'
    }

    Write-ArtifactChecksums $buildRoot
    Assert-NoDistributionOutputs $buildRoot

    if ($PassThru) {
        [pscustomobject]@{
            BuildId = $buildId
            BuildRoot = $buildRoot
            ExecutablePath = $exePath
            GitCommitSha = $head
            BuildNumber = $BuildNumber
            RunAttempt = $RunAttempt
        }
    }
    else {
        Write-Host "BuildId=$buildId"
        Write-Host "BuildRoot=$buildRoot"
        Write-Host "ExecutablePath=$exePath"
    }
}
finally {
    Pop-Location
}
