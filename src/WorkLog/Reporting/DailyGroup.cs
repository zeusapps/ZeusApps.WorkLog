using WorkLog.Journaling;

namespace WorkLog.Reporting;

public sealed record DailyGroup(string Date, string WorkIdentity, Entry[] Observations);
