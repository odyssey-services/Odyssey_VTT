[CmdletBinding()]
param(
    [string] $RepositoryRoot,
    [string] $TrackedFileList
)

$ErrorActionPreference = 'Stop'

function Write-PolicyResult {
    param(
        [string] $Id,
        [bool] $Passed,
        [string] $Message
    )

    $status = if ($Passed) { 'PASS' } else { 'FAIL' }
    Write-Host "$Id $status $Message"
}

function Normalize-RepoPath {
    param([string] $Path)
    return ($Path -replace '\\', '/').TrimStart('./')
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepositoryRoot = Split-Path -Parent $scriptRoot
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
Push-Location $RepositoryRoot
try {
    $failures = New-Object System.Collections.Generic.List[string]

    $requiredPaths = @(
        'README.md',
        'LICENSE',
        'CONTRIBUTING.md',
        'SECURITY.md',
        'THIRD_PARTY_NOTICES.md',
        'AGENTS.md',
        'PLANS.md',
        'TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md',
        'ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md',
        '.gitignore',
        '.gitattributes',
        '.editorconfig',
        '.github/PULL_REQUEST_TEMPLATE.md',
        'scripts/check-repository-policy.ps1',
        'docs/adr/README.md',
        'docs/tasks/TASK_TEMPLATE.md',
        'docs/tasks/README.md',
        'docs/tasks/SLICE-00_BACKLOG.md',
        'docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md',
        'docs/tasks/completed/ODY-S00-001_Repository_Foundation.md',
        'docs/tasks/active/ODY-S00-002_Unity_Project_Foundation.md',
        'docs/plans/README.md',
        'docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md'
    )

    foreach ($i in 1..10) {
        $pattern = 'docs/adr/ADR-{0:D3}_*' -f $i
        if (-not (Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File | Where-Object {
            (Normalize-RepoPath ($_.FullName.Substring($RepositoryRoot.Length).TrimStart('\', '/'))) -like $pattern
        })) {
            $failures.Add("Missing ADR pattern: $pattern")
        }
    }

    foreach ($path in $requiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot $path))) {
            $failures.Add("Missing required path: $path")
        }
    }

    Write-PolicyResult 'REPO-POLICY-001' ($failures.Count -eq 0) 'required repository files and directories exist'

    if ($TrackedFileList) {
        $trackedFiles = Get-Content -LiteralPath $TrackedFileList | ForEach-Object { Normalize-RepoPath $_ } | Where-Object { $_ }
    }
    elseif (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git')) {
        $trackedFiles = git ls-files | ForEach-Object { Normalize-RepoPath $_ } | Where-Object { $_ }
    }
    else {
        $trackedFiles = Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File -Force |
            Where-Object { $_.FullName -notmatch '\\\.git\\' } |
            ForEach-Object { Normalize-RepoPath ($_.FullName.Substring($RepositoryRoot.Length).TrimStart('\', '/')) }
    }

    $forbiddenPatterns = @(
        '^Documentation/',
        '^Legacy/',
        '^LegacyReference/',
        '(^|/)DOCUMENTATION_ALIGNMENT_CHANGELOG_',
        '(^|/)Odyssey_VTT_New_Chat_Handoff_',
        '(^|/)bundle_manifest\.json$',
        '(^|/)(00_Project_Vision|01_Product_Requirements|02_MVP_Scope|03_Domain_Model|04_Odyssey_Rules_Engine|05_Persistence|06_Networking|07_Permissions|08_Scenes|09_Dice|10_Characters|11_Content|12_Combat|13_Audio|15_Legacy|16_Test_Strategy|17_Roadmap)',
        '\.(zip|7z|rar|tar|gz)$',
        '(^|/)(Library|Temp|Obj|Build|Builds|Logs|UserSettings|MemoryCaptures|Recordings|artifacts)/',
        '(^|/)(\.env|\.secrets|secrets/)',
        '\.(pem|pfx|p12|key|crt|cer)$'
    )

    $forbiddenMatches = @()
    foreach ($file in $trackedFiles) {
        foreach ($pattern in $forbiddenPatterns) {
            if ($file -match $pattern) {
                $forbiddenMatches += $file
                break
            }
        }
    }

    if ($forbiddenMatches.Count -gt 0) {
        $failures.Add('Forbidden tracked paths: ' + (($forbiddenMatches | Sort-Object -Unique) -join ', '))
    }

    Write-PolicyResult 'REPO-POLICY-002' ($forbiddenMatches.Count -eq 0) 'forbidden private/archive/secret/generated tracked patterns are absent'

    $lfsSamples = @('sample.psd', 'sample.wav')
    $lfsFailures = @()
    foreach ($sample in $lfsSamples) {
        $filter = git check-attr filter -- $sample
        if ($LASTEXITCODE -ne 0 -or $filter -notmatch 'filter: lfs') {
            $lfsFailures += "$sample => $filter"
        }
    }

    if ($lfsFailures.Count -gt 0) {
        $failures.Add('LFS attribute failures: ' + ($lfsFailures -join '; '))
    }

    Write-PolicyResult 'REPO-POLICY-003' ($lfsFailures.Count -eq 0) 'approved binary candidates use Git LFS attributes'

    $textSamples = @('sample.cs', 'sample.md', 'sample.json', 'sample.meta', 'sample.asset', 'sample.prefab', 'sample.unity', 'sample.uxml', 'sample.uss')
    $textFailures = @()
    foreach ($sample in $textSamples) {
        $filter = git check-attr filter -- $sample
        if ($LASTEXITCODE -ne 0 -or $filter -match 'filter: lfs') {
            $textFailures += "$sample => $filter"
        }
    }

    if ($textFailures.Count -gt 0) {
        $failures.Add('Text attribute failures: ' + ($textFailures -join '; '))
    }

    Write-PolicyResult 'REPO-POLICY-004' ($textFailures.Count -eq 0) 'source/Markdown/JSON/Unity YAML/meta/UI text are not globally forced into LFS'

    if ($failures.Count -gt 0) {
        Write-Host ''
        Write-Host 'Repository policy failures:'
        foreach ($failure in $failures) {
            Write-Host "- $failure"
        }
        exit 1
    }

    Write-Host ''
    Write-Host 'Repository policy check passed.'
}
finally {
    Pop-Location
}
