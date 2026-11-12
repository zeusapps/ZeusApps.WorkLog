[CmdletBinding()]
param(
    [ValidateSet('Baseline', 'WorkingTree')]
    [string] $Source = 'WorkingTree',
    [string] $BaselineCommit = '0d7768581a0305725454dc33d610924cce9d61ef',
    [string] $Name = 'after',
    [switch] $CheckNewlines
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/realtime-dashboard-updates/baseline/instrumented'))
$targetRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot $Name))
$separator = [System.IO.Path]::DirectorySeparatorChar
if (-not $targetRoot.StartsWith($artifactRoot.TrimEnd($separator) + $separator, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Instrumented snapshot target must remain below artifacts/realtime-dashboard-updates/baseline/instrumented.'
}
if ($Name -notmatch '^[a-z0-9][a-z0-9-]{0,30}$') { throw 'Name must be a short lowercase artifact label.' }

function Normalize-TemplateForTarget([string] $Template, [string] $TargetNewline) {
    return $Template.Replace("`r`n", "`n").Replace("`n", $TargetNewline)
}

function Assert-NewlineNormalization {
    foreach ($templateNewline in @("`n", "`r`n")) {
        foreach ($targetNewline in @("`n", "`r`n")) {
            $oldTemplate = 'before' + $templateNewline + 'after'
            $newTemplate = 'begin' + $templateNewline + 'end'
            $targetAnchor = 'before' + $targetNewline + 'after'
            $targetReplacement = 'begin' + $targetNewline + 'end'
            if ((Normalize-TemplateForTarget $oldTemplate $targetNewline) -cne $targetAnchor) {
                throw 'Newline self-check failed to normalize a guarded source anchor.'
            }
            if ((Normalize-TemplateForTarget $newTemplate $targetNewline) -cne $targetReplacement) {
                throw 'Newline self-check failed to normalize a guarded replacement.'
            }
            if (([regex]::Matches($targetAnchor, [regex]::Escape((Normalize-TemplateForTarget $oldTemplate $targetNewline)))).Count -ne 1) {
                throw 'Newline self-check did not match exactly one source anchor.'
            }
        }
    }
}

Assert-NewlineNormalization
if ($CheckNewlines) {
    Write-Output 'Template/target newline matrix passed (LF/CRLF x LF/CRLF).'
    return
}
if (Test-Path -LiteralPath $targetRoot) { throw "Instrumented snapshot already exists: $Name. Choose a new label; snapshots are immutable." }

$stageRoot = Join-Path $artifactRoot ('.stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
try {
    if ($Source -eq 'Baseline') {
        $archive = Join-Path $stageRoot 'source.tar'
        & git -C $repoRoot archive --format=tar --output=$archive $BaselineCommit -- Directory.Build.props global.json src/WorkLog dashboard
        if ($LASTEXITCODE -ne 0) { throw "git archive failed for $BaselineCommit." }
        & tar -xf $archive -C $stageRoot
        if ($LASTEXITCODE -ne 0) { throw 'Could not extract the baseline source archive.' }
        Remove-Item -LiteralPath $archive -Force

        # Dashboard build output is ignored by Git. Preserve the baseline bundle as it exists at snapshot time.
        $liveDist = Join-Path $repoRoot 'dashboard/dist'
        if (-not (Test-Path -LiteralPath (Join-Path $liveDist 'dashboard/build.stamp'))) {
            throw 'Baseline dashboard build stamp is missing; do not build during an active baseline measurement window.'
        }
        Copy-Item -LiteralPath $liveDist -Destination (Join-Path $stageRoot 'dashboard/dist') -Recurse
        $sourceIdentity = (& git -C $repoRoot rev-parse $BaselineCommit).Trim()
        $dashboardOrigin = 'ignored dashboard/dist copied at snapshot time; SHA-256 recorded in manifest'
    }
    else {
        Copy-Item -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Destination $stageRoot
        Copy-Item -LiteralPath (Join-Path $repoRoot 'global.json') -Destination $stageRoot
        $sourceCopy = Join-Path $stageRoot 'src'
        New-Item -ItemType Directory -Path $sourceCopy -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot 'src/WorkLog') -Destination $sourceCopy -Recurse
        $dashboardCopy = Join-Path $stageRoot 'dashboard'
        New-Item -ItemType Directory -Path $dashboardCopy -Force | Out-Null
        foreach ($relative in @('src', 'dist', 'scripts/stamp.mjs', 'package.json', 'package-lock.json', 'angular.json', 'tsconfig.json', 'tsconfig.app.json', 'tsconfig.spec.json')) {
            $inputPath = Join-Path $repoRoot ('dashboard/' + $relative)
            if (Test-Path -LiteralPath $inputPath -PathType Container) { Copy-Item -LiteralPath $inputPath -Destination $dashboardCopy -Recurse }
            elseif (Test-Path -LiteralPath $inputPath -PathType Leaf) {
                $destination = Join-Path $dashboardCopy $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                Copy-Item -LiteralPath $inputPath -Destination $destination
            }
        }
        foreach ($relative in @('src/WorkLog/bin', 'src/WorkLog/obj', 'dashboard/node_modules', 'dashboard/.angular', 'dashboard/playwright-report', 'dashboard/test-results')) {
            $excluded = Join-Path $stageRoot $relative
            if (Test-Path -LiteralPath $excluded) { Remove-Item -LiteralPath $excluded -Recurse -Force }
        }
        $sourceIdentity = (& git -C $repoRoot rev-parse HEAD).Trim()
        $dashboardOrigin = 'working tree source and current dashboard/dist; source changes included'
    }

    $instrumentRoot = Join-Path $stageRoot 'src/WorkLog'
    if (-not (Test-Path -LiteralPath $instrumentRoot)) { throw 'Snapshot is missing src/WorkLog.' }
    $anchors = [System.Collections.Generic.List[object]]::new()
    function Get-TreeSha256([string] $root) {
        $treeHash = [System.Security.Cryptography.IncrementalHash]::CreateHash([System.Security.Cryptography.HashAlgorithmName]::SHA256)
        $treeFiles = Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object { $_.FullName -notlike '*\obj\*' -and $_.FullName -notlike '*\bin\*' }
        foreach ($file in $treeFiles | Sort-Object FullName) {
            $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            $treeHash.AppendData([System.Text.Encoding]::UTF8.GetBytes($relative + "`n"))
            $treeHash.AppendData([System.IO.File]::ReadAllBytes($file.FullName))
        }
        $result = [Convert]::ToHexString($treeHash.GetHashAndReset()).ToLowerInvariant()
        $treeHash.Dispose()
        return $result
    }
    function Replace-ExactlyOne([string] $RelativePath, [string] $Old, [string] $New, [string] $Label) {
        $path = Join-Path $stageRoot $RelativePath
        $text = [System.IO.File]::ReadAllText($path)
        $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $oldText = Normalize-TemplateForTarget $Old $newline
        $newText = Normalize-TemplateForTarget $New $newline
        $count = ([regex]::Matches($text, [regex]::Escape($oldText))).Count
        if ($count -ne 1) { throw "Transform '$Label' expected exactly one anchor in $RelativePath; found $count." }
        [System.IO.File]::WriteAllText($path, $text.Replace($oldText, $newText), [System.Text.UTF8Encoding]::new($false))
        $anchors.Add([ordered]@{ label = $Label; path = $RelativePath.Replace('\', '/'); anchorCount = $count; anchorSha256 = (Get-Sha256Text $Old) })
    }
    function Get-Sha256Text([string] $Value) {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    }
    function Add-CountedExecutions([string] $RelativePath, [string] $MethodName, [int] $Minimum) {
        $path = Join-Path $stageRoot $RelativePath
        $text = [System.IO.File]::ReadAllText($path)
        $pattern = '(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\.' + [regex]::Escape($MethodName) + '\(\)'
        $matches = [regex]::Matches($text, $pattern)
        if ($matches.Count -lt $Minimum) { throw "Instrumentation '$MethodName' expected at least $Minimum executions in $RelativePath; found $($matches.Count)." }
        $text = [regex]::Replace($text, $pattern, 'WorkLog.Dashboard.C06Metrics.Sqlite(() => ${receiver}.' + $MethodName + '())')
        [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
        $anchors.Add([ordered]@{ label = "sqlite-$($MethodName.ToLowerInvariant())"; path = $RelativePath.Replace('\', '/'); anchorCount = $matches.Count; anchorSha256 = (Get-Sha256Text $pattern) })
    }
    function Add-CountedStaticCalls([string] $RelativePath, [string] $TypeName, [string] $MethodName, [string] $ReplacementName, [int] $Minimum) {
        $path = Join-Path $stageRoot $RelativePath
        $text = [System.IO.File]::ReadAllText($path)
        $pattern = [regex]::Escape($TypeName + '.' + $MethodName) + '\('
        $matches = [regex]::Matches($text, $pattern)
        if ($matches.Count -lt $Minimum) { throw "Instrumentation '$TypeName.$MethodName' expected at least $Minimum callsites in $RelativePath; found $($matches.Count)." }
        $text = [regex]::Replace($text, $pattern, 'C06Metrics.AfterOnly' + $ReplacementName + '(')
        [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
        $anchors.Add([ordered]@{ label = "after-only-$($TypeName.ToLowerInvariant())-$($MethodName.ToLowerInvariant())"; path = $RelativePath.Replace('\', '/'); anchorCount = $matches.Count; anchorSha256 = (Get-Sha256Text $pattern) })
    }
    $sourceHash = Get-TreeSha256 $stageRoot

    $sourcePath = 'src/WorkLog/Dashboard/SourcePath.cs'
    Replace-ExactlyOne $sourcePath '            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);' '            C06Metrics.MetadataProbe();
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);' 'canonical-path metadata probes'
    Replace-ExactlyOne $sourcePath '                var info = new FileInfo(target);' '                C06Metrics.MetadataProbe();
                var info = new FileInfo(target);' 'markdown-link metadata probes'
    Replace-ExactlyOne $sourcePath '        return new FileSystemEnumerable<Item>(directory, (ref FileSystemEntry e) => new Item(e.ToFullPath(), e.IsDirectory,
            (e.Attributes & FileAttributes.ReparsePoint) != 0, e.IsDirectory ? 0 : e.Length, e.LastWriteTimeUtc.UtcTicks), options).ToList();' '        var items = new FileSystemEnumerable<Item>(directory, (ref FileSystemEntry e) => new Item(e.ToFullPath(), e.IsDirectory,
            (e.Attributes & FileAttributes.ReparsePoint) != 0, e.IsDirectory ? 0 : e.Length, e.LastWriteTimeUtc.UtcTicks), options).ToList();
        C06Metrics.MetadataEnumeration(items.Count);
        return items;' 'source-directory enumeration completion'

    $catalogPath = 'src/WorkLog/Dashboard/SourceCatalog.cs'
    Replace-ExactlyOne $catalogPath '        var info = new FileInfo(path);' '        C06Metrics.MetadataProbe();
        var info = new FileInfo(path);' 'catalog file stamps'

    $afterOnlyCoverage = @()
    $liveCoordinatorPath = 'src/WorkLog/Dashboard/DashboardLiveCoordinator.cs'
    if (Test-Path -LiteralPath (Join-Path $stageRoot $liveCoordinatorPath)) {
        Replace-ExactlyOne $liveCoordinatorPath '            var info = new FileInfo(path);' '            C06Metrics.AfterOnlyMetadataProbe();
            var info = new FileInfo(path);' 'live revision file-signature metadata probes'
        Add-CountedStaticCalls $liveCoordinatorPath 'Directory' 'Exists' 'DirectoryExists' 8
        $afterOnlyCoverage += [ordered]@{ counter = 'afterOnlyMetadataProbes'; sourcePath = $liveCoordinatorPath; callsite = 'Directory.Exists'; baselineExpected = 0 }
        Add-CountedStaticCalls $liveCoordinatorPath 'File' 'Exists' 'FileExists' 2
        $afterOnlyCoverage += [ordered]@{ counter = 'afterOnlyMetadataProbes'; sourcePath = $liveCoordinatorPath; callsite = 'File.Exists'; baselineExpected = 0 }
        Add-CountedStaticCalls $liveCoordinatorPath 'Directory' 'GetLastWriteTimeUtc' 'DirectoryLastWriteTimeUtc' 1
        $afterOnlyCoverage += [ordered]@{ counter = 'afterOnlyMetadataProbes'; sourcePath = $liveCoordinatorPath; callsite = 'Directory.GetLastWriteTimeUtc'; baselineExpected = 0 }
        $afterOnlyCoverage += [ordered]@{
            counter = 'afterOnlyMetadataProbes'
            sourcePath = $liveCoordinatorPath
            baselineExpected = 0
            afterLabel = 'live revision file-signature metadata probes'
        }
    }
    else {
        $afterOnlyCoverage += [ordered]@{
            counter = 'afterOnlyMetadataProbes'
            sourcePath = $liveCoordinatorPath
            baselineExpected = 0
            afterLabel = $null
        }
    }
    $liveSourcePath = 'src/WorkLog/Dashboard/DashboardChangeSource.cs'
    if (Test-Path -LiteralPath (Join-Path $stageRoot $liveSourcePath)) {
        Add-CountedStaticCalls $liveSourcePath 'Directory' 'Exists' 'DirectoryExists' 3
        $afterOnlyCoverage += [ordered]@{
            counter = 'afterOnlyMetadataProbes'
            sourcePath = $liveSourcePath
            callsite = 'Directory.Exists'
            baselineExpected = 0
        }
    }

    $documentPath = 'src/WorkLog/Dashboard/DocumentService.cs'
    Replace-ExactlyOne $documentPath '            var before = new FileInfo(path);' '            C06Metrics.MetadataProbe();
            var before = new FileInfo(path);' 'document pre-read metadata'
    Replace-ExactlyOne $documentPath '            var bytes = File.ReadAllBytes(path);' '            var bytes = File.ReadAllBytes(path);
            C06Metrics.DocumentContentRead(bytes.LongLength);' 'document content reads'
    Replace-ExactlyOne $documentPath '            var after = new FileInfo(path);' '            C06Metrics.MetadataProbe();
            var after = new FileInfo(path);' 'document post-read metadata'
    Replace-ExactlyOne $documentPath '                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), encoding, bomLength > 0, Interlocked.Increment(ref order));' '                Convert.ToHexStringLower(C06Metrics.DocumentSha256(bytes.LongLength, System.Security.Cryptography.SHA256.HashData(bytes))), encoding, bomLength > 0, Interlocked.Increment(ref order));' 'document content hashes'

    $journalPath = 'src/WorkLog/Dashboard/DashboardJournal.cs'
    Add-CountedExecutions $journalPath 'ExecuteReader' 3
    Add-CountedExecutions $journalPath 'ExecuteScalar' 4
    Replace-ExactlyOne $journalPath '            entries.Add(Json.Decode<Entry>(reader.GetString(0)));' '            C06Metrics.JournalEntryDecoded();
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));' 'journal entry payload decodes'

    $digestPath = 'src/WorkLog/Persistence/DigestStore.cs'
    Add-CountedExecutions $digestPath 'ExecuteReader' 1
    Replace-ExactlyOne $digestPath '        while (reader.Read())
            result.Add(new(reader.GetString(0), reader.GetString(1), Json.Decode<string[]>(reader.GetString(2)),' '        while (reader.Read())
        {
            WorkLog.Dashboard.C06Metrics.DigestRowMaterialized();
            result.Add(new(reader.GetString(0), reader.GetString(1), Json.Decode<string[]>(reader.GetString(2)),' 'digest rows materialized start'
    Replace-ExactlyOne $digestPath '                reader.GetString(3), new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero)));
        return result.ToArray();' '                reader.GetString(3), new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero)));
        }
        return result.ToArray();' 'digest rows materialized end'

    $sqliteCommandsPath = 'src/WorkLog/Persistence/SqliteCommands.cs'
    Replace-ExactlyOne $sqliteCommandsPath '        return command.ExecuteScalar();' '        return WorkLog.Dashboard.C06Metrics.Sqlite(() => command.ExecuteScalar());' 'shared SQLite scalar executions'
    Replace-ExactlyOne $sqliteCommandsPath '        command.ExecuteNonQuery();' '        WorkLog.Dashboard.C06Metrics.Sqlite(() => command.ExecuteNonQuery());' 'shared SQLite non-query executions'

    $commandPath = 'src/WorkLog/Dashboard/LocalCommand.cs'
    Replace-ExactlyOne $commandPath '        try { process.Start(); }' '        C06Metrics.CommandStartAttempt(executable);
        try
        {
            process.Start();
            C06Metrics.CommandStarted(executable);
        }' 'git/OpenSpec process start boundaries'

    $hostPath = 'src/WorkLog/Dashboard/DashboardHost.cs'
    Replace-ExactlyOne $hostPath '        app.MapGet("/api/info", () => new { timezone =' '        app.MapGet("/api/c06-metrics", () => C06Metrics.Snapshot());
        app.MapGet("/api/info", () => new { timezone =' 'copy-only protected measurement endpoint'

    $metricsSource = @'
using System.Diagnostics;

namespace WorkLog.Dashboard;

internal static class C06Metrics
{
    private static long metadataEnumerations;
    private static long metadataEntries;
    private static long metadataProbes;
    private static long afterOnlyMetadataProbes;
    private static long documentContentReads;
    private static long documentContentReadBytes;
    private static long documentSha256Hashes;
    private static long documentSha256Bytes;
    private static long sqliteCommands;
    private static long journalEntryDecodes;
    private static long digestRowsMaterialized;
    private static long gitCommandStartAttempts;
    private static long gitCommandStarts;
    private static long openSpecCommandStartAttempts;
    private static long openSpecCommandStarts;

    public static void MetadataEnumeration(int entries)
    {
        Interlocked.Increment(ref metadataEnumerations);
        Interlocked.Add(ref metadataEntries, entries);
    }

    public static void MetadataProbe() => Interlocked.Increment(ref metadataProbes);
    public static void AfterOnlyMetadataProbe() => Interlocked.Increment(ref afterOnlyMetadataProbes);
    public static bool AfterOnlyDirectoryExists(string path) { AfterOnlyMetadataProbe(); return Directory.Exists(path); }
    public static bool AfterOnlyFileExists(string path) { AfterOnlyMetadataProbe(); return File.Exists(path); }
    public static DateTime AfterOnlyDirectoryLastWriteTimeUtc(string path) { AfterOnlyMetadataProbe(); return Directory.GetLastWriteTimeUtc(path); }

    public static void DocumentContentRead(long bytes)
    {
        Interlocked.Increment(ref documentContentReads);
        Interlocked.Add(ref documentContentReadBytes, bytes);
    }

    public static byte[] DocumentSha256(long bytes, byte[] hash)
    {
        Interlocked.Increment(ref documentSha256Hashes);
        Interlocked.Add(ref documentSha256Bytes, bytes);
        return hash;
    }

    public static T Sqlite<T>(Func<T> execute)
    {
        Interlocked.Increment(ref sqliteCommands);
        return execute();
    }

    public static void JournalEntryDecoded() => Interlocked.Increment(ref journalEntryDecodes);
    public static void DigestRowMaterialized() => Interlocked.Increment(ref digestRowsMaterialized);

    public static void CommandStartAttempt(string executable)
    {
        if (executable.Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref gitCommandStartAttempts);
        else if (executable.Equals("openspec", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref openSpecCommandStartAttempts);
    }

    public static void CommandStarted(string executable)
    {
        if (executable.Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref gitCommandStarts);
        else if (executable.Equals("openspec", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref openSpecCommandStarts);
    }

    public static object Snapshot()
    {
        using var process = Process.GetCurrentProcess();
        return new
        {
            schemaVersion = 1,
            scope = "instrumented-worklog-method-boundaries-not-physical-io",
            counters = new
            {
                metadataEnumerations = Interlocked.Read(ref metadataEnumerations),
                metadataEntries = Interlocked.Read(ref metadataEntries),
                metadataProbes = Interlocked.Read(ref metadataProbes),
                afterOnlyMetadataProbes = Interlocked.Read(ref afterOnlyMetadataProbes),
                documentContentReads = Interlocked.Read(ref documentContentReads),
                documentContentReadBytes = Interlocked.Read(ref documentContentReadBytes),
                documentSha256Hashes = Interlocked.Read(ref documentSha256Hashes),
                documentSha256Bytes = Interlocked.Read(ref documentSha256Bytes),
                sqliteCommands = Interlocked.Read(ref sqliteCommands),
                journalEntryDecodes = Interlocked.Read(ref journalEntryDecodes),
                digestRowsMaterialized = Interlocked.Read(ref digestRowsMaterialized),
                gitCommandStartAttempts = Interlocked.Read(ref gitCommandStartAttempts),
                gitCommandStarts = Interlocked.Read(ref gitCommandStarts),
                openSpecCommandStartAttempts = Interlocked.Read(ref openSpecCommandStartAttempts),
                openSpecCommandStarts = Interlocked.Read(ref openSpecCommandStarts)
            },
            process = new
            {
                gcAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true),
                totalProcessorTimeTicks = process.TotalProcessorTime.Ticks
            }
        };
    }
}
'@
    $metricsPath = Join-Path $instrumentRoot 'Dashboard/C06Metrics.cs'
    [System.IO.File]::WriteAllText($metricsPath, $metricsSource, [System.Text.UTF8Encoding]::new($false))

    # Keep the copied dashboard bundle as an input, while ensuring this harness never builds or writes into the live dashboard tree.
    $projectPath = Join-Path $instrumentRoot 'WorkLog.csproj'
    $projectText = [System.IO.File]::ReadAllText($projectPath)
    $projectAnchor = '<Target Name="BuildDashboard" BeforeTargets="PrepareForBuild"'
    $projectCount = ([regex]::Matches($projectText, [regex]::Escape($projectAnchor))).Count
    if ($projectCount -ne 1) { throw "Expected one copied dashboard-build target anchor; found $projectCount." }
    $projectText = $projectText.Replace($projectAnchor, '<Target Name="BuildDashboard" Condition="''$(C06BuildDashboard)'' == ''true''" BeforeTargets="PrepareForBuild"')
    [System.IO.File]::WriteAllText($projectPath, $projectText, [System.Text.UTF8Encoding]::new($false))
    $anchors.Add([ordered]@{ label = 'disable-live-dashboard-build-by-default'; path = 'src/WorkLog/WorkLog.csproj'; anchorCount = 1; anchorSha256 = (Get-Sha256Text $projectAnchor) })

    $files = Get-ChildItem -LiteralPath $stageRoot -File -Recurse | Where-Object { $_.FullName -notlike '*\obj\*' -and $_.FullName -notlike '*\bin\*' }
    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash([System.Security.Cryptography.HashAlgorithmName]::SHA256)
    foreach ($file in $files | Sort-Object FullName) {
        $relative = [System.IO.Path]::GetRelativePath($stageRoot, $file.FullName).Replace('\', '/')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $hash.AppendData($bytes)
        $hash.AppendData([System.IO.File]::ReadAllBytes($file.FullName))
    }
    $targetHash = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    $hash.Dispose()
    $transformHash = Get-Sha256Text (ConvertTo-Json -InputObject @($anchors) -Compress -Depth 8)
    $instrumenterHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = [ordered]@{
        schemaVersion = 1
        source = $Source
        sourceIdentity = $sourceIdentity
        baselineCommit = if ($Source -eq 'Baseline') { $BaselineCommit } else { $null }
        dashboardOrigin = $dashboardOrigin
        sourceInputsSha256 = $sourceHash
        instrumenterSha256 = $instrumenterHash
        transformsSha256 = $transformHash
        instrumentedTargetSha256 = $targetHash
        metricsEndpoint = '/api/c06-metrics (copy-only; existing /api authorization middleware applies)'
        metricScope = 'Cumulative method-boundary invocation and byte counts; not physical or kernel I/O.'
        counters = @('metadataEnumerations','metadataEntries','metadataProbes','afterOnlyMetadataProbes','documentContentReads','documentContentReadBytes','documentSha256Hashes','documentSha256Bytes','sqliteCommands','journalEntryDecodes','digestRowsMaterialized','gitCommandStartAttempts','gitCommandStarts','openSpecCommandStartAttempts','openSpecCommandStarts','gcAllocatedBytes','totalProcessorTimeTicks')
        coveredCallsites = @(
            'SourcePath.List: enumeration invocations and FileSystemEntry count',
            'SourcePath.Canonical and MarkdownFiles: explicit metadata probes',
            'SourceCatalog.FileStamp: catalog input metadata probes',
            'DashboardLiveCoordinator.FileSignature: after-only read-only journal/digest/read-index metadata probes in afterOnlyMetadataProbes; baseline zero callsites',
            'DashboardLiveCoordinator and FileSystemDashboardChangeSource: after-only explicit file/directory existence and directory-write metadata probes in afterOnlyMetadataProbes; baseline zero callsites',
            'DocumentService.Read: pre/post metadata, file content reads/bytes, SHA-256 count/bytes',
            'DashboardJournal: direct ExecuteReader/ExecuteScalar executions and payload Entry decodes',
            'DigestStore: direct query executions and result rows materialized',
            'SqliteCommands: scalar and non-query executions',
            'LocalCommand.RunAsync: Git/OpenSpec process start attempts and successful starts',
            'C06Metrics.Snapshot: GC.GetTotalAllocatedBytes and Process.TotalProcessorTime')
        afterOnlyCoverage = $afterOnlyCoverage
        transforms = $anchors
    }
    [System.IO.File]::WriteAllText((Join-Path $stageRoot 'instrumentation-manifest.json'), ($manifest | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $stageRoot -Destination $targetRoot
    Write-Output "Instrumented snapshot created: artifacts/realtime-dashboard-updates/baseline/instrumented/$Name"
    Write-Output "Source identity: $sourceIdentity"
    Write-Output "Target SHA-256: $targetHash"
}
catch {
    throw
}
finally {
    if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
}
