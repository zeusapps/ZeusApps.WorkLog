using System.ComponentModel;
using WorkLog.Journaling;

namespace WorkLog.Reporting;

[Description("Recorded observations for one work item on one calendar date in the report timezone.")]
public sealed record DailyGroup(
    [property: Description("Calendar date in YYYY-MM-DD form, derived from occurredAt in the report timezone.")] string Date,
    [property: Description("Grouping identity combining the repository with a change or sorted distinct qualified ticket set, selected by period.groupBy. Ticket mode retains change identity without qualified tickets. Branch names do not define work identity; groups can span pages.")] string WorkIdentity,
    [property: Description("Full observations in grouped and legacy views; empty in compact view. Multiple statuses are retained; do not count this view again.")] Entry[] Observations,
    [property: Description("IDs of this page's observations in compact view, referencing report.entries. Groups can continue on subsequent pages.")] string[] EntryIds);
