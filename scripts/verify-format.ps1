Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $textFiles = @(
        Get-ChildItem -Path . -Recurse -File -Include *.cs,*.csproj,*.props,*.json,*.asmdef,*.ps1 |
            Where-Object {
                $_.FullName -notmatch '\\(Library|Temp|Obj|Build|Builds|Logs|UserSettings|\.git)\\'
            }
    )

    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($file in $textFiles) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -match '\s+$') {
                $relative = Resolve-Path -Relative $file.FullName
                $failures.Add("$relative line $($i + 1) has trailing whitespace.")
            }
        }
    }

    dotnet format .\DotNet\Odyssey.Core.sln --verify-no-changes --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format failed with exit code $LASTEXITCODE."
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ }
        exit 1
    }

    Write-Host 'FORMAT-001 PASS repository text formatting checks passed'
}
finally {
    Pop-Location
}
