#Requires -Version 7.0
<#
.SYNOPSIS
Installs, rolls back and retires local WorkLog development builds on a versioned installation.

.DESCRIPTION
Actions (see docs/releases.md, "Development builds"):
  Preflight  Verify a dev package, its marker, freshness and contracts; write an operation record under artifacts/dev-builds/operations. Changes nothing in the installation or data directory.
  Switch     Verify, publish and select the build for new processes through the product's own update code. The original selection is recorded as previous.
  Rollback   Restore the selection from immediately before the switch (one level). Refuses when the selection changed since.
  Retire     Remove a dev-range payload that is not selected, not previous, not the current.previous.json selection, not in use and not needed by an unfinished update. Right after a Rollback or stable update it is still refused; one further selection change frees it.

It never stops or restarts MCP servers, dashboards or agent sessions, and never edits client configuration or the journal.
Defaults point at the real per-user installation: run Preflight there, but Switch, Rollback and Retire only with the maintainer's explicit authorization.
-InstallationRoot, -DataDirectory and -SkipProbe exist for tests: they are refused unless everything is under the repository artifacts folder (-SkipProbe is refused for the default installation). -Workspace must always be under the repository artifacts folder, including for the default installation.
#>
param(
    [ValidateSet('Preflight', 'Switch', 'Rollback', 'Retire')][string]$Action = 'Preflight',
    # Dev package zip. Default: the newest worklog-*-win-x64.zip under artifacts/dev-builds/.
    [string]$Package,
    # Switch/Rollback: the operation record that Preflight printed (operationManifest).
    [string]$OperationManifest,
    # Retire: the dev-range payload version to remove.
    [string]$Version,
    [string]$InstallationRoot,
    [string]$DataDirectory,
    [string]$Workspace,
    [switch]$SkipProbe
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'tools/WorkLog.DevInstall/WorkLog.DevInstall.csproj'
dotnet build $project -c Release -v quiet --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Building the dev installer failed.' }

$arguments = @($Action.ToLowerInvariant(), '--repo', $repo)
foreach ($pair in @(@('--package', $Package), @('--operation', $OperationManifest), @('--version', $Version), @('--installation', $InstallationRoot), @('--data-dir', $DataDirectory), @('--workspace', $Workspace))) {
    if ($pair[1]) { $arguments += $pair }
}
if ($SkipProbe) { $arguments += '--skip-probe' }
dotnet run --project $project -c Release --no-build -- @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
