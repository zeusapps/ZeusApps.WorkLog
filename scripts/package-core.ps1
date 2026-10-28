# Versions whose PATCH is at least this value are development builds (InstallationLayout.DevPatchStart).
$script:DevPatchStart = 9000
function Test-DevVersion([string]$Text) { return $Text -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -and [int64]($Text.Split('.')[2]) -ge $script:DevPatchStart }

function New-WorkLogPackage {
    # $DevMarker (channel, commit, dirty, builtAt, baseVersion) marks a development build and is required for, and only valid with, a version in the development range.
    param([string]$PackageVersion, [string]$OutputDirectory, [System.Collections.IDictionary]$DevMarker, [string]$InformationalVersion)
    $ErrorActionPreference = 'Stop'
    if ($PackageVersion -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected stable MAJOR.MINOR.PATCH version' }
    $isDevVersion = Test-DevVersion $PackageVersion
    if ($isDevVersion -and -not $DevMarker) { throw "Version $PackageVersion is in the reserved development range (PATCH >= $script:DevPatchStart); stable packages cannot use it. Use package.ps1 -Dev for development builds." }
    if ($DevMarker -and -not $isDevVersion) { throw "A development marker needs a development-range version (PATCH >= $script:DevPatchStart)." }
    $releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
    $packageRoot = Join-Path $releaseRoot "worklog-$PackageVersion-win-x64"
    if (Test-Path -LiteralPath $packageRoot) { throw "Package output already exists: $packageRoot. Select a fresh OutputDirectory to preserve immutable payloads." }
    $buildRoot = Join-Path $releaseRoot ('.build/' + [guid]::NewGuid().ToString('N'))
    $payload = Join-Path $packageRoot "versions/$PackageVersion"
    $launcher = Join-Path $buildRoot 'launcher'
    $publishArguments = @("-p:Version=$PackageVersion")
    if ($InformationalVersion) { $publishArguments += "-p:InformationalVersion=$InformationalVersion" }
    dotnet publish src/WorkLog/WorkLog.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true @publishArguments -o $payload | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Payload publish failed' }
    dotnet publish src/WorkLog.Launcher/WorkLog.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=1.0.0 -o $launcher | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed' }
    Copy-Item -LiteralPath (Join-Path $launcher 'worklog-launcher.exe') -Destination (Join-Path $packageRoot 'worklog.exe')
    foreach ($directory in @($packageRoot, $payload)) {
        Copy-Item -LiteralPath README.md -Destination $directory
        Copy-Item -LiteralPath docs/installation.md -Destination (Join-Path $directory 'INSTALL.md')
    }
    $hashes = @{}
    foreach ($name in @('worklog.exe', 'README.md', 'INSTALL.md', 'worklog.pdb')) {
        $file = Join-Path $payload $name
        if (Test-Path -LiteralPath $file) { $hashes[$name] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $manifest = @{ version = $PackageVersion; runtime = 'win-x64'; selfContained = $true; packageFormat = 2; launcherContract = 1; sharedDataContract = 1; journalSchema = 2; files = $hashes }
    if ($DevMarker) { $manifest['build'] = $DevMarker }
    [System.IO.File]::WriteAllText((Join-Path $payload 'release.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
    $manifestHash = (Get-FileHash -LiteralPath (Join-Path $payload 'release.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $selection = @{ current = @{ version = $PackageVersion; manifestHash = $manifestHash }; previous = $null; generation = 1; operationId = [guid]::NewGuid().ToString('N') }
    [System.IO.File]::WriteAllText((Join-Path $packageRoot 'current.json'), ($selection | ConvertTo-Json -Depth 8), $utf8)
    [System.IO.File]::WriteAllText((Join-Path $packageRoot 'launcher.json'), (@{ version = '1.0.0'; launcherContract = 1; sharedDataContract = 1; journalSchema = 2 } | ConvertTo-Json), $utf8)
    $executable = Join-Path $packageRoot 'worklog.exe'
    $actualVersion = & $executable --version
    # --version prints the bare version as its first token; a development build appends " dev <commit> ..." after it.
    $reported = "$actualVersion".Trim()
    if ($LASTEXITCODE -ne 0 -or $reported.Split(' ')[0] -cne $PackageVersion) { throw 'Launcher/payload version smoke check failed' }
    if ($DevMarker -and -not $reported.StartsWith("$PackageVersion dev ", [System.StringComparison]::Ordinal)) { throw 'Launcher/payload version smoke check failed (expected a development label)' }
    if (-not $DevMarker -and $reported -cne $PackageVersion) { throw 'Launcher/payload version smoke check failed (stable build must print its plain version)' }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Join-Path $releaseRoot "worklog-$PackageVersion-win-x64.zip"
    $zip = [System.IO.Compression.ZipFile]::Open($archive, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $owned = @('worklog.exe', 'launcher.json', 'current.json', 'README.md', 'INSTALL.md')
        $owned += @($hashes.Keys | ForEach-Object { "versions/$PackageVersion/$_" })
        $owned += "versions/$PackageVersion/release.json"
        foreach ($name in $owned) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $packageRoot $name), $name, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose() }
    if ((Get-Item -LiteralPath $archive).Length -gt 256MB) { throw 'Package exceeds the 256 MiB download bound' }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(($archive + '.sha256'), "$hash  $([System.IO.Path]::GetFileName($archive))`n", [System.Text.Encoding]::ASCII)
    [pscustomobject]@{ Version = $PackageVersion; Root = $packageRoot; Executable = $executable; Archive = $archive; Bytes = (Get-Item -LiteralPath $archive).Length }
}
