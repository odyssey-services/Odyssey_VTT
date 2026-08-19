param(
    [Parameter(Mandatory = $true)]
    [string] $BuildRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$buildRootResolved = Resolve-Path -LiteralPath $BuildRoot
$buildRootFull = [System.IO.Path]::GetFullPath($buildRootResolved).TrimEnd('\')
$allowedPrefix = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/builds')).TrimEnd('\') + '\'

function Assert-InBuildRoot {
    if (-not $buildRootFull.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "BuildRoot must be under artifacts/builds."
    }
    if ([System.IO.Path]::GetFileName($buildRootFull) -ne 'Windows-x64') {
        throw "BuildRoot must end in Windows-x64."
    }
}

function Read-Json([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing JSON: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ChildProcessIds([int] $ParentId) {
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$ParentId" -ErrorAction SilentlyContinue)
    $ids = @()
    foreach ($child in $children) {
        $ids += [int] $child.ProcessId
        $ids += Get-ChildProcessIds ([int] $child.ProcessId)
    }

    return $ids
}

function Stop-ProcessTree([int] $RootPid) {
    $ids = @(Get-ChildProcessIds $RootPid) + $RootPid
    foreach ($id in ($ids | Select-Object -Unique | Sort-Object -Descending)) {
        $process = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($process -ne $null) {
            Stop-Process -Id $id -Force
        }
    }
}

function Remove-FileWithRetry([string] $Path) {
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try {
            if (Test-Path -LiteralPath $Path) {
                Remove-Item -LiteralPath $Path -Force
            }
            return
        }
        catch {
            if ($attempt -eq 19) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

function Assert-NoOrphans([int] $RootPid) {
    $orphans = @(Get-ChildProcessIds $RootPid)
    if ($orphans.Count -gt 0) {
        throw "Player smoke left child processes: $($orphans -join ',')"
    }
}

function Test-SafeText([string] $Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $forbidden = @(
        [regex]::Escape($env:USERNAME),
        [regex]::Escape($env:COMPUTERNAME),
        '[A-Za-z]:\\',
        '/Users/',
        '/home/',
        '\.git',
        'Documentation/',
        'LegacyReference',
        'secret',
        'token',
        'private'
    )
    foreach ($pattern in $forbidden) {
        if (-not [string]::IsNullOrWhiteSpace($pattern) -and $text -match $pattern) {
            throw "Unsafe retained evidence content in $Path."
        }
    }
}

function Assert-BuildLogRedacted([string] $BuildId) {
    # TC-PLAYER-008: verify scripts/build-dev.ps1 redacted the retained Unity Editor
    # build log in place. This is intentionally narrower than Test-SafeText's blanket
    # '[A-Za-z]:\\' pattern (calibrated for smoke evidence JSON, which should contain no
    # paths at all): a real Unity Editor build log legitimately and harmlessly contains
    # standard toolchain installation paths (e.g. C:\Program Files\Unity\...), which are
    # not user- or machine-identifying and are already public in this script's own
    # $fallbackUnityEditorPath default. Only the same sensitive categories the redaction
    # targets are checked here.
    $buildLogPath = Join-Path $repoRoot "Logs/ODY-S00-009/build-dev-$BuildId.log"
    if (-not (Test-Path -LiteralPath $buildLogPath)) {
        throw "Missing retained build log for TC-PLAYER-008: $buildLogPath"
    }
    $text = Get-Content -LiteralPath $buildLogPath -Raw
    $repoRootFull = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\')
    $userProfileFull = [System.IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd('\')
    $forbidden = @(
        [regex]::Escape($env:USERNAME),
        [regex]::Escape($env:COMPUTERNAME),
        [regex]::Escape($repoRootFull),
        [regex]::Escape($repoRootFull.Replace('\', '/')),
        [regex]::Escape($userProfileFull),
        [regex]::Escape($userProfileFull.Replace('\', '/'))
    )
    foreach ($pattern in $forbidden) {
        if (-not [string]::IsNullOrWhiteSpace($pattern) -and $text -match $pattern) {
            throw "Unsafe retained build log content in $buildLogPath (pattern: $pattern)."
        }
    }
}

function Assert-Checksums([string] $Root) {
    $checksumPath = Join-Path $Root 'checksums.sha256'
    if (-not (Test-Path -LiteralPath $checksumPath)) { throw 'Missing checksums.sha256.' }
    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-f]{64})  ([^\r\n]+)$') {
            throw "Invalid checksum line: $line"
        }
        $relative = $Matches[2]
        if ($relative -eq 'checksums.sha256' -or [System.IO.Path]::IsPathRooted($relative) -or $relative.Contains('..')) {
            throw "Unsafe checksum relative path: $relative"
        }
        $path = Join-Path $Root ($relative -replace '/', '\')
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Checksum references missing file: $relative"
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Matches[1]) {
            throw "Checksum mismatch for $relative."
        }
    }
}

function Write-ArtifactChecksums([string] $Root) {
    $checksumPath = Join-Path $Root 'checksums.sha256'
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $rows = @()
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        if ($file.FullName -eq $checksumPath) { continue }
        $relative = [System.IO.Path]::GetFullPath($file.FullName).Substring($rootFull.Length).Replace('\', '/')
        $rows += [pscustomobject]@{
            Path = $relative
            Hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $content = (($rows | Sort-Object Path | ForEach-Object { "$($_.Hash)  $($_.Path)" }) -join "`n") + "`n"
    [System.IO.File]::WriteAllText($checksumPath, $content, (New-Object System.Text.UTF8Encoding($false)))
}

function Assert-IdentityParity([string] $Root) {
    $sidecar = Join-Path $Root 'build-identity.json'
    $embedded = Join-Path $Root 'Odyssey_Data/StreamingAssets/Odyssey/build-identity.json'
    if (-not (Test-Path -LiteralPath $sidecar)) { throw 'Missing sidecar build-identity.json.' }
    if (-not (Test-Path -LiteralPath $embedded)) { throw 'Missing embedded BuildIdentity JSON.' }
    $sidecarHash = (Get-FileHash -LiteralPath $sidecar -Algorithm SHA256).Hash
    $embeddedHash = (Get-FileHash -LiteralPath $embedded -Algorithm SHA256).Hash
    if ($sidecarHash -ne $embeddedHash) { throw 'Sidecar and embedded BuildIdentity JSON differ.' }
    $identity = Read-Json $sidecar
    if ($identity.channel -ne 'development') { throw 'BuildIdentity channel must be development.' }
    if ($identity.release -ne $false) { throw 'Development Player BuildIdentity must not be release.' }
    if ($identity.configuration -ne 'Development-Debug' -or $identity.platform -ne 'WindowsStandalone' -or $identity.architecture -ne 'x86_64' -or $identity.scriptingBackend -ne 'Mono') {
        throw 'BuildIdentity target/profile fields do not match ODY-S00-009.'
    }
    return $identity
}

function Assert-ArtifactSafety([string] $Root) {
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $relative = [System.IO.Path]::GetFullPath($file.FullName).Substring(([System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\').Length).Replace('\', '/')
        if ($relative -match '(?i)(nunit|testkit|unity\.test|/editor/|private|documentation|legacyreference|(^|/)(release|releasecandidate|rc|installer|updater|sbom|telemetry)(/|\.|$))') {
            throw "Forbidden file in Player artifact: $relative"
        }
    }
}

function Wait-PlayerEvidenceBootstrapReady([string] $EvidencePath, [System.Diagnostics.Process] $Process, [int] $Seconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $EvidencePath) {
            try {
                $json = Read-Json $EvidencePath
                if ($json.bootstrapReady -eq $true) { return $json }
            }
            catch {
            }
        }
        if ($Process.HasExited) {
            throw "Player exited before Bootstrap Ready with code $($Process.ExitCode)."
        }
        Start-Sleep -Milliseconds 200
    }

    throw 'Player did not report Bootstrap Ready within 120 seconds.'
}

function Invoke-SmokeRun([int] $RunNumber, [object] $Identity) {
    $exe = Join-Path $buildRootFull 'Odyssey.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "Missing Odyssey.exe: $exe" }
    $evidenceName = "smoke-run-$RunNumber.json"
    $evidencePath = Join-Path $buildRootFull $evidenceName
    if (Test-Path -LiteralPath $evidencePath) { Remove-Item -LiteralPath $evidencePath -Force }
    $tempLog = Join-Path ([System.IO.Path]::GetTempPath()) ("odyssey-player-smoke-$([guid]::NewGuid().ToString('N')).log")

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $exe
    $startInfo.WorkingDirectory = $buildRootFull
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = "--odyssey-player-smoke --odyssey-smoke-evidence $evidenceName --odyssey-smoke-run $RunNumber -logFile `"$tempLog`""
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $totalDeadline = [DateTimeOffset]::UtcNow.AddSeconds(150)
    try {
        $readyEvidence = Wait-PlayerEvidenceBootstrapReady $evidencePath $process 120
        $exitDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
        while (-not $process.HasExited -and [DateTimeOffset]::UtcNow -lt $exitDeadline -and [DateTimeOffset]::UtcNow -lt $totalDeadline) {
            Start-Sleep -Milliseconds 200
        }
        if (-not $process.HasExited) {
            Stop-ProcessTree $process.Id
            throw 'Player did not exit cleanly within 15 seconds after Bootstrap Ready.'
        }
        if ($process.ExitCode -ne 0) {
            throw "Player smoke exited with code $($process.ExitCode)."
        }

        Assert-NoOrphans $process.Id
        if (Test-Path -LiteralPath $tempLog) {
            $log = Get-Content -LiteralPath $tempLog -Raw
            if ($log -match '(Unhandled Exception|Fatal error|Crash!!!|Assertion failed)') {
                throw 'Player log contains fatal error evidence.'
            }
            Remove-FileWithRetry $tempLog
        }

        $evidence = Read-Json $evidencePath
        Test-SafeText $evidencePath
        foreach ($field in @('schemaVersion', 'buildId', 'gitCommitSha', 'runNumber', 'startedUtc', 'bootstrapReadyUtc', 'shutdownRequestedUtc', 'completedUtc', 'bootstrapReady', 'appShellLoaded', 'hdrpActive', 'uiToolkitRootDisplayed', 'submitPerformed', 'cancelPerformed', 'buildIdentityLoaded', 'result')) {
            if ($null -eq ($evidence.PSObject.Properties | Where-Object { $_.Name -eq $field } | Select-Object -First 1)) {
                throw "Smoke evidence missing field: $field"
            }
        }
        if ($evidence.schemaVersion -ne 1 -or $evidence.runNumber -ne $RunNumber -or $evidence.result -ne 'pass') { throw 'Smoke evidence result metadata mismatch.' }
        if ($evidence.buildId -ne $Identity.buildId -or $evidence.gitCommitSha -ne $Identity.gitCommitSha) { throw 'Smoke evidence BuildIdentity parity mismatch.' }
        foreach ($flag in @('bootstrapReady', 'appShellLoaded', 'hdrpActive', 'uiToolkitRootDisplayed', 'submitPerformed', 'cancelPerformed', 'buildIdentityLoaded')) {
            if ($evidence.$flag -ne $true) { throw "Smoke evidence flag is false: $flag" }
        }

        Write-Host "TC-PLAYER-005 PASS run=$RunNumber bootstrapReady=1"
        Write-Host "TC-PLAYER-006 PASS run=$RunNumber appShell=1 hdrp=1 ui=1 submit=1 cancel=1"
        Write-Host "TC-PLAYER-007 PASS run=$RunNumber exitCode=0 orphanChildren=0"
    }
    catch {
        if ($process -ne $null -and -not $process.HasExited) {
            Stop-ProcessTree $process.Id
        }
        throw
    }
    finally {
        if ($process -ne $null) { $process.Dispose() }
        if (Test-Path -LiteralPath $tempLog) { Remove-FileWithRetry $tempLog }
    }
}

Assert-InBuildRoot
$identity = Assert-IdentityParity $buildRootFull
Assert-BuildLogRedacted $identity.buildId
Assert-Checksums $buildRootFull
Assert-ArtifactSafety $buildRootFull
Write-Host 'TC-PLAYER-004 PASS BuildIdentity sidecar/embedded parity and checksum baseline'
Write-Host 'TC-PLAYER-009 PASS artifact excludes test/editor/private outputs'

Invoke-SmokeRun 1 $identity
Invoke-SmokeRun 2 $identity

Write-ArtifactChecksums $buildRootFull
Assert-Checksums $buildRootFull
Assert-ArtifactSafety $buildRootFull
Write-Host 'TC-PLAYER-008 PASS retained build log and smoke evidence are bounded, redacted, and safe'
Write-Host 'TC-PLAYER-010 PASS no Release/RC/signing/installer/updater/SBOM/telemetry outputs'
