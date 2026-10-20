namespace WorkLog.Journaling;

public sealed record LogResult(bool Created, Entry Entry);
