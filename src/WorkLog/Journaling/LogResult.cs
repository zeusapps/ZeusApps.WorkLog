using System.ComponentModel;

namespace WorkLog.Journaling;

[Description("Result of logging an observation, including whether a new entry was saved or an existing entry was returned.")]
public sealed record LogResult(
    [property: Description("True when a new observation was saved. False when a retry or duplicate initial start returned an existing entry.")] bool Created,
    [property: Description("The saved or previously existing observation. A replay retains its original ID and timestamps.")] Entry Entry);
