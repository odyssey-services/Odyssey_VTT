param(
    [string] $RootPath,
    [switch] $SkipNegativeFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RootPath)) {
    $RootPath = Resolve-Path (Join-Path $PSScriptRoot '..')
}
else {
    $RootPath = Resolve-Path $RootPath
}

$modulePackages = [ordered]@{
    'Odyssey.Domain' = 'com.odyssey.domain'
    'Odyssey.Rules' = 'com.odyssey.rules'
    'Odyssey.Content' = 'com.odyssey.content'
    'Odyssey.Application' = 'com.odyssey.application'
    'Odyssey.Persistence' = 'com.odyssey.persistence'
    'Odyssey.Networking' = 'com.odyssey.networking'
}

$allowed = @{
    'Odyssey.Domain' = @()
    'Odyssey.Rules' = @('Odyssey.Domain')
    'Odyssey.Content' = @('Odyssey.Domain', 'Odyssey.Rules')
    'Odyssey.Application' = @('Odyssey.Domain', 'Odyssey.Rules', 'Odyssey.Content')
    'Odyssey.Persistence' = @('Odyssey.Domain', 'Odyssey.Content', 'Odyssey.Application')
    'Odyssey.Networking' = @('Odyssey.Domain', 'Odyssey.Content', 'Odyssey.Application')
    'Odyssey.Unity.Client' = @('Odyssey.Domain', 'Odyssey.Rules', 'Odyssey.Content', 'Odyssey.Application', 'Odyssey.Persistence', 'Odyssey.Networking')
    'Odyssey.Unity.Client.Editor' = @('Odyssey.Unity.Client', 'Odyssey.Domain', 'Odyssey.Rules', 'Odyssey.Content', 'Odyssey.Application', 'Odyssey.Persistence', 'Odyssey.Networking')
}

$coreBridgeModules = @('Odyssey.Domain', 'Odyssey.Rules', 'Odyssey.Content', 'Odyssey.Application')
$requiredTestCaseIds = @(
    'TC-ARCH-001',
    'TC-ARCH-002',
    'TC-DOTNET-001',
    'TC-UNITY-ASM-001',
    'TC-UNITY-TEST-001',
    'TC-REPO-001',
    'TC-ID-001',
    'TC-ID-002',
    'TC-VERSION-001',
    'TC-VERSION-002',
    'TC-RESULT-001',
    'TC-RESULT-002',
    'TC-RESULT-003',
    'TC-RESULT-004',
    'TC-CMD-001',
    'TC-CMD-002',
    'TC-CMD-003',
    'TC-CMD-004',
    'TC-CMD-005',
    'TC-CMD-006',
    'TC-EVENT-001',
    'TC-CLOCK-001',
    'TC-CLOCK-002',
    'TC-CLOCK-003',
    'TC-RNG-001',
    'TC-RNG-002',
    'TC-RNG-003',
    'TC-RNG-004',
    'TC-RNG-005'
)
$testProjects = @('Odyssey.Tests.Unit', 'Odyssey.Tests.Domain', 'Odyssey.Tests.Contracts', 'Odyssey.Tests.Architecture')
$baselinePackageVersion = '0.1.0'
$testPackageVersions = [ordered]@{
    'Microsoft.NET.Test.Sdk' = @{
        Property = 'MicrosoftNETTestSdkVersion'
        Version = '18.8.1'
    }
    'NUnit' = @{
        Property = 'NUnitVersion'
        Version = '4.6.1'
    }
    'NUnit3TestAdapter' = @{
        Property = 'NUnit3TestAdapterVersion'
        Version = '6.2.0'
    }
}

function Get-RelativePath([string] $Path) {
    $rootFull = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + '\'
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length).Replace('\', '/')
    }
    return $pathFull.Replace('\', '/')
}

function Write-Utf8NoBom([string] $Path, [string] $Content) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Read-JsonFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing JSON file: $(Get-RelativePath $Path)"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Malformed JSON: $(Get-RelativePath $Path)"
    }
}

function Read-XmlFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing XML file: $(Get-RelativePath $Path)"
    }

    try {
        [xml] $xml = Get-Content -LiteralPath $Path -Raw
        return $xml
    }
    catch {
        throw "Malformed XML: $(Get-RelativePath $Path)"
    }
}

function Test-JsonProperty([object] $Json, [string] $Name) {
    return $null -ne ($Json.PSObject.Properties | Where-Object { $_.Name -eq $Name } | Select-Object -First 1)
}

function Assert-SetEquals([System.Collections.Generic.List[string]] $Errors, [string] $Subject, [string[]] $Actual, [string[]] $Expected) {
    $actualJoined = (@($Actual) | Sort-Object) -join ','
    $expectedJoined = (@($Expected) | Sort-Object) -join ','
    if ($actualJoined -ne $expectedJoined) {
        $Errors.Add("$Subject dependencies mismatch. Expected [$expectedJoined], actual [$actualJoined].")
    }
}

function Test-StableExactVersion([string] $Version) {
    return $Version -match '^[0-9]+(\.[0-9]+){2}$'
}

function Get-PackageReferences([string] $AssemblyName) {
    $packageName = $modulePackages[$AssemblyName]
    $json = Read-JsonFile (Join-Path $RootPath "Packages/$packageName/package.json")

    if ($json.name -ne $packageName) {
        throw "Package name mismatch in Packages/$packageName/package.json: expected $packageName, got $($json.name)."
    }
    if ([string] $json.version -ne $baselinePackageVersion -or -not (Test-StableExactVersion ([string] $json.version))) {
        throw "Package version mismatch in Packages/$packageName/package.json: expected stable exact $baselinePackageVersion, got $($json.version)."
    }

    $references = @()
    if (Test-JsonProperty $json 'dependencies') {
        $dependencyProperties = @($json.dependencies.PSObject.Properties)
        foreach ($dependencyProperty in $dependencyProperties) {
            $dependency = $dependencyProperty.Name
            if ([string]::IsNullOrWhiteSpace($dependency)) {
                continue
            }
            $moduleName = ($modulePackages.GetEnumerator() | Where-Object { $_.Value -eq $dependency }).Key
            if (-not $moduleName) {
                throw "Unexpected package dependency '$dependency' in Packages/$packageName/package.json."
            }
            $dependencyVersion = [string] $dependencyProperty.Value
            $targetPackageName = $modulePackages[$moduleName]
            $targetPackage = Read-JsonFile (Join-Path $RootPath "Packages/$targetPackageName/package.json")
            $targetVersion = [string] $targetPackage.version
            if ($dependencyVersion -ne $targetVersion -or -not (Test-StableExactVersion $dependencyVersion)) {
                throw "Package dependency version mismatch in Packages/$packageName/package.json: $dependency=$dependencyVersion, target $targetPackageName version=$targetVersion."
            }
            $references += $moduleName
        }
    }

    return @($references | Sort-Object)
}

function Get-AsmdefReferences([string] $AssemblyName, [string] $AsmdefPath, [bool] $ProductionAssembly) {
    $json = Read-JsonFile $AsmdefPath
    if ($json.name -ne $AssemblyName) {
        throw "Assembly name mismatch in $(Get-RelativePath $AsmdefPath): expected $AssemblyName, got $($json.name)."
    }

    if ($ProductionAssembly) {
        if ($json.autoReferenced -ne $false) {
            throw "Production asmdef must set autoReferenced=false: $(Get-RelativePath $AsmdefPath)."
        }
        if ($json.allowUnsafeCode -ne $false) {
            throw "Production asmdef must set allowUnsafeCode=false: $(Get-RelativePath $AsmdefPath)."
        }
        if ($json.overrideReferences -ne $false) {
            throw "Production asmdef must set overrideReferences=false: $(Get-RelativePath $AsmdefPath)."
        }
        if ($json.noEngineReferences -ne $true) {
            throw "Production asmdef must set noEngineReferences=true: $(Get-RelativePath $AsmdefPath)."
        }
    }

    $references = @()
    foreach ($reference in @($json.references)) {
        if ($reference -like 'GUID:*') {
            throw "GUID asmdef reference is not allowed in $(Get-RelativePath $AsmdefPath)."
        }
        if ($reference -notin $allowed.Keys) {
            throw "Unexpected asmdef reference '$reference' in $(Get-RelativePath $AsmdefPath)."
        }
        $references += $reference
    }
    return @($references | Sort-Object)
}

function Test-UnityTestAsmdef([System.Collections.Generic.List[string]] $Errors, [string] $AssemblyName, [string] $AsmdefPath, [bool] $MustBeEditorOnly) {
    try {
        $json = Read-JsonFile $AsmdefPath
        if ($json.name -ne $AssemblyName) {
            $Errors.Add("Unity test asmdef name mismatch in $(Get-RelativePath $AsmdefPath).")
        }
        if ('TestAssemblies' -notin @($json.optionalUnityReferences)) {
            $Errors.Add("Unity test asmdef is not marked test-only: $(Get-RelativePath $AsmdefPath).")
        }
        if ($MustBeEditorOnly -and 'Editor' -notin @($json.includePlatforms)) {
            $Errors.Add("EditMode asmdef must be Editor-only: $(Get-RelativePath $AsmdefPath).")
        }
    }
    catch {
        $Errors.Add($_.Exception.Message)
    }
}

function Get-CsprojReferences([string] $ProjectPath) {
    $xml = Read-XmlFile $ProjectPath
    $references = @()
    foreach ($reference in @($xml.SelectNodes('//ProjectReference'))) {
        $include = $reference.Include
        $name = [System.IO.Path]::GetFileNameWithoutExtension($include)
        if ($name -notin $coreBridgeModules) {
            throw "Unexpected ProjectReference '$include' in $(Get-RelativePath $ProjectPath)."
        }
        $references += $name
    }
    return @($references | Sort-Object)
}

function Test-Cycles([hashtable] $Graph, [System.Collections.Generic.List[string]] $Errors) {
    $visiting = New-Object System.Collections.Generic.HashSet[string]
    $visited = New-Object System.Collections.Generic.HashSet[string]

    function Visit([string] $Node, [string[]] $Stack) {
        if ($visiting.Contains($Node)) {
            $Errors.Add("Cycle detected: $(($Stack + $Node) -join ' -> ').")
            return
        }
        if ($visited.Contains($Node)) {
            return
        }

        [void] $visiting.Add($Node)
        foreach ($next in @($Graph[$Node])) {
            if ($Graph.ContainsKey($next)) {
                Visit $next ($Stack + $Node)
            }
        }
        [void] $visiting.Remove($Node)
        [void] $visited.Add($Node)
    }

    foreach ($node in $Graph.Keys) {
        Visit $node @()
    }
}

function Test-BridgeProject([System.Collections.Generic.List[string]] $Errors, [string] $Module, [hashtable] $CsprojGraph) {
    $projectName = "$Module.csproj"
    $projectPath = Join-Path $RootPath "DotNet/Projects/$projectName"

    try {
        $xml = Read-XmlFile $projectPath
        $targetFramework = $xml.Project.PropertyGroup.TargetFramework
        if ($targetFramework -ne 'netstandard2.1') {
            $Errors.Add("$projectName target framework is $targetFramework, expected netstandard2.1.")
        }

        $packageRefs = @($xml.SelectNodes('//PackageReference'))
        if ($packageRefs.Count -ne 0) {
            $Errors.Add("$projectName must not contain PackageReference entries.")
        }

        $compileIncludes = @($xml.SelectNodes('//Compile') | ForEach-Object { $_.Include.Replace('\', '/') })
        $packageName = $modulePackages[$Module]
        $expectedInclude = "../../Packages/$packageName/Runtime/**/*.cs"
        if ($compileIncludes.Count -ne 1 -or $compileIncludes[0] -ne $expectedInclude) {
            $Errors.Add("$projectName Compile Include must be exactly $expectedInclude.")
        }

        foreach ($include in $compileIncludes) {
            foreach ($otherModule in $modulePackages.Keys) {
                if ($otherModule -ne $Module) {
                    $otherPackage = $modulePackages[$otherModule]
                    if ($include -like "*Packages/$otherPackage/*") {
                        $Errors.Add("$projectName includes source from $otherModule directly.")
                    }
                }
            }
        }

        $CsprojGraph[$Module] = Get-CsprojReferences $projectPath
        Assert-SetEquals $Errors "$Module csproj" $CsprojGraph[$Module] $allowed[$Module]
    }
    catch {
        $Errors.Add($_.Exception.Message)
    }
}

function Test-CentralPackageVersions([System.Collections.Generic.List[string]] $Errors) {
    $propsPath = Join-Path $RootPath 'Directory.Build.props'
    try {
        $xml = Read-XmlFile $propsPath
        foreach ($packageName in $testPackageVersions.Keys) {
            $propertyName = $testPackageVersions[$packageName].Property
            $expectedVersion = $testPackageVersions[$packageName].Version
            $propertyValue = [string] $xml.Project.PropertyGroup.$propertyName
            if ($propertyValue -ne $expectedVersion) {
                $Errors.Add("Directory.Build.props $propertyName must be $expectedVersion, got $propertyValue.")
            }
        }
    }
    catch {
        $Errors.Add($_.Exception.Message)
    }
}

function Test-TestCatalog([System.Collections.Generic.List[string]] $Errors) {
    $catalogPath = Join-Path $RootPath 'Tests/Metadata/test-catalog.json'
    try {
        $json = Read-JsonFile $catalogPath
        if (-not (Test-JsonProperty $json 'testCases')) {
            $Errors.Add('Test catalog must contain testCases.')
            return
        }

        $ids = New-Object System.Collections.Generic.HashSet[string]
        $ownershipKeys = New-Object System.Collections.Generic.HashSet[string]
        foreach ($case in @($json.testCases)) {
            foreach ($property in @('testCaseId', 'taskId', 'authority', 'runner', 'path', 'check')) {
                if (-not (Test-JsonProperty $case $property) -or [string]::IsNullOrWhiteSpace($case.$property)) {
                    $Errors.Add("Test catalog entry is missing '$property'.")
                }
            }

            if ($case.testCaseId -notmatch '^TC-[A-Z0-9]+(-[A-Z0-9]+)*-[0-9]{3}$') {
                $Errors.Add("Invalid test case ID: $($case.testCaseId).")
            }
            $taskId = [string] $case.taskId
            if ($taskId -notmatch '^ODY-S[0-9]{2}-[0-9]{3}$') {
                $Errors.Add("Invalid taskId for $($case.testCaseId): $taskId.")
            }
            else {
                $taskMatches = @(
                    Get-ChildItem -LiteralPath (Join-Path $RootPath 'docs/tasks/active') -File -Filter "$taskId`_*.md" -ErrorAction SilentlyContinue
                    Get-ChildItem -LiteralPath (Join-Path $RootPath 'docs/tasks/completed') -File -Filter "$taskId`_*.md" -ErrorAction SilentlyContinue
                )
                if ($taskMatches.Count -eq 0) {
                    $Errors.Add("Test catalog entry $($case.testCaseId) references missing task contract: $taskId.")
                }
                elseif ($taskMatches.Count -gt 1) {
                    $Errors.Add("Test catalog entry $($case.testCaseId) references ambiguous task contract: $taskId.")
                }
            }
            if (-not $ids.Add([string] $case.testCaseId)) {
                $Errors.Add("Duplicate test case ID: $($case.testCaseId).")
            }

            $casePath = Join-Path $RootPath ([string] $case.path)
            if (-not (Test-Path -LiteralPath $casePath)) {
                $Errors.Add("Test catalog path does not exist for $($case.testCaseId): $($case.path).")
            }
            $ownershipKey = "$($case.runner)|$($case.path)|$($case.check)"
            if (-not $ownershipKeys.Add($ownershipKey)) {
                $Errors.Add("Duplicate test catalog ownership entry for runner/path/check: $ownershipKey.")
            }
        }

        foreach ($requiredId in $requiredTestCaseIds) {
            if (-not $ids.Contains($requiredId)) {
                $Errors.Add("Required test case ID missing from catalog: $requiredId.")
            }
        }
    }
    catch {
        $Errors.Add($_.Exception.Message)
    }
}

function Test-ForbiddenGlobalApis([System.Collections.Generic.List[string]] $Errors) {
    $forbiddenApiPatterns = [ordered]@{
        'DateTime.Now' = '\bDateTime\.Now\b'
        'DateTime.UtcNow' = '\bDateTime\.UtcNow\b'
        'DateTimeOffset.Now' = '\bDateTimeOffset\.Now\b'
        'DateTimeOffset.UtcNow' = '\bDateTimeOffset\.UtcNow\b'
        'Stopwatch' = '\bStopwatch\b'
        'Environment.TickCount' = '\bEnvironment\.TickCount(?:64)?\b'
        'Task.Delay' = '\bTask\.Delay\b'
        'System.Random' = '\b(?:new\s+Random\s*\(|new\s+System\.Random\s*\(|System\.Random\s*\(|Random\.Shared\b)'
        'UnityEngine.Time' = '\bUnityEngine\.Time\b|\bTime\.deltaTime\b|\bTime\.time\b'
        'UnityEngine.Random' = '\bUnityEngine\.Random\b|\bRandom\.(?:Range|value|state)\b'
    }

    foreach ($module in $modulePackages.Keys) {
        $sourceRoot = Join-Path $RootPath "Packages/$($modulePackages[$module])/Runtime"
        if (-not (Test-Path -LiteralPath $sourceRoot)) {
            continue
        }

        foreach ($source in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs') {
            $relativePath = Get-RelativePath $source.FullName
            $text = Get-Content -LiteralPath $source.FullName -Raw
            foreach ($entry in $forbiddenApiPatterns.GetEnumerator()) {
                if ($text -match $entry.Value) {
                    $Errors.Add("Forbidden global time/random API '$($entry.Key)' in production source: $relativePath.")
                }
            }
        }
    }
}

function Test-RepositoryStructure {
    $errors = New-Object System.Collections.Generic.List[string]
    $asmdefGraph = @{}
    $packageGraph = @{}
    $csprojGraph = @{}

    Test-CentralPackageVersions $errors

    foreach ($requiredPath in @('DotNet/Odyssey.Core.sln', 'NuGet.Config', 'Tests/Metadata/test-catalog.json')) {
        $path = Join-Path $RootPath $requiredPath
        if (-not (Test-Path -LiteralPath $path)) {
            $errors.Add("Missing required path: $requiredPath.")
        }
    }

    foreach ($module in $modulePackages.Keys) {
        $packageName = $modulePackages[$module]
        $asmdefPath = Join-Path $RootPath "Packages/$packageName/Runtime/$module.asmdef"
        $sourcePath = Join-Path $RootPath "Packages/$packageName/Runtime/AssemblyMarker.cs"

        if (-not (Test-Path -LiteralPath $sourcePath)) {
            $errors.Add("Missing marker source: $(Get-RelativePath $sourcePath).")
        }

        try {
            $packageGraph[$module] = Get-PackageReferences $module
            $asmdefGraph[$module] = Get-AsmdefReferences $module $asmdefPath $true
        }
        catch {
            $errors.Add($_.Exception.Message)
        }

        Assert-SetEquals $errors "$module package.json" $packageGraph[$module] $allowed[$module]
        Assert-SetEquals $errors "$module asmdef" $asmdefGraph[$module] $allowed[$module]
    }

    $unityRuntimeAsmdef = Join-Path $RootPath 'Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef'
    $unityEditorAsmdef = Join-Path $RootPath 'Assets/Odyssey/Client/Editor/Odyssey.Unity.Client.Editor.asmdef'
    $editModeAsmdef = Join-Path $RootPath 'Assets/Odyssey/Client/Tests/EditMode/Odyssey.Tests.Unity.EditMode.asmdef'
    $playModeAsmdef = Join-Path $RootPath 'Assets/Odyssey/Client/Tests/PlayMode/Odyssey.Tests.Unity.PlayMode.asmdef'

    try {
        $asmdefGraph['Odyssey.Unity.Client'] = Get-AsmdefReferences 'Odyssey.Unity.Client' $unityRuntimeAsmdef $false
        $asmdefGraph['Odyssey.Unity.Client.Editor'] = Get-AsmdefReferences 'Odyssey.Unity.Client.Editor' $unityEditorAsmdef $false
        [void] (Get-AsmdefReferences 'Odyssey.Tests.Unity.EditMode' $editModeAsmdef $false)
        [void] (Get-AsmdefReferences 'Odyssey.Tests.Unity.PlayMode' $playModeAsmdef $false)
        Test-UnityTestAsmdef $errors 'Odyssey.Tests.Unity.EditMode' $editModeAsmdef $true
        Test-UnityTestAsmdef $errors 'Odyssey.Tests.Unity.PlayMode' $playModeAsmdef $false
    }
    catch {
        $errors.Add($_.Exception.Message)
    }

    Assert-SetEquals $errors 'Odyssey.Unity.Client asmdef' $asmdefGraph['Odyssey.Unity.Client'] $allowed['Odyssey.Unity.Client']
    Assert-SetEquals $errors 'Odyssey.Unity.Client.Editor asmdef' $asmdefGraph['Odyssey.Unity.Client.Editor'] $allowed['Odyssey.Unity.Client.Editor']

    foreach ($module in $coreBridgeModules) {
        Test-BridgeProject $errors $module $csprojGraph
    }

    foreach ($unexpected in @('Odyssey.Persistence.csproj', 'Odyssey.Networking.csproj')) {
        $path = Join-Path $RootPath "DotNet/Projects/$unexpected"
        if (Test-Path -LiteralPath $path) {
            $errors.Add("Unexpected bridge project exists: $(Get-RelativePath $path).")
        }
    }

    foreach ($unexpected in @('Odyssey.Tests.Persistence', 'Odyssey.Tests.Networking')) {
        $path = Join-Path $RootPath "DotNet/Tests/$unexpected"
        if (Test-Path -LiteralPath $path) {
            $errors.Add("Unexpected test project exists: $(Get-RelativePath $path).")
        }
    }

    foreach ($testProject in $testProjects) {
        $projectPath = Join-Path $RootPath "DotNet/Tests/$testProject/$testProject.csproj"
        try {
            $xml = Read-XmlFile $projectPath
            if ($xml.Project.PropertyGroup.TargetFramework -ne 'net10.0') {
                $errors.Add("$testProject target framework must be net10.0.")
            }

            $packageReferences = @($xml.SelectNodes('//PackageReference'))
            $actualPackages = @($packageReferences | ForEach-Object { $_.Include })
            Assert-SetEquals $errors "$testProject PackageReference" $actualPackages @($testPackageVersions.Keys)
            if ($packageReferences.Count -ne 3) {
                $errors.Add("$testProject must have exactly three PackageReference entries.")
            }

            foreach ($packageReference in $packageReferences) {
                $packageName = [string] $packageReference.Include
                if (-not $testPackageVersions.Contains($packageName)) {
                    $errors.Add("$testProject has unapproved PackageReference $packageName.")
                    continue
                }

                $propertyName = $testPackageVersions[$packageName].Property
                $expectedReference = '$(' + $propertyName + ')'
                $version = [string] $packageReference.Version
                if ($version -ne $expectedReference) {
                    $errors.Add("$testProject PackageReference $packageName must use Version=`"$expectedReference`", got `"$version`".")
                }
            }
        }
        catch {
            $errors.Add($_.Exception.Message)
        }
    }

    foreach ($module in $modulePackages.Keys) {
        $sourceRoot = Join-Path $RootPath "Packages/$($modulePackages[$module])/Runtime"
        if (-not (Test-Path -LiteralPath $sourceRoot)) {
            $errors.Add("Missing runtime source root for $module.")
            continue
        }

        $sources = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs')
        if ($sources.Count -eq 0) {
            $errors.Add("No production source exists for $module.")
        }

        foreach ($source in $sources) {
            $text = Get-Content -LiteralPath $source.FullName -Raw
            if ($module -in $coreBridgeModules -and $text -match '^\s*using\s+UnityEngine[.;]') {
                $errors.Add("Core source references UnityEngine: $(Get-RelativePath $source.FullName).")
            }
            if ($text -match 'Odyssey\.Tests|NUnit') {
                $errors.Add("Production source references test code: $(Get-RelativePath $source.FullName).")
            }
        }
    }

    $dotNetProductionSources = @(Get-ChildItem -Path (Join-Path $RootPath 'DotNet') -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -match '\\Projects\\' -and
            $_.FullName -notmatch '\\(bin|obj)\\'
        })
    if ($dotNetProductionSources.Count -gt 0) {
        $errors.Add('Production source must not be copied under DotNet/Projects.')
    }

    foreach ($module in $modulePackages.Keys) {
        if ($packageGraph.ContainsKey($module) -and $asmdefGraph.ContainsKey($module)) {
            Assert-SetEquals $errors "$module package/asmdef parity" $packageGraph[$module] $asmdefGraph[$module]
        }
        if ($module -in $coreBridgeModules -and $csprojGraph.ContainsKey($module)) {
            Assert-SetEquals $errors "$module asmdef/csproj parity" $asmdefGraph[$module] $csprojGraph[$module]
        }
    }

    if ('Odyssey.Networking' -in @($asmdefGraph['Odyssey.Persistence']) -or 'Odyssey.Persistence' -in @($asmdefGraph['Odyssey.Networking'])) {
        $errors.Add('Persistence and Networking must not reference each other.')
    }

    Test-Cycles $asmdefGraph $errors
    Test-TestCatalog $errors
    Test-ForbiddenGlobalApis $errors
    return ,$errors
}

function New-FixturePackage([string] $FixtureRoot, [string] $Module, [bool] $InvalidDomainDependency) {
    $packageName = $modulePackages[$Module]
    $dependencyModules = @($allowed[$Module])
    if ($InvalidDomainDependency -and $Module -eq 'Odyssey.Domain') {
        $dependencyModules = @('Odyssey.Rules')
    }

    $dependencyLines = @()
    foreach ($dependency in $dependencyModules) {
        $dependencyLines += "    `"$($modulePackages[$dependency])`": `"0.1.0`""
    }
    $dependencyBlock = '{}'
    if ($dependencyLines.Count -gt 0) {
        $dependencyBlock = "{`n$($dependencyLines -join ",`n")`n  }"
    }

    $referenceLines = @()
    foreach ($dependency in $dependencyModules) {
        $referenceLines += "    `"$dependency`""
    }
    $referenceBlock = '[]'
    if ($referenceLines.Count -gt 0) {
        $referenceBlock = "[`n$($referenceLines -join ",`n")`n  ]"
    }

    Write-Utf8NoBom (Join-Path $FixtureRoot "Packages/$packageName/package.json") @"
{
  "name": "$packageName",
  "version": "0.1.0",
  "unity": "6000.4",
  "license": "UNLICENSED",
  "dependencies": $dependencyBlock
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot "Packages/$packageName/Runtime/$Module.asmdef") @"
{
  "name": "$Module",
  "rootNamespace": "$Module",
  "references": $referenceBlock,
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot "Packages/$packageName/Runtime/AssemblyMarker.cs") @"
namespace $Module
{
    internal static class AssemblyMarker
    {
        internal const string ModuleName = "$Module";
    }
}
"@
}

function New-FixtureCsproj([string] $FixtureRoot, [string] $Module, [bool] $InvalidDomainDependency) {
    $projectReferences = @($allowed[$Module])
    if ($InvalidDomainDependency -and $Module -eq 'Odyssey.Domain') {
        $projectReferences = @('Odyssey.Rules')
    }

    $referenceBlock = ''
    if ($projectReferences.Count -gt 0) {
        $referenceLines = @()
        foreach ($reference in $projectReferences) {
            $referenceLines += "    <ProjectReference Include=`"$reference.csproj`" />"
        }
        $referenceBlock = @"

  <ItemGroup>
$($referenceLines -join "`n")
  </ItemGroup>
"@
    }

    $packageName = $modulePackages[$Module]
    Write-Utf8NoBom (Join-Path $FixtureRoot "DotNet/Projects/$Module.csproj") @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>$Module</AssemblyName>
    <RootNamespace>$Module</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>$referenceBlock

  <ItemGroup>
    <Compile Include="..\..\Packages\$packageName\Runtime\**\*.cs" />
  </ItemGroup>
</Project>
"@
}

function New-TestProjectFixture([string] $FixtureRoot, [string] $ProjectName) {
    Write-Utf8NoBom (Join-Path $FixtureRoot "DotNet/Tests/$ProjectName/$ProjectName.csproj") @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="`$(MicrosoftNETTestSdkVersion)" />
    <PackageReference Include="NUnit" Version="`$(NUnitVersion)" />
    <PackageReference Include="NUnit3TestAdapter" Version="`$(NUnit3TestAdapterVersion)" />
  </ItemGroup>
</Project>
"@
}

function New-SyntheticFixture([string] $FixtureRoot, [bool] $InvalidDomainDependency) {
    foreach ($module in $modulePackages.Keys) {
        New-FixturePackage $FixtureRoot $module $InvalidDomainDependency
    }

    foreach ($module in $coreBridgeModules) {
        New-FixtureCsproj $FixtureRoot $module $InvalidDomainDependency
    }

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Directory.Build.props') @"
<Project>
  <PropertyGroup>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <MicrosoftNETTestSdkVersion>18.8.1</MicrosoftNETTestSdkVersion>
    <NUnitVersion>4.6.1</NUnitVersion>
    <NUnit3TestAdapterVersion>6.2.0</NUnit3TestAdapterVersion>
  </PropertyGroup>
</Project>
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'DotNet/Odyssey.Core.sln') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'NuGet.Config') @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org"
         value="https://api.nuget.org/v3/index.json"
         protocolVersion="3" />
  </packageSources>
</configuration>
"@

    foreach ($testProject in $testProjects) {
        New-TestProjectFixture $FixtureRoot $testProject
    }

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef') @"
{
  "name": "Odyssey.Unity.Client",
  "references": [
    "Odyssey.Domain",
    "Odyssey.Rules",
    "Odyssey.Content",
    "Odyssey.Application",
    "Odyssey.Persistence",
    "Odyssey.Networking"
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Editor/Odyssey.Unity.Client.Editor.asmdef') @"
{
  "name": "Odyssey.Unity.Client.Editor",
  "references": [
    "Odyssey.Unity.Client",
    "Odyssey.Domain",
    "Odyssey.Rules",
    "Odyssey.Content",
    "Odyssey.Application",
    "Odyssey.Persistence",
    "Odyssey.Networking"
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Tests/EditMode/Odyssey.Tests.Unity.EditMode.asmdef') @"
{
  "name": "Odyssey.Tests.Unity.EditMode",
  "references": [
    "Odyssey.Unity.Client"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Tests/PlayMode/Odyssey.Tests.Unity.PlayMode.asmdef') @"
{
  "name": "Odyssey.Tests.Unity.PlayMode",
  "references": [
    "Odyssey.Unity.Client"
  ],
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'Tests/Metadata/test-catalog.json') @"
{
  "testCases": [
    { "testCaseId": "TC-ARCH-001", "taskId": "ODY-S00-003", "authority": "ADR-001", "runner": "PowerShell", "path": "scripts/verify-test-structure.ps1", "check": "valid graph" },
    { "testCaseId": "TC-ARCH-002", "taskId": "ODY-S00-003", "authority": "ADR-001", "runner": "PowerShell", "path": "scripts/verify-test-structure.ps1", "check": "invalid graph" },
    { "testCaseId": "TC-DOTNET-001", "taskId": "ODY-S00-003", "authority": "ADR-006", "runner": "dotnet test", "path": "DotNet/Odyssey.Core.sln", "check": "dotnet bridge tests" },
    { "testCaseId": "TC-UNITY-ASM-001", "taskId": "ODY-S00-003", "authority": "ADR-001", "runner": "Unity batchmode", "path": "Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef", "check": "Unity asmdef graph" },
    { "testCaseId": "TC-UNITY-TEST-001", "taskId": "ODY-S00-003", "authority": "ADR-006", "runner": "Unity Test Framework", "path": "Assets/Odyssey/Client/Tests", "check": "Unity tests" },
    { "testCaseId": "TC-REPO-001", "taskId": "ODY-S00-003", "authority": "AGENTS.md", "runner": "PowerShell", "path": "scripts/verify-repository.ps1", "check": "repository verification" },
    { "testCaseId": "TC-ID-001", "taskId": "ODY-S00-004", "authority": "ODY-S00-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "identity valid values" },
    { "testCaseId": "TC-ID-002", "taskId": "ODY-S00-004", "authority": "ODY-S00-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "identity invalid values" },
    { "testCaseId": "TC-VERSION-001", "taskId": "ODY-S00-004", "authority": "ADR-007", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "version valid values" },
    { "testCaseId": "TC-VERSION-002", "taskId": "ODY-S00-004", "authority": "ADR-007", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "version dimensions independent" },
    { "testCaseId": "TC-RESULT-001", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "result invariants" },
    { "testCaseId": "TC-RESULT-002", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "error safe fields" },
    { "testCaseId": "TC-RESULT-003", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "retry vocabulary" },
    { "testCaseId": "TC-RESULT-004", "taskId": "ODY-S00-004", "authority": "ADR-004", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "bounded safe details" },
    { "testCaseId": "TC-CMD-001", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "command envelope" },
    { "testCaseId": "TC-CMD-002", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "command result" },
    { "testCaseId": "TC-CMD-003", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "duplicate replay" },
    { "testCaseId": "TC-CMD-004", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "mismatch rejection" },
    { "testCaseId": "TC-CMD-005", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "single-flight duplicate" },
    { "testCaseId": "TC-CMD-006", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "atomic commit failure" },
    { "testCaseId": "TC-EVENT-001", "taskId": "ODY-S00-005", "authority": "ADR-002", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "event batch" },
    { "testCaseId": "TC-CLOCK-001", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "clock injection" },
    { "testCaseId": "TC-CLOCK-002", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "virtual scheduler" },
    { "testCaseId": "TC-CLOCK-003", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "ADR-008 clock shape" },
    { "testCaseId": "TC-RNG-001", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "HMAC vector" },
    { "testCaseId": "TC-RNG-002", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "xoshiro vector" },
    { "testCaseId": "TC-RNG-003", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "rejection mapping" },
    { "testCaseId": "TC-RNG-004", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "proof data secrets" },
    { "testCaseId": "TC-RNG-005", "taskId": "ODY-S00-005", "authority": "ADR-008", "runner": "dotnet test", "path": "DotNet/Tests/Odyssey.Tests.Unit", "check": "canonical RNG evidence" }
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'scripts/verify-test-structure.ps1') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'scripts/verify-repository.ps1') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'docs/tasks/completed/ODY-S00-003_Module_and_Test_Skeleton.md') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'docs/tasks/completed/ODY-S00-004_Identity_Version_and_Result_Primitives.md') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'docs/tasks/active/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md') "`n"
}

function Set-FixturePackageVersionMismatch([string] $FixtureRoot) {
    $packagePath = Join-Path $FixtureRoot 'Packages/com.odyssey.rules/package.json'
    $json = Read-JsonFile $packagePath
    $json.dependencies.'com.odyssey.domain' = '0.2.0'
    Write-Utf8NoBom $packagePath ($json | ConvertTo-Json -Depth 10)
}

function Set-FixtureDuplicateCatalogOwnership([string] $FixtureRoot) {
    $catalogPath = Join-Path $FixtureRoot 'Tests/Metadata/test-catalog.json'
    $json = Read-JsonFile $catalogPath
    $json.testCases[1].check = $json.testCases[0].check
    Write-Utf8NoBom $catalogPath ($json | ConvertTo-Json -Depth 10)
}

function Invoke-GuardFixture([string] $FixtureRoot) {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RootPath $FixtureRoot -SkipNegativeFixture 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    return @{
        ExitCode = $exitCode
        Output = @($output)
        Text = ($output -join "`n")
    }
}

$errors = Test-RepositoryStructure
if ($errors.Count -gt 0) {
    Write-Host 'TC-ARCH-001 FAIL valid ADR-001 graph check failed'
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'TC-ARCH-001 PASS valid ADR-001 graph passes'

if (-not $SkipNegativeFixture) {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odyssey-graph-fixture-" + [guid]::NewGuid().ToString('N'))
    try {
        New-SyntheticFixture $fixtureRoot $false
        $validResult = Invoke-GuardFixture $fixtureRoot
        if ($validResult.ExitCode -ne 0) {
            Write-Host "TC-ARCH-002 FAIL valid synthetic fixture failed with exit code $($validResult.ExitCode)"
            $validResult.Output | ForEach-Object { Write-Host $_ }
            exit 1
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-SyntheticFixture $fixtureRoot $true
        $invalidResult = Invoke-GuardFixture $fixtureRoot
        $expectedDiagnostic = $invalidResult.Text -match 'Odyssey\.Domain.*(dependencies mismatch|Cycle detected)|Cycle detected.*Odyssey\.Domain'
        if ($invalidResult.ExitCode -eq 0 -or -not $expectedDiagnostic) {
            Write-Host "TC-ARCH-002 FAIL controlled invalid fixture did not fail for expected Domain dependency reason"
            $invalidResult.Output | ForEach-Object { Write-Host $_ }
            exit 1
        }
        Write-Host "TC-ARCH-002 PASS controlled invalid Domain->Rules dependency rejected with exit code $($invalidResult.ExitCode)"

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-SyntheticFixture $fixtureRoot $false
        Set-FixturePackageVersionMismatch $fixtureRoot
        $versionResult = Invoke-GuardFixture $fixtureRoot
        if ($versionResult.ExitCode -eq 0 -or $versionResult.Text -notmatch 'Package dependency version mismatch') {
            Write-Host 'TC-ARCH-002 FAIL controlled package version mismatch was not rejected for expected reason'
            $versionResult.Output | ForEach-Object { Write-Host $_ }
            exit 1
        }
        Write-Host "TC-ARCH-002 PASS controlled package version mismatch rejected with exit code $($versionResult.ExitCode)"

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-SyntheticFixture $fixtureRoot $false
        Set-FixtureDuplicateCatalogOwnership $fixtureRoot
        $catalogResult = Invoke-GuardFixture $fixtureRoot
        if ($catalogResult.ExitCode -eq 0 -or $catalogResult.Text -notmatch 'Duplicate test catalog ownership entry') {
            Write-Host 'TC-ARCH-002 FAIL controlled duplicate catalog ownership was not rejected for expected reason'
            $catalogResult.Output | ForEach-Object { Write-Host $_ }
            exit 1
        }
        Write-Host "TC-ARCH-002 PASS controlled duplicate catalog ownership rejected with exit code $($catalogResult.ExitCode)"
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

exit 0
