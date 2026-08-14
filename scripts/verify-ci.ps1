Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [string] $Text,
        [string] $Needle,
        [string] $Message
    )

    if ($Text -notlike "*$Needle*") {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string] $Text,
        [string] $Needle,
        [string] $Message
    )

    if ($Text -match [regex]::Escape($Needle)) {
        throw $Message
    }
}

function Get-JobBlock {
    param(
        [string] $Text,
        [string] $JobId
    )

    $pattern = "(?ms)^  $([regex]::Escape($JobId)):\r?\n(?<body>.*?)(?=^  [A-Za-z0-9_-]+:\r?\n|\z)"
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Missing required CI job: $JobId"
    }

    return $match.Value
}

function Assert-JobContains {
    param(
        [string] $Text,
        [string] $JobId,
        [string] $Needle
    )

    $block = Get-JobBlock -Text $Text -JobId $JobId
    Assert-Contains -Text $block -Needle $Needle -Message "Job '$JobId' is missing required text: $Needle"
}

function Test-CiWorkflow {
    param([string] $WorkflowPath)

    if (-not (Test-Path -LiteralPath $WorkflowPath)) {
        throw "Missing workflow: $WorkflowPath"
    }

    $text = Get-Content -LiteralPath $WorkflowPath -Raw
    $normalized = $text -replace "`r`n", "`n"

    if ($normalized -notmatch "(?m)^name: ci$") {
        throw 'Workflow name must be exactly ci.'
    }

    if ($normalized -notmatch "(?ms)^on:\n  pull_request:\n    branches:\n      - main\n  push:\n    branches:\n      - main\n") {
        throw 'Workflow must contain only the required pull_request/main and push/main trigger block.'
    }

    if ($normalized -notmatch "(?ms)^permissions:\n  contents: read\n") {
        throw 'Workflow must use top-level permissions: contents: read.'
    }

    $jobsSectionMatch = [regex]::Match($normalized, '(?ms)^jobs:\n(?<body>.*)\z')
    if (-not $jobsSectionMatch.Success) {
        throw 'Workflow must contain a jobs block.'
    }

    $jobsSection = $jobsSectionMatch.Groups['body'].Value
    $jobs = @([regex]::Matches($jobsSection, '(?m)^  ([A-Za-z0-9_-]+):\s*$') | ForEach-Object { $_.Groups[1].Value })
    $requiredJobs = @(
        'repository-policy-format-structure',
        'dotnet-restore-build-test',
        'unity-project-package-static',
        'buildidentity-provenance'
    )
    if ($jobs.Count -ne $requiredJobs.Count) {
        throw "Workflow must define exactly $($requiredJobs.Count) jobs; found $($jobs.Count)."
    }

    foreach ($job in $requiredJobs) {
        if ($job -notin $jobs) {
            throw "Workflow is missing required job: $job"
        }

        Assert-JobContains -Text $normalized -JobId $job -Needle "name: $job"
        Assert-JobContains -Text $normalized -JobId $job -Needle 'runs-on: windows-2022'
        Assert-JobContains -Text $normalized -JobId $job -Needle 'timeout-minutes:'
        Assert-JobContains -Text $normalized -JobId $job -Needle 'uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803'

        $block = Get-JobBlock -Text $normalized -JobId $job
        if ($block -match '(?m)^    if:') {
            throw "Job '$job' must not use a job-level if condition."
        }
    }

    Assert-JobContains -Text $normalized -JobId 'repository-policy-format-structure' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1'
    Assert-JobContains -Text $normalized -JobId 'repository-policy-format-structure' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1'
    Assert-JobContains -Text $normalized -JobId 'repository-policy-format-structure' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1'
    Assert-JobContains -Text $normalized -JobId 'repository-policy-format-structure' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-ci.ps1'

    Assert-JobContains -Text $normalized -JobId 'dotnet-restore-build-test' -Needle 'uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1'
    Assert-JobContains -Text $normalized -JobId 'dotnet-restore-build-test' -Needle 'dotnet-version: 10.0.302'
    Assert-JobContains -Text $normalized -JobId 'dotnet-restore-build-test' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1'
    Assert-JobContains -Text $normalized -JobId 'dotnet-restore-build-test' -Needle 'dotnet build .\DotNet\Odyssey.Core.sln --no-restore'
    Assert-JobContains -Text $normalized -JobId 'dotnet-restore-build-test' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1'

    Assert-JobContains -Text $normalized -JobId 'unity-project-package-static' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity-project.ps1'
    $unityBlock = Get-JobBlock -Text $normalized -JobId 'unity-project-package-static'
    foreach ($needle in @('Unity.exe', 'unity -batchmode', 'game-ci', 'gameci', 'UNITY_LICENSE', 'UNITY_EMAIL', 'UNITY_PASSWORD', 'compile')) {
        Assert-NotContains -Text $unityBlock -Needle $needle -Message "Static Unity job contains forbidden text: $needle"
    }

    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle "if (`$eventName -eq 'pull_request')"
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-Channel pull_request'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle "-PullRequestNumber `$pullRequestNumber"
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle "elseif (`$eventName -eq 'push')"
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-Channel development'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-GitRef $gitRef'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-BuildNumber $buildNumber'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-RunAttempt $runAttempt'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '-TimestampUtc $timestamp'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-build-identity.ps1'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle '$checkoutSha = git rev-parse HEAD'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'if ($identity.gitCommitSha -ne $checkoutSha)'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'uses: actions/upload-artifact@330a01c490aca151604b8cf639adc76d48f6c5d4'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'name: odyssey-build-identity'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'artifacts/build-identity/**/build-identity.json'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'artifacts/build-identity/**/checksums.sha256'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'retention-days: 7'
    Assert-JobContains -Text $normalized -JobId 'buildidentity-provenance' -Needle 'if-no-files-found: error'

    $usesMatches = @([regex]::Matches($normalized, 'uses:\s*([^\s]+)') | ForEach-Object { $_.Groups[1].Value })
    $approvedActions = @(
        'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803',
        'actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1',
        'actions/upload-artifact@330a01c490aca151604b8cf639adc76d48f6c5d4'
    )
    foreach ($uses in $usesMatches) {
        if ($uses -notin $approvedActions) {
            throw "Workflow uses unapproved or unpinned action: $uses"
        }
    }

    $forbidden = @(
        'pull_request_target',
        'self-hosted',
        'windows-latest',
        'game-ci',
        'gameci',
        'UNITY_LICENSE',
        'UNITY_EMAIL',
        'UNITY_PASSWORD',
        'secrets.',
        'Unity.exe',
        'unity -batchmode',
        'continue-on-error',
        'actions/cache',
        'actions/download-artifact',
        'create-release',
        'upload-release-asset',
        'GITHUB_TOKEN: write'
    )
    foreach ($needle in $forbidden) {
        Assert-NotContains -Text $normalized -Needle $needle -Message "Workflow contains forbidden text: $needle"
    }
}

function Invoke-NegativeFixture {
    param(
        [string] $Name,
        [scriptblock] $Mutate
    )

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("odyssey-ci-fixture-$([Guid]::NewGuid()).yml")
    try {
        $base = Get-Content -LiteralPath $script:workflowPath -Raw
        $mutated = & $Mutate $base
        Set-Content -LiteralPath $tmp -Value $mutated -NoNewline -Encoding UTF8

        try {
            Test-CiWorkflow -WorkflowPath $tmp
        }
        catch {
            Write-Host "TC-CI-010 PASS controlled invalid workflow rejected: $Name"
            return
        }

        throw "Controlled invalid workflow was accepted: $Name"
    }
    finally {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$script:workflowPath = Join-Path $repoRoot '.github/workflows/ci.yml'
Test-CiWorkflow -WorkflowPath $script:workflowPath

Invoke-NegativeFixture -Name 'old single odyssey-fast-ci job' -Mutate {
    @'
name: Odyssey Fast CI

on:
  pull_request:
    branches:
      - main

permissions:
  contents: read

jobs:
  odyssey-fast-ci:
    name: odyssey-fast-ci
    runs-on: windows-latest
    timeout-minutes: 20
    steps:
      - name: Checkout
        uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803
'@
}
Invoke-NegativeFixture -Name 'missing push trigger' -Mutate {
    param($Text)
    $Text -replace "(?ms)\n  push:\n    branches:\n      - main\n", "`n"
}
Invoke-NegativeFixture -Name 'windows-latest runner' -Mutate {
    param($Text)
    $Text -replace 'windows-2022', 'windows-latest'
}
Invoke-NegativeFixture -Name 'missing required job' -Mutate {
    param($Text)
    $Text -replace "(?ms)\n  unity-project-package-static:\n.*?(?=\n  buildidentity-provenance:)", "`n"
}
Invoke-NegativeFixture -Name 'floating Action tag' -Mutate {
    param($Text)
    $Text -replace 'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803', 'actions/checkout@v4'
}
Invoke-NegativeFixture -Name 'continue-on-error' -Mutate {
    param($Text)
    $Text -replace 'run: powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verify-format.ps1', "run: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1`n        continue-on-error: true"
}
Invoke-NegativeFixture -Name 'unapproved Unity Action' -Mutate {
    param($Text)
    $Text -replace 'actions/upload-artifact@330a01c490aca151604b8cf639adc76d48f6c5d4', 'game-ci/unity-builder@v4'
}
Invoke-NegativeFixture -Name 'artifact retention too large' -Mutate {
    param($Text)
    $Text -replace 'retention-days: 7', 'retention-days: 30'
}
Invoke-NegativeFixture -Name 'required job skip condition' -Mutate {
    param($Text)
    $Text -replace "repository-policy-format-structure:\n    name:", "repository-policy-format-structure:`n    if: `${{ always() }}`n    name:"
}

Write-Host 'TC-CI-001 PASS required pull_request/main and push/main triggers are present'
Write-Host 'TC-CI-002 PASS workflow uses top-level contents: read'
Write-Host 'TC-CI-003 PASS workflow actions are pinned to approved immutable SHAs'
Write-Host 'TC-CI-004 PASS repository policy, formatting, test-structure, and CI verifier scripts are wired to the owning job'
Write-Host 'TC-CI-005 PASS .NET restore, build, and fast test scripts are wired to the owning job'
Write-Host 'TC-CI-006 PASS static Unity version/package check does not run Unity'
Write-Host 'TC-CI-007 PASS BuildIdentity/provenance job is wired for PR and push/main evidence'
Write-Host 'TC-CI-008 PASS workflow avoids pull_request_target, write token permissions, and fork secrets'
Write-Host 'TC-CI-009 PASS workflow avoids GameCI and Unity secrets'
Write-Host 'TC-CI-011 PASS unavailable toolchain or non-zero script cannot false-green'
Write-Host 'TC-CI-012 PASS stable check names, bounded time/retention, and no required-job skip trick are enforced'
