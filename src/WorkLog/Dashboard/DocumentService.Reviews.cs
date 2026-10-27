using System.Collections.Concurrent;

namespace WorkLog.Dashboard;

internal sealed record ReviewsRequest(string[]? Repositories = null, string Scope = "active");
internal sealed record ReviewItem(string SourceId, string Change, string Variant, string Path, bool Archived, string[] Repositories,
    int Line, string? Id, string Quote, string Comment, bool Truncated);
internal sealed record ReviewList(ReviewItem[] Items, string[] Unavailable);
/// <summary>The current content of one document, with the key under which its read baseline is stored.</summary>
internal sealed record ReadSnapshot(string Key, string Revision, string Text, long Length, long LastWriteTicks);

internal sealed partial class DocumentService
{
    internal const int ReviewQuoteLimit = 280;
    internal const int ReviewCommentLimit = 500;
    private sealed record ParsedMarkers(string Fingerprint, ReviewMarker[] Markers);
    private readonly Dictionary<string, ParsedMarkers> markerCache = new(StringComparer.Ordinal);
    private readonly object markerGate = new();
    private int markerParses;
    private int changeResolutions;
    /// <summary>How many documents have been parsed for review markers, as opposed to answered from the marker cache.</summary>
    internal int MarkerParses => Volatile.Read(ref markerParses);
    internal int MarkerCacheSize { get { lock (markerGate) return markerCache.Count; } }
    /// <summary>How many times a listing canonicalized a change directory; a listing answered from the marker cache resolves none.</summary>
    internal int ChangeResolutions => Volatile.Read(ref changeResolutions);

    /// <summary>
    /// Lists the review markers currently present in change documents. A marker is open while it is in the file, so the list
    /// is derived from the documents on every call. The parse is cached per catalog identity and the catalog's own length and
    /// write-time fingerprint of the document: an unchanged document costs a lookup, with no path resolution and no stat.
    /// A cached entry can therefore be at most one catalog refresh (2 s) behind the file. The cache is bounded by the catalog:
    /// after each listing, the entries of documents that are no longer in the catalog snapshot are removed, never by age or count.
    /// </summary>
    public async Task<ReviewList> ReviewsAsync(ReviewsRequest request, CancellationToken token = default)
    {
        if (request.Scope is not ("active" or "archived" or "all"))
            throw new ArgumentException("scope must be active, archived, or all.");
        var snapshot = await catalog.GetAsync(token);
        var (selected, unavailable) = Unavailable(snapshot, request.Repositories);
        bool InScope(IEnumerable<string> repositories) => selected is null || repositories.Intersect(selected).Any();
        var items = new List<ReviewItem>();
        foreach (var change in snapshot.Changes.Where(c => (request.Scope == "all" || c.Archived == (request.Scope == "archived")) && InScope(c.Repositories)))
        {
            // Resolved on the first cache miss of this change, then reused by its other documents.
            string? directory = null;
            Exception? failure = null;
            foreach (var document in change.Documents)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var markers = Markers(snapshot, change, document, ref directory, ref failure);
                    foreach (var marker in markers)
                    {
                        var truncated = false;
                        var quote = Excerpt(marker.Quote, ReviewQuoteLimit, ellipsis: true, ref truncated);
                        var comment = Excerpt(marker.Comment, ReviewCommentLimit, ellipsis: false, ref truncated);
                        items.Add(new(change.SourceId, change.Name, change.Variant, document.Path, change.Archived, change.Repositories,
                            marker.Line, marker.Id, quote, comment, truncated));
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { unavailable.Add(change.Name + "/" + document.Path + ": " + e.Message); }
            }
        }
        PruneMarkerCache(snapshot);
        return new(items.ToArray(), unavailable.ToArray());
    }

    private static string MarkerKey(ChangeInfo change, ChangeDocument document)
        => string.Join('|', change.SourceId, change.Variant, change.DirectoryName, document.Path);

    /// <summary>Removes the entries of documents that are not in the whole catalog snapshot, so a listing over one scope keeps the others warm.</summary>
    private void PruneMarkerCache(CatalogSnapshot snapshot)
    {
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in snapshot.Changes)
            foreach (var document in change.Documents)
                live.Add(MarkerKey(change, document));
        lock (markerGate)
        {
            foreach (var key in markerCache.Keys.Where(k => !live.Contains(k)).ToList())
                markerCache.Remove(key);
        }
    }

    private ReviewMarker[] Markers(CatalogSnapshot snapshot, ChangeInfo change, ChangeDocument document, ref string? directory, ref Exception? failure)
    {
        var key = MarkerKey(change, document);
        lock (markerGate)
        {
            if (markerCache.TryGetValue(key, out var hit) && hit.Fingerprint == document.Fingerprint)
                return hit.Markers;
        }
        if (failure is not null) throw failure;
        string file;
        try
        {
            directory ??= ResolveChangeDirectory(snapshot, change);
            if (!document.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only Markdown documents can be read.");
            file = SourcePath.Within(change.Directory, directory, document.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (directory is null) failure = e;
            throw;
        }
        // Parsing must not push the reader's documents out of their cache.
        var value = Read(file, cacheResult: false);
        var markers = ReviewMarker.Parse(value.Text);
        Interlocked.Increment(ref markerParses);
        lock (markerGate)
        {
            markerCache[key] = new(document.Fingerprint, markers);
        }
        return markers;
    }

    /// <summary>The canonical directory of a change, after rechecking that the live directory is still inside its source.</summary>
    private string ResolveChangeDirectory(CatalogSnapshot snapshot, ChangeInfo change)
    {
        var source = snapshot.Sources.SingleOrDefault(s => s.Id == change.SourceId && s.Error is null)
            ?? throw new FileNotFoundException("Document source is unavailable.");
        Interlocked.Increment(ref changeResolutions);
        _ = SourcePath.Within(source.Root, System.IO.Path.GetRelativePath(source.Root, change.Directory));
        return SourcePath.Canonical(change.Directory);
    }

    /// <summary>Cuts a value to <paramref name="limit"/> characters without splitting a surrogate pair. An existing trailing ellipsis does not count.</summary>
    private static string Excerpt(string value, int limit, bool ellipsis, ref bool truncated)
    {
        var body = ellipsis && value.EndsWith('…') ? value[..^1] : value;
        if (body.Length <= limit)
            return value;
        truncated = true;
        var end = char.IsHighSurrogate(body[limit - 1]) ? limit - 1 : limit;
        return body[..end] + (ellipsis ? "…" : "");
    }

    /// <summary>Rejects a request that names no document, so malformed input is a client error rather than a failure of the server.</summary>
    private static void RequireDocument(DocumentRequest? request)
    {
        if (request is not { SourceId: not null, Change: not null, Path: not null })
            throw new ArgumentException("A document is required: sourceId, change, and path.");
    }

    /// <summary>The key of a document's read baseline. The variant is left out so an archive move keeps the baseline.</summary>
    internal static string BaselineKey(string sourceId, string change, string path) => sourceId + ":" + change + ":" + path.Replace('\\', '/');

    private static string BaselineKey(CatalogSnapshot snapshot, DocumentRequest request)
        => BaselineKey(request.SourceId, request.Change == "@source" ? "@source" : SourceCatalog.Find(snapshot, request.SourceId, request.Change, request.Variant).Name, request.Path);

    /// <summary>Resolves a document request to its baseline key without reading the file.</summary>
    internal async Task<string> BaselineKeyAsync(DocumentRequest request, CancellationToken token)
    {
        RequireDocument(request);
        var snapshot = await catalog.GetAsync(token);
        _ = Resolve(snapshot, request);
        return BaselineKey(snapshot, request);
    }

    /// <summary>Reads a document for the baseline store. <paramref name="fresh"/> rereads the bytes instead of trusting the length and write-time cache.</summary>
    internal async Task<ReadSnapshot> SnapshotAsync(DocumentRequest request, bool fresh, CancellationToken token)
    {
        RequireDocument(request);
        var snapshot = await catalog.GetAsync(token);
        var resolved = Resolve(snapshot, request);
        var value = Read(resolved.File, fresh);
        return new(BaselineKey(snapshot, request), value.Revision, value.Text, value.Length, value.LastWrite.Ticks);
    }
}
