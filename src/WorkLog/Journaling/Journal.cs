using WorkLog.Configuration;
using WorkLog.Persistence;
using WorkLog.Reporting;

namespace WorkLog.Journaling;

public sealed class Journal
{
    private readonly EntryFactory entryFactory;
    private readonly SqliteEntryStore entries;

    public Journal(LocalSettings settings, RepositoryConfig? repository = null)
    {
        entryFactory = new EntryFactory(settings.Configuration, repository);
        entries = new SqliteEntryStore(new JournalDatabase(settings.DataDirectory));
    }

    public LogResult Log(LogRequest request) => entries.Append(entryFactory.Prepare(request));

    public Report Query(QueryRequest request)
    {
        var period = ReportingPeriod.FromRequest(request);
        var observations = entries.ReadPeriod(period.From, period.To);
        return ReportBuilder.Build(request, period, observations);
    }
}
