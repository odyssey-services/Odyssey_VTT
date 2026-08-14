Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$workflowPath = Join-Path $repoRoot '.github/workflows/ci.yml'
if (-not (Test-Path -LiteralPath $workflowPath)) {
    throw 'Missing .github/workflows/ci.yml'
}

$text = Get-Content -LiteralPath $workflowPath -Raw
$required = @(
    'pull_request:',
    'permissions:',
    'contents: read',
    'name: odyssey-fast-ci',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity-project.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1',
    'dotnet build .\DotNet\Odyssey.Core.sln --no-restore',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-build-identity.ps1',
    'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-build-identity.ps1',
    'retention-days: 7',
    'if-no-files-found: error'
)

foreach ($needle in $required) {
    if ($text -notlike "*$needle*") {
        throw "Workflow is missing required text: $needle"
    }
}

$approvedActions = @(
    'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803',
    'actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1',
    'actions/upload-artifact@330a01c490aca151604b8cf639adc76d48f6c5d4'
)
$usesMatches = @([regex]::Matches($text, 'uses:\s*([^\s]+)') | ForEach-Object { $_.Groups[1].Value })
foreach ($uses in $usesMatches) {
    if ($uses -notin $approvedActions) {
        throw "Workflow uses unapproved or unpinned action: $uses"
    }
}

$forbidden = @(
    'pull_request_target',
    'self-hosted',
    'game-ci',
    'gameci',
    'UNITY_LICENSE',
    'UNITY_EMAIL',
    'UNITY_PASSWORD',
    'secrets.',
    'unity -batchmode',
    'Unity.exe',
    'continue-on-error: true',
    'actions/cache',
    'actions/download-artifact',
    'release',
    'create-release'
)
foreach ($needle in $forbidden) {
    if ($text -match [regex]::Escape($needle)) {
        throw "Workflow contains forbidden text: $needle"
    }
}

Write-Host 'TC-CI-001 PASS workflow invokes repository policy entry point'
Write-Host 'TC-CI-002 PASS workflow invokes formatting entry point'
Write-Host 'TC-CI-003 PASS workflow invokes test-structure entry point'
Write-Host 'TC-CI-004 PASS workflow invokes static Unity/source validation'
Write-Host 'TC-CI-005 PASS workflow invokes restore, build, and fast tests'
Write-Host 'TC-CI-006 PASS workflow validates Unity source state without Unity compile claim'
Write-Host 'TC-CI-008 PASS workflow permissions are minimal and no secrets are referenced'
Write-Host 'TC-CI-009 PASS workflow actions are pinned to approved immutable SHAs'
Write-Host 'TC-CI-010 PASS artifact retention is bounded'
Write-Host 'TC-CI-011 PASS required check name is stable: odyssey-fast-ci'
