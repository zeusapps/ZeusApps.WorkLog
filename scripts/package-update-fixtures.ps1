param([string]$OutputDirectory = ('artifacts/update-fixtures/' + [guid]::NewGuid().ToString('N')), [switch]$NativeClients)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    . (Join-Path $PSScriptRoot 'package-core.ps1')
    $fixtureRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
    # Compiled versions differ; production packaging still enforces Directory.Build.props/tag agreement.
    $old = New-WorkLogPackage -PackageVersion '0.0.1' -OutputDirectory $fixtureRoot
    $new = New-WorkLogPackage -PackageVersion '0.0.2' -OutputDirectory $fixtureRoot
    $probe = Join-Path $fixtureRoot 'probe'
    dotnet publish tools/WorkLog.LauncherProbe/WorkLog.LauncherProbe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $probe
    if ($LASTEXITCODE -ne 0) { throw 'Launcher probe publish failed' }
    dotnet run --project tools/WorkLog.Smoke -c Release -- --launcher-only $old.Executable (Join-Path $probe 'worklog-probe.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Launcher forwarding/lifetime acceptance failed' }
    $mode = if ($NativeClients) { '--versioned-native' } else { '--versioned-updates' }
    dotnet run --project tools/WorkLog.Smoke -c Release -- $mode $old.Executable $new.Archive
    if ($LASTEXITCODE -ne 0) { throw 'Mixed-version update acceptance failed' }
} finally { Pop-Location }
