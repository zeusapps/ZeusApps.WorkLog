using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

internal sealed record ReadEntry(string Revision, long Length, long LastWriteTicks, DateTimeOffset ReadAt, string Blob);
internal sealed record ReadBaseline(string Revision, string Text);
internal sealed class ReadIndex
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, ReadEntry> Entries { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The index on disk was written by another schema version. It is left untouched and nothing is remembered or reported from it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Foreign { get; init; }
}

/// <summary>
/// The text of each document as the user last read it, kept in the private data directory under <c>dashboard-reads/</c>:
/// an index plus content-addressed blobs. The store is bounded in documents and bytes and discards the least recently read
/// entries first. Deleting the directory only resets the baselines.
/// An index is replaced only on positive evidence of damage (invalid or oversized content, a hash mismatch). A failure to read
/// or write a file (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) is not evidence of damage: it propagates, and
/// nothing is written or deleted. An index of another schema version is preserved and the store reports no baselines.
/// </summary>
internal sealed partial class ReadBaselineStore(string dataDirectory, TimeProvider? time = null)
{
    internal const int MaximumKeys = 300;
    internal const long MaximumBytes = 64L * 1024 * 1024;
    private const long MaximumIndexBytes = 4 * 1024 * 1024;
    /// <summary>An entry read again within this span is not rewritten, so opening a document many times does not rewrite the index each time.</summary>
    internal static readonly TimeSpan TouchInterval = TimeSpan.FromSeconds(60);
    // Process-wide: every store over any directory serializes its index and blob updates.
    private static readonly object Gate = new();
    private readonly string directory = Path.Combine(dataDirectory, "dashboard-reads");
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private string IndexPath => Path.Combine(directory, "index.json");
    private string BlobDirectory => Path.Combine(directory, "blobs");
    private string BlobPath(string blob) => Path.Combine(BlobDirectory, blob + ".md");

    /// <summary>The remembered content of a document, or null when none is remembered or its blob is damaged.</summary>
    public ReadBaseline? Get(string key)
    {
        lock (Gate)
        {
            var index = Load();
            if (!index.Entries.TryGetValue(key, out var entry))
                return null;
            var text = ReadBlob(entry.Blob);
            if (text is null)
            {
                // The blob is missing or does not match its digest: positive evidence that it is lost. A blob that cannot be read throws instead.
                index.Entries.Remove(key);
                Save(index);
                return null;
            }
            var now = clock.GetUtcNow();
            if (now - entry.ReadAt >= TouchInterval)
            {
                index.Entries[key] = entry with
                {
                    ReadAt = Next(index, now)
                };
                try { Save(index); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // The read still succeeds; only its recency is not recorded.
            }
            return new(entry.Revision, text);
        }
    }

    /// <summary>Remembers <paramref name="text"/> as the content last read of <paramref name="key"/>, then enforces the bounds.</summary>
    public void Mark(string key, string revision, long length, long lastWriteTicks, string text)
    {
        lock (Gate)
        {
            var index = Load();
            if (index.Foreign)
                throw new IOException("Read baselines were written by a newer WorkLog and are left untouched.");
            var bytes = Encoding.UTF8.GetBytes(text);
            var blob = Convert.ToHexStringLower(SHA256.HashData(bytes));
            // The blob is written before the index names it, so a crash leaves an unreferenced blob rather than a dangling entry.
            if (ReadBlob(blob) is null)
                AtomicFile.Write(BlobPath(blob), text);
            index.Entries[key] = new(revision, length, lastWriteTicks, Next(index, clock.GetUtcNow()), blob);
            Enforce(index, key);
            Save(index);
            DeleteUnreferenced(index);
        }
    }

    /// <summary>The length and write time stored for each remembered document.</summary>
    public Dictionary<string, ReadEntry> Entries()
    {
        lock (Gate)
            return new(Load().Entries, StringComparer.Ordinal);
    }

    // Read times only ever increase, so the order of reads is exact even when the clock does not move between two marks.
    private static DateTimeOffset Next(ReadIndex index, DateTimeOffset now)
    {
        var latest = index.Entries.Count == 0 ? DateTimeOffset.MinValue : index.Entries.Values.Max(e => e.ReadAt);
        return now > latest ? now : latest.AddTicks(1);
    }

    private void Enforce(ReadIndex index, string keep)
    {
        var sizes = index.Entries.Values.Select(e => e.Blob).Distinct(StringComparer.Ordinal)
            .ToDictionary(blob => blob, blob => File.Exists(BlobPath(blob)) ? new FileInfo(BlobPath(blob)).Length : 0, StringComparer.Ordinal);
        var total = sizes.Values.Sum();
        while (index.Entries.Count > MaximumKeys || total > MaximumBytes)
        {
            var others = index.Entries.Where(pair => pair.Key != keep).ToArray();
            if (others.Length == 0)
                break;
            var oldest = others.MinBy(pair => pair.Value.ReadAt);
            index.Entries.Remove(oldest.Key);
            if (!index.Entries.Values.Any(e => e.Blob == oldest.Value.Blob))
                total -= sizes[oldest.Value.Blob];
        }
    }

    private void DeleteUnreferenced(ReadIndex index)
    {
        if (!Directory.Exists(BlobDirectory))
            return;
        var referenced = index.Entries.Values.Select(e => BlobPath(e.Blob)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(BlobDirectory))
        {
            if (referenced.Contains(file))
                continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The blob text, or null when the blob is missing or damaged. A blob that cannot be opened throws, so it is not mistaken for a lost one.</summary>
    private string? ReadBlob(string blob)
    {
        var path = BlobPath(blob);
        if (!File.Exists(path))
            return null;
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != blob)
            return null; // A damaged blob is never shown as what the user read.
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return null; }
    }

    private void Save(ReadIndex index) => AtomicFile.Write(IndexPath, Json.Encode(index));

    /// <summary>
    /// The stored index. A missing, invalid or oversized index is treated as empty and replaced by the next write. An index of another
    /// schema version is returned as <see cref="ReadIndex.Foreign"/>. A file that cannot be read throws.
    /// </summary>
    private ReadIndex Load()
    {
        if (!File.Exists(IndexPath))
            return new();
        if (new FileInfo(IndexPath).Length > MaximumIndexBytes)
            return new();
        var text = File.ReadAllText(IndexPath);
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("schemaVersion", out var version)
                && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number != 1)
                return new() { Foreign = true };
            var index = JsonSerializer.Deserialize<ReadIndex>(text, Json.Options);
            if (index is not { SchemaVersion: 1, Entries: not null })
                return new();
            // Entries come from a file a person could edit: a name that is not a plain digest must never become a path.
            var valid = index.Entries.Where(pair => pair.Value is { Blob: not null, Revision: not null } && BlobName().IsMatch(pair.Value.Blob));
            return new()
            {
                Entries = new(valid, StringComparer.Ordinal)
            };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException) { return new(); }
    }

    [GeneratedRegex("^[a-f0-9]{64}$")] private static partial Regex BlobName();
}
