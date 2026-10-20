using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Protocol;
using WorkLog.Journaling;
using WorkLog.Reporting;

namespace WorkLog.Mcp;

internal static class UnknownFieldValidation
{
    public static string? Find(CallToolRequestParams? request)
    {
        var (argumentName, argumentType) = request?.Name switch
        {
            "log_work" => ("entry", typeof(LogRequest)),
            "query_work" => ("period", typeof(QueryRequest)),
            _ => (null, null)
        };
        if (argumentName is null || request?.Arguments is null)
            return null;

        foreach (var argument in request.Arguments)
        {
            if (argument.Key != argumentName)
                return argument.Key;
            var unknown = Find(argument.Value, argumentType!, argumentName);
            if (unknown is not null)
                return unknown;
        }
        return null;
    }

    private static string? Find(JsonElement value, Type type, string path)
    {
        if (value.ValueKind == JsonValueKind.Array && type.IsArray)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var unknown = Find(item, type.GetElementType()!, $"{path}[{index++}]");
                if (unknown is not null)
                    return unknown;
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            var metadata = McpJson.Options.GetTypeInfo(type);
            if (metadata.Kind != JsonTypeInfoKind.Object)
                return null;
            var comparison = McpJson.Options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var property in value.EnumerateObject())
            {
                var member = metadata.Properties.FirstOrDefault(member => member.Name.Equals(property.Name, comparison));
                var memberPath = path + "." + property.Name;
                if (member is null)
                    return memberPath;
                var unknown = Find(property.Value, member.PropertyType, memberPath);
                if (unknown is not null)
                    return unknown;
            }
        }
        return null;
    }
}
