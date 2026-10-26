using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

internal sealed record DocumentRequest(string SourceId, string Change, string Path, string? Variant = null);
internal sealed record TaskPosition(string? Number, string Text, int Line);
internal sealed record DocumentContent(string SourceId, string Change, string Path, string? Variant, string Location, string Revision, string Text,
    bool Archived, bool CanComment, TaskPosition[] Tasks);
internal sealed record CommentRequest(string SourceId, string Change, string Path, string Revision, int StartLine, int EndLine,
    string BlockText, string Comment, string? Variant = null);
internal sealed record ReviewMutationRequest(string SourceId, string Change, string Path, string Revision, int Line,
    string MarkerText, string? Comment = null, string? Id = null, string? Variant = null);
internal sealed record SearchRequest(string Query, string[]? Repositories = null, string Scope = "active");
internal sealed record SearchMatch(string SourceId, string Change, string Path, string[] Repositories, bool Archived, int Line, string? Section, string Excerpt, string Variant);
internal sealed record SearchResults(SearchMatch[] Matches, bool LimitReached, string[] Unavailable);
internal sealed record LinkTarget(string SourceId, string Change, string Path, string? Anchor);

internal sealed partial class DocumentService(SourceCatalog catalog)
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    private sealed record Cached(long Length, DateTime LastWrite, string Text, string Revision, Encoding Encoding, bool Bom);
    private readonly ConcurrentDictionary<string, Cached> cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim writeGate = new(1);
    internal int HashReads { get; private set; }

    private static (string File, bool Archived, string Location) Resolve(CatalogSnapshot snapshot, DocumentRequest request, bool write = false)
    {
        var source = snapshot.Sources.SingleOrDefault(s => s.Id == request.SourceId && s.Error is null)
            ?? throw new FileNotFoundException("Document source is unavailable.");
        if (!request.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only Markdown documents can be read.");
        if (request.Change == "@source")
        {
            if (write) throw new ArgumentException("Only active change documents accept review comments.");
            var root = SourcePath.Within(source.Root, "openspec");
            return (SourcePath.Within(root, request.Path), true, source.Location);
        }
        var change = SourceCatalog.Resolve(snapshot, request.SourceId, request.Change, request.Variant);
        // Recheck the live directory, not merely the path captured during discovery.
        _ = SourcePath.Within(source.Root, System.IO.Path.GetRelativePath(source.Root, change.Directory));
        if (write && (change.Archived || !Directory.Exists(change.Directory)))
            throw new ArgumentException("Archived or unavailable changes are read-only.");
        var file = SourcePath.Within(change.Directory, request.Path);
        return (file, change.Archived, source.Location);
    }

    private Cached Read(string path, bool fresh = false)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var before = new FileInfo(path);
            if (!before.Exists) throw new FileNotFoundException("Document is unavailable. Refresh to resolve an archive move.");
            if (before.Length > MaximumBytes) throw new ArgumentException("Document exceeds the 2 MiB read limit.");
            if (!fresh && cache.TryGetValue(path, out var existing) && before.Length == existing.Length && before.LastWriteTimeUtc == existing.LastWrite)
                return existing;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumBytes) throw new ArgumentException("Document exceeds the 2 MiB read limit.");
            var after = new FileInfo(path);
            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc || bytes.LongLength != after.Length) continue;
            var (encoding, bomLength) = DetectEncoding(bytes);
            var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
            var value = new Cached(after.Length, after.LastWriteTimeUtc, text,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), encoding, bomLength > 0);
            HashReads++;
            if (cache.Count >= 64) cache.Clear();
            cache[path] = value;
            return value;
        }
        throw new IOException("Document is changing. Retry after the agent finishes editing.");
    }

    private static (Encoding Encoding, int BomLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) return (new UTF32Encoding(false, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) return (new UTF32Encoding(true, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(true, true), 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, true, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, true, true), 2);
        return (new UTF8Encoding(false, true), 0);
    }

    public async Task<DocumentContent> GetAsync(DocumentRequest request, CancellationToken cancellationToken = default)
    {
        var snapshot = await catalog.GetAsync(cancellationToken);
        var resolved = Resolve(snapshot, request);
        var value = Read(resolved.File);
        var tasks = Newlines().Split(value.Text).Select((line, index) => (Match: TaskLine().Match(line), Index: index))
            .Where(x => x.Match.Success).Select(x => new TaskPosition(
                x.Match.Groups[1].Success ? x.Match.Groups[1].Value : null, x.Match.Groups[2].Value, x.Index)).ToArray();
        return new(request.SourceId, request.Change, request.Path, request.Variant, resolved.Location, value.Revision, value.Text, resolved.Archived,
            !resolved.Archived && request.Change != "@source", tasks);
    }

    public async Task<DocumentContent> CommentAsync(CommentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length > 16_384) throw new ArgumentException("Write a comment between 1 and 16384 characters.");
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await catalog.GetAsync(cancellationToken);
            var selection = new DocumentRequest(request.SourceId, request.Change, request.Path, request.Variant);
            var resolved = Resolve(snapshot, selection, write: true);
            var value = Read(resolved.File, fresh: true);
            var lines = Newlines().Split(value.Text);
            if (request.StartLine < 0 || request.EndLine <= request.StartLine || request.EndLine > lines.Length)
                throw new DashboardConflictException("The selected block changed. Reload the document and select it again; your comment is retained.");
            var block = string.Join('\n', lines[request.StartLine..request.EndLine]);
            if (block != Newlines().Replace(request.BlockText, "\n"))
                throw new DashboardConflictException("The selected block changed. Reload the document and select it again; your comment is retained.");
            var matches = Newlines().Matches(value.Text);
            var newline = matches.Count > 0 ? matches[0].Value : Environment.NewLine;
            var insertion = request.EndLine <= matches.Count ? matches[request.EndLine - 1].Index + matches[request.EndLine - 1].Length : value.Text.Length;
            var comment = ReviewMarker.Create(request.Comment, block, newline);
            var separator = insertion > 0 && value.Text[insertion - 1] is not ('\r' or '\n') ? newline : "";
            var text = value.Text.Insert(insertion, separator + newline + comment + newline);
            return await WriteReviewAsync(snapshot, selection, resolved.File, value, text, cancellationToken);
        }
        finally { writeGate.Release(); }
    }

    public async Task<DocumentContent> MutateCommentAsync(ReviewMutationRequest request, bool delete, CancellationToken cancellationToken = default)
    {
        if (!delete && (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length > 16_384))
            throw new ArgumentException("Write a comment between 1 and 16384 characters.");
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await catalog.GetAsync(cancellationToken);
            var selection = new DocumentRequest(request.SourceId, request.Change, request.Path, request.Variant);
            var resolved = Resolve(snapshot, selection, write: true);
            var value = Read(resolved.File, fresh: true);
            var markers = ReviewMarker.Parse(value.Text);
            var candidates = markers.Where(m => request.Id is not null ? m.Id == request.Id : m.Id is null && m.Line == request.Line).ToArray();
            var marker = candidates.Length == 1 ? candidates[0] : null;
            if (marker is null || Newlines().Replace(marker.Raw, "\n") != Newlines().Replace(request.MarkerText, "\n")
                || (marker.Id is null && request.Revision != value.Revision && markers.Count(m => m.Raw == marker.Raw) > 1))
                throw new DashboardConflictException("This review comment changed, moved, or was removed. Your edit is retained; reload and select the current note.");
            var newline = Newlines().Match(value.Text) is { Success: true } found ? found.Value : Environment.NewLine;
            var replacement = delete ? "" : ReviewMarker.Create(request.Comment!, marker.Quote, newline, marker.Id);
            var text = value.Text.Remove(marker.Start, marker.End - marker.Start).Insert(marker.Start, replacement);
            return await WriteReviewAsync(snapshot, selection, resolved.File, value, text, cancellationToken);
        }
        finally { writeGate.Release(); }
    }

    private async Task<DocumentContent> WriteReviewAsync(CatalogSnapshot snapshot, DocumentRequest selection, string file,
        Cached value, string text, CancellationToken cancellationToken)
    {
        var preamble = value.Bom ? value.Encoding.GetPreamble() : [];
        var bytes = preamble.Concat(value.Encoding.GetBytes(text)).ToArray();
        if (bytes.Length > MaximumBytes) throw new ArgumentException("The comment would exceed the 2 MiB document limit.");
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            // Best-effort protection only; external editors do not participate in this gate.
            _ = Resolve(snapshot, selection, write: true);
            File.Move(temporary, file, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        cache.TryRemove(file, out _);
        return await GetAsync(selection, cancellationToken);
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken token = default)
    {
        if (request.Query.Length is < 1 or > 500) throw new ArgumentException("Search text must contain between 1 and 500 characters.");
        if (request.Scope is not ("active" or "archived" or "all")) throw new ArgumentException("scope must be active, archived, or all.");
        var snapshot = await catalog.GetAsync(token);
        var results = new List<SearchMatch>();
        var unavailable = snapshot.Notices.Concat(snapshot.Sources.Where(s => s.Error is not null).Select(s => s.Location + ": " + s.Error)).ToList();
        foreach (var change in snapshot.Changes.Where(c => (request.Scope == "all" || c.Archived == (request.Scope == "archived"))
            && (request.Repositories is null || request.Repositories.Length == 0 || c.Repositories.Intersect(request.Repositories).Any())))
        {
            foreach (var document in change.Documents)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var resolved = Resolve(snapshot, new(change.SourceId, change.Name, document.Path, change.Variant));
                    var lines = Newlines().Split(Read(resolved.File, fresh: true).Text);
                    string? section = null;
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith('#')) section = lines[i].TrimStart('#', ' ');
                        var match = lines[i].IndexOf(request.Query, StringComparison.OrdinalIgnoreCase);
                        if (match < 0) continue;
                        if (results.Count == 50) return new(results.ToArray(), true, unavailable.ToArray());
                        var start = Math.Max(0, match - 90);
                        var excerpt = lines[i].Substring(start, Math.Min(360, lines[i].Length - start));
                        results.Add(new(change.SourceId, change.Name, document.Path, change.Repositories, change.Archived, i, section, excerpt, change.Variant));
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { unavailable.Add(change.Name + "/" + document.Path + ": " + e.Message); }
            }
        }
        return new(results.ToArray(), false, unavailable.ToArray());
    }

    public async Task<LinkTarget> LinkAsync(DocumentRequest request, string target, CancellationToken token)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out _) || target.StartsWith("//", StringComparison.Ordinal)) throw new ArgumentException("This is not a local document link.");
        var snapshot = await catalog.GetAsync(token);
        var source = snapshot.Sources.Single(s => s.Id == request.SourceId);
        var current = Resolve(snapshot, request).File;
        var parts = target.Split('#', 2);
        var candidate = parts[0].Length == 0 ? current : System.IO.Path.GetFullPath(Uri.UnescapeDataString(parts[0]), System.IO.Path.GetDirectoryName(current)!);
        var root = SourcePath.Within(source.Root, "openspec");
        var safe = SourcePath.Within(root, System.IO.Path.GetRelativePath(root, candidate));
        if (!safe.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only local Markdown documents can be opened.");
        foreach (var change in snapshot.Changes.Where(c => c.SourceId == source.Id))
        {
            var relative = System.IO.Path.GetRelativePath(change.Directory, safe).Replace('\\', '/');
            if (change.Documents.Any(d => d.Path == relative)) return new(source.Id, change.Name, relative, parts.Length > 1 ? parts[1] : null);
        }
        return new(source.Id, "@source", System.IO.Path.GetRelativePath(root, safe).Replace('\\', '/'), parts.Length > 1 ? parts[1] : null);
    }

    [GeneratedRegex("\\r\\n|\\n|\\r")] private static partial Regex Newlines();
    [GeneratedRegex(@"^\s*(?:[-*+]|\d{1,9}[.)])\s*\[[^\]]*\]\s*(?:(\d+(?:\.\d+)+(?:[A-Za-z]+)?)\s+)?(.*)$")] private static partial Regex TaskLine();
}
