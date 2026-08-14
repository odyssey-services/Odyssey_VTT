param(
    [long] $BuildNumber = 0,
    [int] $RunAttempt = 1,
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH,
    [switch] $PassThru
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
    & git -C $repoRoot diff --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Tracked worktree has unstaged changes; build provenance requires a clean tracked tree.'
    }

    & git -C $repoRoot diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Index has staged changes; build provenance requires a clean tracked index.'
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

Push-Location $repoRoot
try {
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
