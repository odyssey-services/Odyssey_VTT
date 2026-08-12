param(
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$logDir = Join-Path $repoRoot 'Logs/ODY-S00-007'
$artifactDir = Join-Path $repoRoot 'artifacts/serialization-aot-smoke'
$fallbackUnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'

if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $UnityEditorPath = $fallbackUnityEditorPath
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

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

function Invoke-Process([string] $FileName, [string[]] $Arguments, [string] $Name) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FileName
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = Join-ProcessArguments $Arguments

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void] $process.Start()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "$Name failed with exit code $($process.ExitCode)."
    }
    Write-Host "$Name PASS exit code 0"
}

Push-Location $repoRoot
try {
    if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
        throw "Required Unity Editor not found: $UnityEditorPath."
    }

    Invoke-Process $UnityEditorPath @(
        '-batchmode',
        '-quit',
        '-projectPath', $repoRoot,
        '-executeMethod', 'Odyssey.Unity.Client.Editor.Serialization.SerializationAotBuild.Build',
        '-logFile', (Join-Path $logDir 'serialization-aot-build.log')
    ) 'TC-SER-022 serialization-aot-smoke build'

    $playerPath = Join-Path $artifactDir 'serialization-aot-smoke.exe'
    if (-not (Test-Path -LiteralPath $playerPath)) {
        throw "serialization-aot-smoke player was not created: $playerPath"
    }

    Invoke-Process $playerPath @('-batchmode', '-nographics', '-logFile', (Join-Path $logDir 'serialization-aot-player.log')) 'TC-DIAG-042 serialization-aot-smoke player'

    $expected = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'Tests/Fixtures/Serialization/golden-vectors.json') | ConvertFrom-Json
    $playerLog = Get-Content -Raw -LiteralPath (Join-Path $logDir 'serialization-aot-player.log')
    $match = [regex]::Match($playerLog, 'serialization-aot-smoke PASS payloadHash=([0-9a-f]{64}) fingerprint=(fp_[0-9a-f]{64}) diagnosticHash=([0-9a-f]{64}) manifestHash=([0-9a-f]{64})')
    if (-not $match.Success) {
        throw 'serialization-aot-smoke did not emit the exact vector marker.'
    }

    if ($match.Groups[1].Value -ne $expected.syntheticPayloadV2Sha256) { throw 'TC-SER-022 payload hash mismatch.' }
    if ($match.Groups[2].Value -ne $expected.commandFingerprint) { throw 'TC-SER-022 command fingerprint mismatch.' }
    if ($match.Groups[3].Value -ne $expected.diagnosticLogEventV1Sha256) { throw 'TC-DIAG-042 diagnostic hash mismatch.' }
    if ($match.Groups[4].Value -ne $expected.odcampManifestV1Sha256) { throw 'TC-SER-022 manifest hash mismatch.' }
    Write-Host 'TC-SER-022/TC-DIAG-042 serialization-aot-smoke exact vector comparison PASS'
}
finally {
    Pop-Location
}
