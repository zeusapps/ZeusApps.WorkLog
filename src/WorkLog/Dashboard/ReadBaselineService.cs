namespace WorkLog.Dashboard;

internal sealed record ReadDocumentRequest(DocumentRequest Document);
internal sealed record ReadMarkRequest(DocumentRequest Document, string Revision);
/// <summary>
/// The fingerprint is <c>length:lastWriteTicks</c> as text, the same form the catalog reports for a document. Ticks exceed
/// what a JavaScript number holds exactly, so they travel as part of a string.
/// </summary>
internal sealed record ReadMarkResult(string Revision, string Fingerprint);
internal sealed record ReadSummary(Dictionary<string, string> Entries);

/// <summary>Reads and moves the per-document read baselines. A baseline only ever records content the server has just confirmed.</summary>
internal sealed class ReadBaselineService(SourceCatalog catalog, DocumentService documents, ReadBaselineStore store)
{
    public async Task<ReadBaseline?> GetAsync(ReadDocumentRequest request, CancellationToken token)
        => store.Get(await documents.BaselineKeyAsync(request?.Document!, token));

    /// <summary>
    /// Makes the current content the baseline. The document is reread, and a revision that is no longer current is rejected
    /// so a baseline never records content the user did not see.
    /// </summary>
    public async Task<ReadMarkResult> MarkAsync(ReadMarkRequest request, CancellationToken token)
    {
        if (string.IsNullOrEmpty(request?.Revision))
            throw new ArgumentException("A revision is required.");
        var current = await documents.SnapshotAsync(request.Document, fresh: true, token);
        if (current.Revision != request.Revision)
            throw new DashboardConflictException("The document changed after it was displayed. Reload it, then mark it read.");
        store.Mark(current.Key, current.Revision, current.Length, current.LastWriteTicks, current.Text);
        return new(current.Revision, current.Length + ":" + current.LastWriteTicks);
    }

    /// <summary>The stored length and write time of each remembered document that is in the current catalog.</summary>
    public async Task<ReadSummary> SummaryAsync(CancellationToken token)
    {
        var snapshot = await catalog.GetAsync(token);
        var present = snapshot.Changes.SelectMany(c => c.Documents.Select(d => DocumentService.BaselineKey(c.SourceId, c.Name, d.Path))).ToHashSet(StringComparer.Ordinal);
        return new(store.Entries().Where(pair => present.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Length + ":" + pair.Value.LastWriteTicks, StringComparer.Ordinal));
    }
}
