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
    $publishPath = Join-Path $releaseRoot "worklog-$Version-win-x64"
    dotnet publish src/WorkLog/WorkLog.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath README.md -Destination $publishPath
    Copy-Item -LiteralPath docs/installation.md -Destination (Join-Path $publishPath 'INSTALL.md')
    $manifest = @{ version = $Version; runtime = 'win-x64'; selfContained = $true }
    $manifest | ConvertTo-Json | Set-Content (Join-Path $publishPath 'release.json') -Encoding utf8
    $executable = Join-Path $publishPath 'worklog.exe'
    $actualVersion = & $executable --version
    if ($LASTEXITCODE -ne 0 -or $actualVersion -cne $Version) { throw 'Published version smoke check failed' }
    $archive = Join-Path $releaseRoot "worklog-$Version-win-x64.zip"
    # Package only owned release files; reused publish directories can contain local smoke logs.
    $releaseFiles = @('worklog.exe', 'release.json', 'README.md', 'INSTALL.md') | ForEach-Object { Join-Path $publishPath $_ }
    $symbols = Join-Path $publishPath 'worklog.pdb'
    if (Test-Path -LiteralPath $symbols) { $releaseFiles += $symbols }
    Compress-Archive -LiteralPath $releaseFiles -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($archive))" | Set-Content ($archive + '.sha256') -Encoding ascii
    Write-Output "Packaged $archive"
}
finally { Pop-Location }
