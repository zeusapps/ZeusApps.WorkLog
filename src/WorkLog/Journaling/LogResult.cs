using System.ComponentModel;
using System.Text.Json.Serialization;
using WorkLog.Updates;

namespace WorkLog.Journaling;

[Description("Result of logging an observation, including whether a new entry was saved or an existing entry was returned.")]
public sealed record LogResult(
    [property: Description("True when a new observation was saved. False when a retry or duplicate initial start returned an existing entry.")] bool Created,
    [property: Description("The saved or previously existing observation. A replay retains its original ID and timestamps.")] Entry Entry)
{
    [Description("Optional application update notice, separate from the saved observation. Announce once per version per assistant session.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UpdateHint? UpdateHint { get; init; }
}
