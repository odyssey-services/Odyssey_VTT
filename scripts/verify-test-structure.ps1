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
$testAssemblyNames = @('Odyssey.Tests.Unit', 'Odyssey.Tests.Domain', 'Odyssey.Tests.Contracts', 'Odyssey.Tests.Architecture', 'Odyssey.Tests.Unity.EditMode', 'Odyssey.Tests.Unity.PlayMode')

function Get-RelativePath([string] $Path) {
    $rootFull = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + '\'
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length).Replace('\', '/')
    }
    return $pathFull.Replace('\', '/')
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

function Get-PackageReferences([string] $AssemblyName) {
    $packageName = $modulePackages[$AssemblyName]
    $json = Read-JsonFile (Join-Path $RootPath "Packages/$packageName/package.json")
    $references = @()
    if ($json.PSObject.Properties.Name -contains 'dependencies') {
        foreach ($dependency in $json.dependencies.PSObject.Properties.Name) {
            $moduleName = ($modulePackages.GetEnumerator() | Where-Object { $_.Value -eq $dependency }).Key
            if ($moduleName) {
                $references += $moduleName
            }
        }
    }
    return @($references | Sort-Object)
}

function Get-AsmdefReferences([string] $AssemblyName, [string] $AsmdefPath) {
    $json = Read-JsonFile $AsmdefPath
    if ($json.name -ne $AssemblyName) {
        throw "Assembly name mismatch in $(Get-RelativePath $AsmdefPath): expected $AssemblyName, got $($json.name)."
    }

    $references = @()
    foreach ($reference in @($json.references)) {
        if ($reference -like 'GUID:*') {
            throw "GUID asmdef reference is not allowed in $(Get-RelativePath $AsmdefPath)."
        }
        if ($reference -in $allowed.Keys) {
            $references += $reference
        }
        elseif ($reference -like 'Odyssey.*') {
            throw "Unknown Odyssey asmdef reference '$reference' in $(Get-RelativePath $AsmdefPath)."
        }
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
        if ($name -like 'Odyssey.*') {
            $references += $name
        }
    }
    return @($references | Sort-Object)
}

function Assert-SetEquals([System.Collections.Generic.List[string]] $Errors, [string] $Subject, [string[]] $Actual, [string[]] $Expected) {
    $actualJoined = (@($Actual) | Sort-Object) -join ','
    $expectedJoined = (@($Expected) | Sort-Object) -join ','
    if ($actualJoined -ne $expectedJoined) {
        $Errors.Add("$Subject dependencies mismatch. Expected [$expectedJoined], actual [$actualJoined].")
    }
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

function Test-RepositoryStructure {
    $errors = New-Object System.Collections.Generic.List[string]
    $asmdefGraph = @{}
    $packageGraph = @{}
    $csprojGraph = @{}

    foreach ($module in $modulePackages.Keys) {
        $packageName = $modulePackages[$module]
        $packagePath = Join-Path $RootPath "Packages/$packageName/package.json"
        $asmdefPath = Join-Path $RootPath "Packages/$packageName/Runtime/$module.asmdef"
        $sourcePath = Join-Path $RootPath "Packages/$packageName/Runtime/AssemblyMarker.cs"

        if (-not (Test-Path -LiteralPath $sourcePath)) {
            $errors.Add("Missing marker source: $(Get-RelativePath $sourcePath).")
        }

        try {
            $packageGraph[$module] = Get-PackageReferences $module
            $asmdefGraph[$module] = Get-AsmdefReferences $module $asmdefPath
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
        $asmdefGraph['Odyssey.Unity.Client'] = Get-AsmdefReferences 'Odyssey.Unity.Client' $unityRuntimeAsmdef
        $asmdefGraph['Odyssey.Unity.Client.Editor'] = Get-AsmdefReferences 'Odyssey.Unity.Client.Editor' $unityEditorAsmdef
        [void] (Get-AsmdefReferences 'Odyssey.Tests.Unity.EditMode' $editModeAsmdef)
        [void] (Get-AsmdefReferences 'Odyssey.Tests.Unity.PlayMode' $playModeAsmdef)
        Test-UnityTestAsmdef $errors 'Odyssey.Tests.Unity.EditMode' $editModeAsmdef $true
        Test-UnityTestAsmdef $errors 'Odyssey.Tests.Unity.PlayMode' $playModeAsmdef $false
    }
    catch {
        $errors.Add($_.Exception.Message)
    }

    Assert-SetEquals $errors 'Odyssey.Unity.Client asmdef' $asmdefGraph['Odyssey.Unity.Client'] $allowed['Odyssey.Unity.Client']
    Assert-SetEquals $errors 'Odyssey.Unity.Client.Editor asmdef' $asmdefGraph['Odyssey.Unity.Client.Editor'] $allowed['Odyssey.Unity.Client.Editor']

    foreach ($module in $coreBridgeModules) {
        $projectName = "$module.csproj"
        $projectPath = Join-Path $RootPath "DotNet/Projects/$projectName"
        try {
            $xml = Read-XmlFile $projectPath
            $targetFramework = $xml.Project.PropertyGroup.TargetFramework
            if ($targetFramework -ne 'netstandard2.1') {
                $errors.Add("$projectName target framework is $targetFramework, expected netstandard2.1.")
            }

            $compileIncludes = @($xml.SelectNodes('//Compile') | ForEach-Object { $_.Include.Replace('\', '/') })
            $packageName = $modulePackages[$module]
            $expectedInclude = "../../Packages/$packageName/Runtime/**/*.cs"
            if ($expectedInclude -notin $compileIncludes) {
                $errors.Add("$projectName does not include $expectedInclude.")
            }

            foreach ($include in $compileIncludes) {
                foreach ($otherModule in $modulePackages.Keys) {
                    if ($otherModule -ne $module) {
                        $otherPackage = $modulePackages[$otherModule]
                        if ($include -like "*Packages/$otherPackage/*") {
                            $errors.Add("$projectName includes source from $otherModule directly.")
                        }
                    }
                }
            }

            $csprojGraph[$module] = Get-CsprojReferences $projectPath
            Assert-SetEquals $errors "$module csproj" $csprojGraph[$module] $allowed[$module]
        }
        catch {
            $errors.Add($_.Exception.Message)
        }
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

    foreach ($testProject in @('Odyssey.Tests.Unit', 'Odyssey.Tests.Domain', 'Odyssey.Tests.Contracts', 'Odyssey.Tests.Architecture')) {
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
            if ($module -in $coreBridgeModules -and $text -match '^\s*using\s+UnityEngine[.;]' ) {
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
    return ,$errors
}

function New-InvalidFixture([string] $FixtureRoot) {
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.domain/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.rules/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.content/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.application/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.persistence/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Packages/com.odyssey.networking/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Runtime') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Editor') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Tests/EditMode') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'Assets/Odyssey/Client/Tests/PlayMode') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'DotNet/Projects') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'DotNet/Tests/Odyssey.Tests.Unit') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'DotNet/Tests/Odyssey.Tests.Domain') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'DotNet/Tests/Odyssey.Tests.Contracts') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $FixtureRoot 'DotNet/Tests/Odyssey.Tests.Architecture') | Out-Null

    foreach ($module in $modulePackages.Keys) {
        $packageName = $modulePackages[$module]
        $deps = @{}
        foreach ($dependency in $allowed[$module]) {
            if ($modulePackages.Contains($dependency)) {
                $deps[$modulePackages[$dependency]] = '0.1.0'
            }
        }
        if ($module -eq 'Odyssey.Domain') {
            $deps['com.odyssey.rules'] = '0.1.0'
        }
        $package = [ordered]@{
            name = $packageName
            version = '0.1.0'
            unity = '6000.4'
            license = 'UNLICENSED'
            dependencies = $deps
        }
        $package | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $FixtureRoot "Packages/$packageName/package.json") -Encoding UTF8

        $refs = @($allowed[$module])
        if ($module -eq 'Odyssey.Domain') {
            $refs = @('Odyssey.Rules')
        }
        $asmdef = [ordered]@{
            name = $module
            references = $refs
            includePlatforms = @()
            excludePlatforms = @()
            noEngineReferences = $true
        }
        $asmdef | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $FixtureRoot "Packages/$packageName/Runtime/$module.asmdef") -Encoding UTF8
        "namespace $module { internal static class AssemblyMarker { } }" | Set-Content -LiteralPath (Join-Path $FixtureRoot "Packages/$packageName/Runtime/AssemblyMarker.cs") -Encoding UTF8
    }
}

$errors = Test-RepositoryStructure
if ($errors.Count -gt 0) {
    Write-Host 'TST-ARCH-001 FAIL valid ADR-001 graph check failed'
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'TST-ARCH-001 PASS valid ADR-001 graph passes'

if (-not $SkipNegativeFixture) {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odyssey-invalid-graph-" + [guid]::NewGuid().ToString('N'))
    try {
        New-InvalidFixture $fixtureRoot
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $fixtureOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RootPath $fixtureRoot -SkipNegativeFixture 2>&1
        $fixtureExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
        if ($fixtureExitCode -eq 0) {
            Write-Host 'TST-ARCH-002 FAIL invalid dependency fixture unexpectedly passed'
            $fixtureOutput | ForEach-Object { Write-Host $_ }
            exit 1
        }
        Write-Host "TST-ARCH-002 PASS invalid dependency fixture failed with exit code $fixtureExitCode"
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

exit 0
