using System.Text.Json;

namespace WorkLog.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new ArgumentException("Expected a JSON object.");
}
