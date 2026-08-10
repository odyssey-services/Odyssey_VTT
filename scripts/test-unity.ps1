Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
$logDir = Join-Path $repoRoot 'Logs/ODY-S00-003'

if (-not (Test-Path -LiteralPath $unityExe)) {
    throw "Required Unity Editor not found: $unityExe"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Invoke-Unity([string[]] $Arguments, [string] $Name) {
    $process = Start-Process -FilePath $unityExe -ArgumentList $Arguments -Wait -NoNewWindow -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$Name failed with exit code $($process.ExitCode)."
    }
    Write-Host "$Name PASS exit code 0"
}

Push-Location $repoRoot
try {
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
        Write-Host "$resultName PASS total=$total passed=$passed failed=$failed skipped=$skipped"
    }
}
finally {
    Pop-Location
}
