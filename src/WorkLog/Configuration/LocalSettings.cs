using System.Text.Json.Nodes;
using WorkLog.Infrastructure;

namespace WorkLog.Configuration;

public sealed class LocalSettings
{
    private JsonObject raw = new();
    private JsonObject baseline = new();
    public string DataDirectory
    {
        get;
    }
    public string ConfigPath => Path.Combine(DataDirectory, "config.json");
    public WorkLogConfiguration Configuration { get; private set; } = new();

    public LocalSettings(string? dataDirectory = null)
    {
        DataDirectory = Path.GetFullPath(dataDirectory
            ?? Environment.GetEnvironmentVariable("WORKLOG_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeusApps.WorkLog"));

        Load(ReadRaw());
    }

    private JsonObject ReadRaw()
    {
        if (!File.Exists(ConfigPath))
            return Project(new WorkLogConfiguration());
        using var stream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd()) as JsonObject ?? throw new ArgumentException("Expected a configuration object.");
    }
    private static JsonObject Project(WorkLogConfiguration value) => JsonNode.Parse(Json.Encode(value))!.AsObject();
    private void Load(JsonObject document)
    {
        raw = document;
        Configuration = Json.Decode<WorkLogConfiguration>(document.ToJsonString());
        // Every typed projection includes legacy defaults; keep raw JSON intact for merging.
        Configuration.Normalize();
        baseline = Project(Configuration);
    }

    internal IDisposable AcquireWrite() => new SettingsLease(DataDirectory);
    internal LocalSettings FreshCopy()
    {
        var latest = new LocalSettings(DataDirectory);
        var merged = Merge(baseline, Project(Configuration), latest.baseline, latest.raw, "config");
        latest.Load(merged!.AsObject());
        return latest;
    }
    internal LocalSettings Copy()
    {
        var result = new LocalSettings(DataDirectory);
        result.Load(Merge(baseline, Project(Configuration), baseline, raw, "config")!.AsObject());
        return result;
    }
    internal string SerializePreserving() => Merge(baseline, Project(Configuration), baseline, raw, "config")!.ToJsonString(Json.Options);
    internal void Adopt(LocalSettings planned) => Load(JsonNode.Parse(planned.SerializePreserving())!.AsObject());

    public void Save()
    {
        using var lease = AcquireWrite();
        var latest = FreshCopy();
        AtomicFile.Write(ConfigPath, latest.SerializePreserving());
        Adopt(latest);
    }

    // Patch only known typed leaves onto the latest raw JSON. Unknown members never enter the patch.
    private static JsonNode? Merge(JsonNode? before, JsonNode? desired, JsonNode? latest, JsonNode? document, string path)
    {
        if (JsonNode.DeepEquals(before, desired))
            return document?.DeepClone();
        if (desired is JsonObject wanted && (before is null || before is JsonObject) && (latest is null || latest is JsonObject))
        {
            var result = document?.DeepClone() as JsonObject ?? new JsonObject();
            var original = before as JsonObject;
            var current = latest as JsonObject;
            foreach (var key in wanted.Select(pair => pair.Key).Union(original?.Select(pair => pair.Key) ?? []))
            {
                var previous = original?[key];
                var next = wanted[key];
                if (JsonNode.DeepEquals(previous, next))
                    continue;
                if (!wanted.ContainsKey(key))
                {
                    if (!JsonNode.DeepEquals(current?[key], previous) && current?.ContainsKey(key) == true)
                        throw new IOException("Concurrent settings conflict at " + path + "." + key + ". Reload and retry.");
                    result.Remove(key);
                }
                else
                    result[key] = Merge(previous, next, current?[key], result[key], path + "." + key);
            }
            return result;
        }
        if (!JsonNode.DeepEquals(latest, before) && !JsonNode.DeepEquals(latest, desired))
            throw new IOException("Concurrent settings conflict at " + path + ". Reload and retry.");
        return desired?.DeepClone();
    }
}
