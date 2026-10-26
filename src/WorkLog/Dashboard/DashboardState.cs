using System.Text.Json.Nodes;
using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

internal sealed class DashboardState(string dataDirectory)
{
    private readonly string path = Path.Combine(dataDirectory, "dashboard-state.json");
    private readonly object gate = new();

    public JsonObject Read()
    {
        lock (gate)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length <= 65_536 && JsonNode.Parse(File.ReadAllText(path)) is JsonObject state
                    && state["schemaVersion"]?.GetValue<int>() == 1) return state;
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or IOException or InvalidOperationException or FormatException) { }
            return new JsonObject { ["schemaVersion"] = 1 };
        }
    }

    public void Write(JsonObject state)
    {
        state["schemaVersion"] = 1;
        var text = state.ToJsonString();
        if (System.Text.Encoding.UTF8.GetByteCount(text) > 65_536) throw new ArgumentException("Dashboard preferences exceed 64 KiB.");
        lock (gate)
        {
            AtomicFile.Write(path, state.ToJsonString());
        }
    }
}
