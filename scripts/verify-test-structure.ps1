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
$requiredTestCaseIds = @('TC-ARCH-001', 'TC-ARCH-002', 'TC-DOTNET-001', 'TC-UNITY-ASM-001', 'TC-UNITY-TEST-001', 'TC-REPO-001')
$testProjects = @('Odyssey.Tests.Unit', 'Odyssey.Tests.Domain', 'Odyssey.Tests.Contracts', 'Odyssey.Tests.Architecture')

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

function Get-PackageReferences([string] $AssemblyName) {
    $packageName = $modulePackages[$AssemblyName]
    $json = Read-JsonFile (Join-Path $RootPath "Packages/$packageName/package.json")

    if ($json.name -ne $packageName) {
        throw "Package name mismatch in Packages/$packageName/package.json: expected $packageName, got $($json.name)."
    }

    $references = @()
    if (Test-JsonProperty $json 'dependencies') {
        $dependencyProperties = @($json.dependencies.PSObject.Properties)
        foreach ($dependency in @($dependencyProperties | ForEach-Object { $_.Name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            $moduleName = ($modulePackages.GetEnumerator() | Where-Object { $_.Value -eq $dependency }).Key
            if (-not $moduleName) {
                throw "Unexpected package dependency '$dependency' in Packages/$packageName/package.json."
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

function Test-TestCatalog([System.Collections.Generic.List[string]] $Errors) {
    $catalogPath = Join-Path $RootPath 'Tests/Metadata/test-catalog.json'
    try {
        $json = Read-JsonFile $catalogPath
        if (-not (Test-JsonProperty $json 'testCases')) {
            $Errors.Add('Test catalog must contain testCases.')
            return
        }

        $ids = New-Object System.Collections.Generic.HashSet[string]
        $paths = New-Object System.Collections.Generic.HashSet[string]
        foreach ($case in @($json.testCases)) {
            foreach ($property in @('testCaseId', 'taskId', 'authority', 'runner', 'path', 'check')) {
                if (-not (Test-JsonProperty $case $property) -or [string]::IsNullOrWhiteSpace($case.$property)) {
                    $Errors.Add("Test catalog entry is missing '$property'.")
                }
            }

            if ($case.testCaseId -notmatch '^TC-[A-Z0-9]+(-[A-Z0-9]+)*-[0-9]{3}$') {
                $Errors.Add("Invalid test case ID: $($case.testCaseId).")
            }
            if ($case.taskId -ne 'ODY-S00-003') {
                $Errors.Add("Test catalog entry $($case.testCaseId) has taskId $($case.taskId), expected ODY-S00-003.")
            }
            if (-not $ids.Add([string] $case.testCaseId)) {
                $Errors.Add("Duplicate test case ID: $($case.testCaseId).")
            }

            $casePath = Join-Path $RootPath ([string] $case.path)
            if (-not (Test-Path -LiteralPath $casePath)) {
                $Errors.Add("Test catalog path does not exist for $($case.testCaseId): $($case.path).")
            }
            if (-not $paths.Add("$($case.testCaseId)|$($case.path)|$($case.check)")) {
                $Errors.Add("Duplicate test catalog ownership entry for $($case.testCaseId).")
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

function Test-RepositoryStructure {
    $errors = New-Object System.Collections.Generic.List[string]
    $asmdefGraph = @{}
    $packageGraph = @{}
    $csprojGraph = @{}

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
            foreach ($packageReference in @($xml.SelectNodes('//PackageReference'))) {
                $version = $packageReference.Version
                if ([string]::IsNullOrWhiteSpace($version) -or $version -match '[\*\[\]\(\),]' -or $version -match '-') {
                    $errors.Add("$testProject has non-exact or prerelease package version for $($packageReference.Include).")
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
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="NUnit" Version="4.6.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="6.2.0" />
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
    { "testCaseId": "TC-REPO-001", "taskId": "ODY-S00-003", "authority": "AGENTS.md", "runner": "PowerShell", "path": "scripts/verify-repository.ps1", "check": "repository verification" }
  ]
}
"@

    Write-Utf8NoBom (Join-Path $FixtureRoot 'scripts/verify-test-structure.ps1') "`n"
    Write-Utf8NoBom (Join-Path $FixtureRoot 'scripts/verify-repository.ps1') "`n"
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
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $validOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RootPath $fixtureRoot -SkipNegativeFixture 2>&1
        $validExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
        if ($validExitCode -ne 0) {
            Write-Host "TC-ARCH-002 FAIL valid synthetic fixture failed with exit code $validExitCode"
            $validOutput | ForEach-Object { Write-Host $_ }
            exit 1
        }

        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        New-SyntheticFixture $fixtureRoot $true
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $invalidOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RootPath $fixtureRoot -SkipNegativeFixture 2>&1
        $invalidExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
        $expectedDiagnostic = ($invalidOutput -join "`n") -match 'Odyssey\.Domain.*(dependencies mismatch|Cycle detected)|Cycle detected.*Odyssey\.Domain'
        if ($invalidExitCode -eq 0 -or -not $expectedDiagnostic) {
            Write-Host "TC-ARCH-002 FAIL controlled invalid fixture did not fail for expected Domain dependency reason"
            $invalidOutput | ForEach-Object { Write-Host $_ }
            exit 1
        }

        Write-Host "TC-ARCH-002 PASS controlled invalid Domain->Rules dependency rejected with exit code $invalidExitCode"
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

exit 0
