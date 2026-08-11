param(
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$logDir = Join-Path $repoRoot 'Logs/ODY-S00-005'
$fallbackUnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'

if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $UnityEditorPath = $fallbackUnityEditorPath
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

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

function Invoke-Unity([string[]] $Arguments, [string] $Name) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $UnityEditorPath
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = Join-ProcessArguments $Arguments

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void] $process.Start()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "$Name failed with exit code $($process.ExitCode)."
    }
    Write-Host "TC-UNITY-ASM-001 PASS $Name exit code 0"
}

function Test-UnityEditorVersion {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $UnityEditorPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = '-version'

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void] $process.Start()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $versionOutput = ($standardOutput + "`n" + $standardError).Trim()
    if ($process.ExitCode -ne 0) {
        throw "Unity Editor version check failed with exit code $($process.ExitCode). Output: $versionOutput"
    }
    if ([string]::IsNullOrWhiteSpace($versionOutput)) {
        throw 'Unity Editor version check produced no output.'
    }

    $match = [regex]::Match($versionOutput, '6000\.4\.0f1')
    if (-not $match.Success) {
        throw "Selected Unity Editor version must be 6000.4.0f1. Output: $versionOutput"
    }

    Write-Host "TC-UNITY-ASM-001 EditorVersion PASS selected=$($match.Value)"
}

Push-Location $repoRoot
try {
    $projectVersionPath = Join-Path $repoRoot 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $projectVersionPath)) {
        throw "ProjectVersion.txt not found: $projectVersionPath"
    }

    $projectVersion = Get-Content -LiteralPath $projectVersionPath -Raw
    if ($projectVersion -notmatch 'm_EditorVersion:\s*6000\.4\.0f1' -or $projectVersion -notmatch 'm_EditorVersionWithRevision:\s*6000\.4\.0f1\s+\(8cf496087c8f\)') {
        throw 'ProjectSettings/ProjectVersion.txt does not match exact Unity 6000.4.0f1 (8cf496087c8f).'
    }

    if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
        throw "Required Unity Editor not found: $UnityEditorPath. Pass -UnityEditorPath or set UNITY_EDITOR_PATH to Unity 6000.4.0f1."
    }

    Test-UnityEditorVersion

    Invoke-Unity @(
        '-batchmode',
        '-quit',
        '-projectPath', $repoRoot,
        '-logFile', (Join-Path $logDir 'unity-compile.log')
    ) 'Unity batch compile'

    Invoke-Unity @(
        '-batchmode',
        '-projectPath', $repoRoot,
        '-runTests',
        '-testPlatform', 'EditMode',
        '-testResults', (Join-Path $logDir 'editmode-results.xml'),
        '-logFile', (Join-Path $logDir 'unity-editmode.log')
    ) 'Unity EditMode tests'

    Invoke-Unity @(
        '-batchmode',
        '-projectPath', $repoRoot,
        '-runTests',
        '-testPlatform', 'PlayMode',
        '-testResults', (Join-Path $logDir 'playmode-results.xml'),
        '-logFile', (Join-Path $logDir 'unity-playmode.log')
    ) 'Unity PlayMode tests'

    foreach ($resultName in @('editmode-results.xml', 'playmode-results.xml')) {
        $resultPath = Join-Path $logDir $resultName
        if (-not (Test-Path -LiteralPath $resultPath)) {
            throw "Unity test result file was not created: $resultPath"
        }

        [xml] $result = Get-Content -LiteralPath $resultPath -Raw
        $total = [int] $result.'test-run'.total
        $failed = [int] $result.'test-run'.failed
        $passed = [int] $result.'test-run'.passed
        $skipped = [int] $result.'test-run'.skipped
        if ($total -le 0) {
            throw "Unity test result file has zero tests: $resultPath"
        }
        if ($failed -ne 0) {
            throw "Unity test result file has $failed failed tests: $resultPath"
        }
        Write-Host "TC-UNITY-TEST-001 PASS $resultName total=$total passed=$passed failed=$failed skipped=$skipped"
    }
}
finally {
    Pop-Location
}
