using WorkLog.Journaling;

namespace WorkLog.Reporting;

public sealed record Report(
    int SchemaVersion,
    DateTimeOffset From,
    DateTimeOffset To,
    string Timezone,
    Entry[] Entries,
    DailyGroup[] Days);
