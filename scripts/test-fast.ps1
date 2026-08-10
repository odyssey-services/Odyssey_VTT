Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    & .\scripts\verify-test-structure.ps1
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet restore .\DotNet\Odyssey.Core.sln
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet build .\DotNet\Odyssey.Core.sln --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet test .\DotNet\Odyssey.Core.sln --no-build
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
