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

function Write-Utf8NoBom {
    param(
        [string] $Path,
        [string] $Content
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-ErrorRegistryEntries {
    param([string] $RegistryPath)

    $entries = @()
    foreach ($line in Get-Content -LiteralPath $RegistryPath) {
        if ($line -notmatch '^\|\s*`([^`]+)`\s*\|') {
            continue
        }

        $columns = @($line.Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
        if ($columns.Count -ne 11) {
            throw "Malformed ERROR_CODES.md row for $($matches[1]): expected 11 columns, got $($columns.Count)."
        }

        $entries += [pscustomobject]@{
            Code = $matches[1]
            OwnerModule = $columns[1].Trim('`')
            Category = $columns[2].Trim('`')
            SafeReasonCode = $columns[3].Trim('`')
            UserMessageKey = $columns[4].Trim('`')
            RetryDirective = $columns[5].Trim('`')
            IntroducedVersion = $columns[6].Trim('`')
            Status = $columns[7]
            AllowedMetadataKeys = $columns[8]
            SecurityNotes = $columns[9]
            TestReference = $columns[10]
        }
    }

    return @($entries)
}

function Test-UserMessageKey {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt 128 -or -not $Value.StartsWith('errors.')) {
        return $false
    }

    return $Value -match '^errors\.[a-z0-9_]+(\.[a-z0-9_]+)+$'
}

function Get-RegistryMetadataKeys {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Trim() -eq '-') {
        return @()
    }

    return @($Value -split ',' | ForEach-Object { $_.Trim().Trim('`') } | Where-Object { $_ })
}

function Get-ProductionErrorCodes {
    param([string] $RepositoryRoot)

    $codes = @{}
    foreach ($source in Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Packages') -Recurse -File -Filter '*.cs') {
        if ($source.FullName -match '\\Tests\\') {
            continue
        }

        $text = Get-Content -LiteralPath $source.FullName -Raw
        foreach ($match in [regex]::Matches($text, 'public\s+static\s+readonly\s+ErrorCode\s+([A-Za-z0-9_]+)\s*=\s*ErrorCode\.Parse\("([^"]+)"\)')) {
            $codes[$match.Groups[2].Value] = $match.Groups[1].Value
        }
    }

    return $codes
}

function Get-ProductionErrorCodeLiterals {
    param([string] $RepositoryRoot)

    $literals = @{}
    foreach ($source in Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Packages') -Recurse -File -Filter '*.cs') {
        if ($source.FullName -match '\\Tests\\') {
            continue
        }

        $text = Get-Content -LiteralPath $source.FullName -Raw
        foreach ($match in [regex]::Matches($text, 'ErrorCode\.(?:Parse|TryParse)\("([^"]+)"')) {
            $literal = $match.Groups[1].Value
            if (-not $literals.ContainsKey($literal)) {
                $literals[$literal] = New-Object System.Collections.Generic.List[string]
            }

            $literals[$literal].Add((Normalize-RepoPath ($source.FullName.Substring($RepositoryRoot.Length).TrimStart('\', '/'))))
        }
    }

    return $literals
}

function Get-ProductionMetadataPolicy {
    param(
        [string] $RepositoryRoot,
        [hashtable] $ProductionCodes
    )

    $policy = @{}
    foreach ($code in $ProductionCodes.Keys) {
        $policy[$code] = @()
    }

    $policyPath = Join-Path $RepositoryRoot 'Packages/com.odyssey.application/Runtime/Results/ErrorMetadataPolicy.cs'
    if (-not (Test-Path -LiteralPath $policyPath)) {
        return $policy
    }

    $text = Get-Content -LiteralPath $policyPath -Raw
    foreach ($match in [regex]::Matches($text, 'if\s*\(\s*code\s*==\s*ErrorCodes\.([A-Za-z0-9_]+)\s*\)\s*\{(?<body>.*?)\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $constantName = $match.Groups[1].Value
        $code = @($ProductionCodes.GetEnumerator() | Where-Object { $_.Value -eq $constantName } | Select-Object -First 1).Key
        if (-not $code) {
            continue
        }

        $keys = @([regex]::Matches($match.Groups['body'].Value, 'key\s*==\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
        $policy[$code] = @($keys | Sort-Object -Unique)
    }

    return $policy
}

function Test-ErrorCodeRegistry {
    param([string] $RepositoryRoot)

    $registryFailures = New-Object System.Collections.Generic.List[string]
    $registryPath = Join-Path $RepositoryRoot 'docs/errors/ERROR_CODES.md'

    if (-not (Test-Path -LiteralPath $registryPath)) {
        $registryFailures.Add('Missing required path: docs/errors/ERROR_CODES.md')
        return $registryFailures.ToArray()
    }

    try {
        $entries = @(Get-ErrorRegistryEntries $registryPath)
    }
    catch {
        $registryFailures.Add($_.Exception.Message)
        return $registryFailures.ToArray()
    }

    if ($entries.Count -eq 0) {
        $registryFailures.Add('ERROR_CODES.md must contain at least one registered ErrorCode.')
    }

    $codePattern = '^[a-z0-9_]+(\.[a-z0-9_]+){2,}$'
    $categories = @('Validation', 'Authorization', 'RuleViolation', 'NotFound', 'Conflict', 'Precondition', 'Capacity', 'Compatibility', 'Integrity', 'TransientInfrastructure', 'PermanentInfrastructure', 'Cancelled', 'Security', 'Internal')
    $retryDirectives = @('DoNotRetry', 'RetrySameRequest', 'RetryWithBackoff', 'RefreshStateThenRetry', 'ReconnectThenRetry', 'UserActionRequired', 'UpgradeRequired', 'ManualRecoveryRequired')
    $safeReasons = @('InvalidRequest', 'PermissionDenied', 'ActionNotAllowed', 'TargetUnavailable', 'StateChanged', 'ResourceUnavailable', 'CapacityReached', 'ApprovalRequired', 'InteractionExpired', 'VersionUnsupported', 'UpdateRequired', 'DataCorrupted', 'ServiceUnavailable', 'OperationTimedOut', 'OperationCancelled', 'ManualRecoveryRequired', 'UnexpectedError')
    $ownerModules = @('Odyssey.Application', 'Odyssey.Domain', 'Odyssey.Rules', 'Odyssey.Content', 'Odyssey.Persistence', 'Odyssey.Networking', 'Odyssey.Unity.Client')
    $statuses = @('Active', 'Deprecated', 'Reserved')
    $semVerPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
    $metadataKeyPattern = '^[a-z0-9_]+(\.[a-z0-9_]+)*$'
    $seenCodes = New-Object System.Collections.Generic.HashSet[string]

    $catalogPath = Join-Path $RepositoryRoot 'Tests/Metadata/test-catalog.json'
    $catalogText = if (Test-Path -LiteralPath $catalogPath) { Get-Content -LiteralPath $catalogPath -Raw } else { '' }

    foreach ($entry in $entries) {
        if ($entry.Code -notmatch $codePattern) {
            $registryFailures.Add("Malformed ErrorCode in registry: $($entry.Code)")
        }
        if ([string] $entry.Code -and ([string] $entry.Code).Length -gt 96) {
            $registryFailures.Add("ErrorCode exceeds max length 96 in registry: $($entry.Code)")
        }
        if (-not $seenCodes.Add([string] $entry.Code)) {
            $registryFailures.Add("Duplicate ErrorCode in registry: $($entry.Code)")
        }
        if ($entry.Category -notin $categories) {
            $registryFailures.Add("Invalid category for $($entry.Code): $($entry.Category)")
        }
        if ($entry.OwnerModule -notin $ownerModules) {
            $registryFailures.Add("Invalid owner module for $($entry.Code): $($entry.OwnerModule)")
        }
        if ($entry.SafeReasonCode -notin $safeReasons) {
            $registryFailures.Add("Invalid safe reason code for $($entry.Code): $($entry.SafeReasonCode)")
        }
        if (-not (Test-UserMessageKey $entry.UserMessageKey)) {
            $registryFailures.Add("Invalid user message key for $($entry.Code): $($entry.UserMessageKey)")
        }
        if ($entry.RetryDirective -notin $retryDirectives) {
            $registryFailures.Add("Invalid retry directive for $($entry.Code): $($entry.RetryDirective)")
        }
        if ($entry.IntroducedVersion -notmatch $semVerPattern) {
            $registryFailures.Add("Invalid introduced version for $($entry.Code): $($entry.IntroducedVersion)")
        }
        if ($entry.Status -notin $statuses) {
            $registryFailures.Add("Invalid status for $($entry.Code): $($entry.Status)")
        }
        foreach ($metadataKey in @(Get-RegistryMetadataKeys $entry.AllowedMetadataKeys)) {
            if ($metadataKey -notmatch $metadataKeyPattern) {
                $registryFailures.Add("Invalid metadata key for $($entry.Code): $metadataKey")
            }
            if ($metadataKey.Length -gt 48) {
                $registryFailures.Add("Metadata key exceeds max length 48 for $($entry.Code): $metadataKey")
            }
        }
        foreach ($field in @('OwnerModule', 'SafeReasonCode', 'UserMessageKey', 'IntroducedVersion', 'Status', 'AllowedMetadataKeys', 'SecurityNotes', 'TestReference')) {
            if ([string]::IsNullOrWhiteSpace([string] $entry.$field)) {
                $registryFailures.Add("Missing required ERROR_CODES.md field '$field' for $($entry.Code).")
            }
        }

        $references = @([regex]::Matches([string] $entry.TestReference, 'TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-[0-9]{3}') | ForEach-Object { $_.Value })
        if ($references.Count -eq 0) {
            $registryFailures.Add("Registry row has no TestCaseId reference: $($entry.Code).")
        }
        foreach ($reference in $references) {
            if ($catalogText -notmatch [regex]::Escape($reference)) {
                $registryFailures.Add("Registry row references missing catalog TestCaseId $reference for $($entry.Code).")
            }
        }
    }

    $productionCodes = Get-ProductionErrorCodes $RepositoryRoot
    $productionLiterals = Get-ProductionErrorCodeLiterals $RepositoryRoot
    $productionMetadataPolicy = Get-ProductionMetadataPolicy $RepositoryRoot $productionCodes
    $entryByCode = @{}
    foreach ($entry in $entries) {
        $entryByCode[[string] $entry.Code] = $entry
    }

    foreach ($code in $productionCodes.Keys) {
        if (-not $entryByCode.ContainsKey($code)) {
            $registryFailures.Add("Production ErrorCode is missing from docs/errors/ERROR_CODES.md: $code")
            continue
        }

        if ($entryByCode[$code].Status -ne 'Active') {
            $registryFailures.Add("Production ErrorCode uses non-active registry row $code with status $($entryByCode[$code].Status).")
        }
    }

    foreach ($literal in $productionLiterals.Keys) {
        if (-not $entryByCode.ContainsKey($literal)) {
            $registryFailures.Add("Production ErrorCode literal is missing from docs/errors/ERROR_CODES.md: $literal")
            continue
        }

        if ($entryByCode[$literal].Status -ne 'Active') {
            $registryFailures.Add("Production ErrorCode literal uses non-active registry row $literal with status $($entryByCode[$literal].Status).")
        }
    }

    foreach ($entry in $entries) {
        $code = [string] $entry.Code
        if ($entry.Status -eq 'Active' -and -not $productionCodes.ContainsKey($code)) {
            $registryFailures.Add("Registry ErrorCode has no matching production ErrorCode.Parse usage: $($entry.Code)")
        }
        if ($entry.Status -ne 'Active' -and $productionCodes.ContainsKey($code)) {
            $registryFailures.Add("Deprecated/reserved ErrorCode must not have active production usage: $($entry.Code)")
        }

        if ($entry.Status -eq 'Active') {
            $registryKeys = @(Get-RegistryMetadataKeys $entry.AllowedMetadataKeys | Sort-Object -Unique)
            $policyKeys = @()
            if ($productionMetadataPolicy.ContainsKey($code)) {
                $policyKeys = @($productionMetadataPolicy[$code] | Sort-Object -Unique)
            }

            if (($registryKeys -join ',') -ne ($policyKeys -join ',')) {
                $registryFailures.Add("Metadata policy mismatch for $code. Registry [$($registryKeys -join ',')], production [$($policyKeys -join ',')].")
            }
        }
    }

    return $registryFailures.ToArray()
}

function New-RegistryFixture {
    param(
        [string] $FixtureRoot,
        [string] $RegistryRows,
        [string] $ProductionConstants,
        [string] $MetadataPolicyBody,
        [string] $ExtraProductionSource = ''
    )

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Tests/Metadata/test-catalog.json') @"
{
  "testCases": [
    { "testCaseId": "TC-RESULT-002", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "error safe fields" },
    { "testCaseId": "TC-RESULT-003", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "retry vocabulary" },
    { "testCaseId": "TC-RESULT-004", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "bounded safe details" }
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'docs/errors/ERROR_CODES.md') @"
# Odyssey VTT Error Code Registry

| Code | Owner module | Category | Default SafeReasonCode | Default UserMessageKey | Default RetryDirective | Introduced version | Status | Allowed metadata keys | Security notes | Test reference |
|---|---|---|---|---|---|---|---|---|---|---|
$RegistryRows
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs') @"
namespace Odyssey.Application.Results
{
    public static class ErrorCodes
    {
$ProductionConstants
    }
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Packages/com.odyssey.application/Runtime/Results/ErrorMetadataPolicy.cs') @"
namespace Odyssey.Application.Results
{
    internal static class ErrorMetadataPolicy
    {
        internal static bool IsAllowed(ErrorCode code, string key)
        {
$MetadataPolicyBody
            return false;
        }
    }
}
"@

    if (-not [string]::IsNullOrWhiteSpace($ExtraProductionSource)) {
        Write-Utf8NoBom (Join-Path $FixtureRoot 'Packages/com.odyssey.application/Runtime/Results/RegistryLiteralProbe.cs') $ExtraProductionSource
    }
}

function Test-ErrorCodeRegistryFixtures {
    $fixtureFailures = New-Object System.Collections.Generic.List[string]
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odyssey-error-registry-fixture-" + [guid]::NewGuid().ToString('N'))
    $activeValidationRow = '| `application.validation.invalid` | `Odyssey.Application` | `Validation` | `InvalidRequest` | `errors.application.validation_invalid` | `DoNotRetry` | `0.1.0` | Active | `limit.max` | safe | `TC-RESULT-002`, `TC-RESULT-004` |'
    $deprecatedInternalRow = '| `application.internal.unexpected` | `Odyssey.Application` | `Internal` | `UnexpectedError` | `errors.application.unexpected` | `ManualRecoveryRequired` | `0.1.0` | Deprecated | - | safe | `TC-RESULT-002`, `TC-RESULT-003` |'
    $validationConstant = '        public static readonly ErrorCode ApplicationValidationInvalid = ErrorCode.Parse("application.validation.invalid");'
    $internalConstant = '        public static readonly ErrorCode ApplicationInternalUnexpected = ErrorCode.Parse("application.internal.unexpected");'
    $validationMetadataPolicy = @"
            if (code == ErrorCodes.ApplicationValidationInvalid)
            {
                return key == "limit.max";
            }
"@

    try {
        New-RegistryFixture $fixtureRoot "$activeValidationRow`n$deprecatedInternalRow" $validationConstant $validationMetadataPolicy
        $validFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if ($validFailures.Count -ne 0) {
            $fixtureFailures.Add('Registry fixture expected Active production row plus Deprecated no-production row to pass: ' + ($validFailures -join '; '))
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled Deprecated registry row without production code is allowed'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-RegistryFixture $fixtureRoot "$activeValidationRow`n$deprecatedInternalRow" $validationConstant $validationMetadataPolicy @"
namespace Odyssey.Application.Results
{
    internal static class RegistryLiteralProbe
    {
        internal static readonly ErrorCode ActiveLiteral = ErrorCode.Parse("application.validation.invalid");
    }
}
"@
        $activeLiteralFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if ($activeLiteralFailures.Count -ne 0) {
            $fixtureFailures.Add('Registry fixture expected registered Active direct literal usage to pass: ' + ($activeLiteralFailures -join '; '))
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled registered Active literal usage is allowed'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-RegistryFixture $fixtureRoot $activeValidationRow $validationConstant $validationMetadataPolicy @"
namespace Odyssey.Application.Results
{
    internal static class RegistryLiteralProbe
    {
        internal static readonly ErrorCode MissingLiteral = ErrorCode.Parse("application.missing.literal");
    }
}
"@
        $missingLiteralFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($missingLiteralFailures -join "`n") -notmatch 'Production ErrorCode literal is missing') {
            $fixtureFailures.Add('Registry fixture expected unregistered direct ErrorCode.Parse literal to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled unregistered ErrorCode.Parse literal is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-RegistryFixture $fixtureRoot "$activeValidationRow`n$deprecatedInternalRow" $validationConstant $validationMetadataPolicy @"
namespace Odyssey.Application.Results
{
    internal static class RegistryLiteralProbe
    {
        internal static readonly bool DeprecatedLiteral = ErrorCode.TryParse("application.internal.unexpected", out _);
    }
}
"@
        $deprecatedLiteralFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($deprecatedLiteralFailures -join "`n") -notmatch 'Production ErrorCode literal uses non-active registry row') {
            $fixtureFailures.Add('Registry fixture expected direct Deprecated ErrorCode.TryParse literal to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled Deprecated ErrorCode.TryParse literal is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-RegistryFixture $fixtureRoot "$activeValidationRow`n$deprecatedInternalRow" "$validationConstant`n$internalConstant" $validationMetadataPolicy
        $nonActiveUseFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($nonActiveUseFailures -join "`n") -notmatch 'Production ErrorCode uses non-active registry row') {
            $fixtureFailures.Add('Registry fixture expected active production use of Deprecated/Reserved code to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled production use of Deprecated registry row is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $invalidReasonRow = $activeValidationRow -replace 'InvalidRequest', 'SomeRandomReason'
        New-RegistryFixture $fixtureRoot $invalidReasonRow $validationConstant $validationMetadataPolicy
        $invalidReasonFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($invalidReasonFailures -join "`n") -notmatch 'Invalid safe reason code') {
            $fixtureFailures.Add('Registry fixture expected invalid SafeReasonCode to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled invalid SafeReasonCode is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $invalidVersionRow = $activeValidationRow -replace '0\.1\.0', 'ODY-S00-004'
        New-RegistryFixture $fixtureRoot $invalidVersionRow $validationConstant $validationMetadataPolicy
        $invalidVersionFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($invalidVersionFailures -join "`n") -notmatch 'Invalid introduced version') {
            $fixtureFailures.Add('Registry fixture expected non-SemVer Introduced version to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled non-SemVer introduced version is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $leadingZeroVersionRow = $activeValidationRow -replace '0\.1\.0', '00.1.0'
        New-RegistryFixture $fixtureRoot $leadingZeroVersionRow $validationConstant $validationMetadataPolicy
        $leadingZeroVersionFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($leadingZeroVersionFailures -join "`n") -notmatch 'Invalid introduced version') {
            $fixtureFailures.Add('Registry fixture expected leading-zero IntroducedVersion to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled leading-zero introduced version is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $missingMessageRow = $activeValidationRow -replace 'errors\.application\.validation_invalid', '-'
        New-RegistryFixture $fixtureRoot $missingMessageRow $validationConstant $validationMetadataPolicy
        $missingMessageFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($missingMessageFailures -join "`n") -notmatch 'Invalid user message key') {
            $fixtureFailures.Add('Registry fixture expected missing UserMessageKey mapping to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled missing UserMessageKey mapping is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $overlongCode = 'application.' + ('a' * 85) + '.invalid'
        $overlongCodeRow = $activeValidationRow -replace 'application\.validation\.invalid', $overlongCode
        New-RegistryFixture $fixtureRoot $overlongCodeRow $validationConstant $validationMetadataPolicy
        $overlongCodeFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($overlongCodeFailures -join "`n") -notmatch 'ErrorCode exceeds max length 96') {
            $fixtureFailures.Add('Registry fixture expected overlong ErrorCode to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled overlong ErrorCode is rejected'
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        $overlongMetadataKey = 'limit.' + ('a' * 44)
        $overlongMetadataRow = $activeValidationRow -replace 'limit\.max', $overlongMetadataKey
        $overlongMetadataPolicy = $validationMetadataPolicy -replace 'limit\.max', $overlongMetadataKey
        New-RegistryFixture $fixtureRoot $overlongMetadataRow $validationConstant $overlongMetadataPolicy
        $overlongMetadataFailures = @(Test-ErrorCodeRegistry $fixtureRoot)
        if (($overlongMetadataFailures -join "`n") -notmatch 'Metadata key exceeds max length 48') {
            $fixtureFailures.Add('Registry fixture expected overlong metadata key to fail.')
        }
        else {
            Write-Host 'REPO-POLICY-005 PASS controlled overlong metadata key is rejected'
        }
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }

    return $fixtureFailures.ToArray()
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
        'TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md',
        'TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.4.md',
        'TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md',
        'ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md',
        'ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md',
        'ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.9.md',
        'ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md',
        '.gitignore',
        '.gitattributes',
        '.editorconfig',
        '.github/PULL_REQUEST_TEMPLATE.md',
        '.github/workflows/ci.yml',
        'scripts/check-repository-policy.ps1',
        'scripts/generate-build-identity.ps1',
        'scripts/restore.ps1',
        'scripts/verify-format.ps1',
        'scripts/verify-test-structure.ps1',
        'scripts/verify-ci.ps1',
        'scripts/verify-unity-project.ps1',
        'scripts/verify-build-identity.ps1',
        'scripts/test-fast.ps1',
        'scripts/test-unity.ps1',
        'scripts/verify-repository.ps1',
        'version.json',
        'global.json',
        'NuGet.Config',
        'Directory.Build.props',
        'config/compatibility.json',
        'Tests/Metadata/test-catalog.json',
        'config/diagnostics/event-codes.json',
        'DotNet/Odyssey.Core.sln',
        'DotNet/Projects/Odyssey.Domain.csproj',
        'DotNet/Projects/Odyssey.Rules.csproj',
        'DotNet/Projects/Odyssey.Content.csproj',
        'DotNet/Projects/Odyssey.Application.csproj',
        'DotNet/Tests/Odyssey.Tests.Unit/Odyssey.Tests.Unit.csproj',
        'DotNet/Tests/Odyssey.Tests.Domain/Odyssey.Tests.Domain.csproj',
        'DotNet/Tests/Odyssey.Tests.Contracts/Odyssey.Tests.Contracts.csproj',
        'DotNet/Tests/Odyssey.Tests.Architecture/Odyssey.Tests.Architecture.csproj',
        'docs/adr/README.md',
        'docs/tasks/TASK_TEMPLATE.md',
        'docs/tasks/README.md',
        'docs/tasks/SLICE-00_BACKLOG.md',
        'docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md',
        'docs/tasks/completed/ODY-S00-001_Repository_Foundation.md',
        'docs/tasks/completed/ODY-S00-002_Unity_Project_Foundation.md',
        'docs/tasks/completed/ODY-S00-003_Module_and_Test_Skeleton.md',
        'docs/tasks/completed/ODY-S00-004_Identity_Version_and_Result_Primitives.md',
        'docs/tasks/completed/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md',
        'docs/tasks/completed/ODY-S00-006_Runtime_Composition_and_Diagnostic_Shell.md',
        'docs/tasks/completed/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md',
        'docs/tasks/completed/ODY-S00-008_Fast_CI_and_Build_Identity.md',
        'docs/errors/ERROR_CODES.md',
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

    $registryFailures = @(Test-ErrorCodeRegistry $RepositoryRoot)
    if ($registryFailures.Count -gt 0) {
        foreach ($failure in $registryFailures) {
            $failures.Add($failure)
        }
    }

    Write-PolicyResult 'REPO-POLICY-005' ($registryFailures.Count -eq 0) 'ErrorCode registry is complete and machine-checkable'

    if ($registryFailures.Count -eq 0) {
        $registryFixtureFailures = @(Test-ErrorCodeRegistryFixtures)
        foreach ($failure in $registryFixtureFailures) {
            $failures.Add($failure)
        }
    }

    try {
        & (Join-Path $RepositoryRoot 'scripts/verify-ci.ps1')
        if ($LASTEXITCODE -ne 0) {
            $failures.Add('scripts/verify-ci.ps1 failed.')
        }
    }
    catch {
        $failures.Add($_.Exception.Message)
    }

    try {
        & (Join-Path $RepositoryRoot 'scripts/verify-unity-project.ps1')
        if ($LASTEXITCODE -ne 0) {
            $failures.Add('scripts/verify-unity-project.ps1 failed.')
        }
    }
    catch {
        $failures.Add($_.Exception.Message)
    }

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
