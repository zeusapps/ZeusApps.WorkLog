param(
    [string]$Version,
    [string]$OutputDirectory,
    # Development build: next unused PATCH >= 9000 of the base version in Directory.Build.props, written to artifacts/dev-builds.
    [switch]$Dev,
    [string]$DevVersion,
    # Prints the development build plan as JSON and writes nothing.
    [switch]$PlanOnly,
    # Installation whose versions/ folder also counts when choosing the next development number.
    [string]$TargetInstallation = (Join-Path $env:LOCALAPPDATA 'Programs\WorkLog')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    . (Join-Path $PSScriptRoot 'package-core.ps1')
    $projectVersion = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if ($Dev -and $Version) { throw '-Dev cannot be combined with -Version.' }
    if ($DevVersion -and -not $Dev) { throw '-DevVersion needs -Dev.' }
    if ($PlanOnly -and -not $Dev) { throw '-PlanOnly needs -Dev.' }
    if (-not $OutputDirectory) { $OutputDirectory = if ($Dev) { 'artifacts/dev-builds' } else { 'artifacts/releases' } }
    $releaseRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory)) }
    if (-not $Dev) {
        if (-not $Version) { $Version = $projectVersion }
        if (Test-DevVersion $Version) { throw "Version $Version is in the reserved development range (PATCH >= $script:DevPatchStart); stable packages cannot use it. Use -Dev for development builds." }
        if ($Version -cne $projectVersion) { throw "Release version $Version differs from application version $projectVersion" }
        if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected MAJOR.MINOR.PATCH version' }
        New-WorkLogPackage -PackageVersion $Version -OutputDirectory $releaseRoot
        return
    }

    if ($projectVersion -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -or (Test-DevVersion $projectVersion)) { throw 'Directory.Build.props must hold a stable MAJOR.MINOR.PATCH base version.' }
    $major = $projectVersion.Split('.')[0]; $minor = $projectVersion.Split('.')[1]
    $taken = @()
    $pattern = "^$major\.$minor\.(\d+)$"
    if (Test-Path -LiteralPath $releaseRoot) {
        foreach ($item in Get-ChildItem -LiteralPath $releaseRoot -Force) {
            if ($item.Name -match "^worklog-($major\.$minor\.\d+)-win-x64(\.zip(\.sha256)?)?$") { $taken += $Matches[1] }
        }
    }
    $installedVersions = Join-Path $TargetInstallation 'versions'
    if (Test-Path -LiteralPath $installedVersions) {
        foreach ($item in Get-ChildItem -LiteralPath $installedVersions -Directory) { if ($item.Name -match $pattern) { $taken += $item.Name } }
    }
    $highest = ($taken | Where-Object { Test-DevVersion $_ } | ForEach-Object { [int64]$_.Split('.')[2] } | Measure-Object -Maximum).Maximum
    $nextPatch = if ($null -eq $highest) { $script:DevPatchStart + 1 } else { [int64]$highest + 1 }
    $packageVersion = if ($DevVersion) { $DevVersion } else { "$major.$minor.$nextPatch" }
    if (-not (Test-DevVersion $packageVersion) -or $packageVersion -notmatch "^$major\.$minor\.\d+$") { throw "-DevVersion must be $major.$minor.PATCH with PATCH >= $script:DevPatchStart." }
    if ($packageVersion -in $taken) { throw "Development version $packageVersion already exists in $releaseRoot or in the target installation." }

    $commit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot read the Git commit.' }
    # Only files that feed the build make a tree dirty; scratch files elsewhere (for example OpenSpec notes) do not.
    # This set only labels the build dirty. It is wider than the stale-input list that Preflight uses (tools/WorkLog.DevInstall/Staleness.cs), which is the single documented list of inputs that change what is packaged.
    $inputs = @('src', 'tests', 'tools', 'scripts', 'dashboard', 'docs') + @(Get-ChildItem -LiteralPath $projectRoot -File -Force | Where-Object { $_.Name -notlike '.*' -or $_.Name -eq '.editorconfig' } | ForEach-Object Name)
    $dirty = [bool]((git status --porcelain --untracked-files=normal -- @inputs) -join '')
    $marker = [ordered]@{ channel = 'dev'; commit = $commit; dirty = $dirty; builtAt = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'); baseVersion = $projectVersion }
    $informational = "$packageVersion-dev+$($commit.Substring(0, 7))" + $(if ($dirty) { '.dirty' } else { '' })
    $plan = [ordered]@{ version = $packageVersion; baseVersion = $projectVersion; outputDirectory = $releaseRoot; informationalVersion = $informational; build = $marker }
    if ($PlanOnly) {
        $plan | ConvertTo-Json -Depth 5
        return
    }
    $result = New-WorkLogPackage -PackageVersion $packageVersion -OutputDirectory $releaseRoot -DevMarker $marker -InformationalVersion $informational
    Write-Host "Packaged $($result.Archive)"
    $result
}
finally { Pop-Location }
