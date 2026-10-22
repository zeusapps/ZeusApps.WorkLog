param(
    [string]$Version,
    [string]$OutputDirectory = 'artifacts/releases'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $projectVersion = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if (-not $Version) { $Version = $projectVersion }
    if ($Version -cne $projectVersion) { throw "Release version $Version differs from application version $projectVersion" }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected MAJOR.MINOR.PATCH version' }
    $releaseRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory)) }
    . (Join-Path $PSScriptRoot 'package-core.ps1')
    New-WorkLogPackage -PackageVersion $Version -OutputDirectory $releaseRoot
}
finally { Pop-Location }
