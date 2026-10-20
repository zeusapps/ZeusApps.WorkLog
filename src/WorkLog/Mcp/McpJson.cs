using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WorkLog.Mcp;

internal static class McpJson
{
    // SDK-generated schemas require nullable record properties. Keep explicit nulls in tool results.
    // These options are deliberately separate from journal serialization and retry fingerprints.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
}
