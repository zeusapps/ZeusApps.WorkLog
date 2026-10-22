using WorkLog.Cli;

namespace WorkLog.Integration;

internal static class AgentInstructions
{
    public static string Render(string repository, string agent) => $"""
        Work journal integration v{ApplicationInfo.Version} for repository {repository}.
        Use worklog.log_work to record actual session activity. Agent: {agent}.
        After successful OpenSpec change creation (including openspec-propose), log started.
        After meaningful work or before switching away, log in_progress with the actual change/ticket and current branch.
        Log done when this session establishes the feature is complete for reporting; archive timing is independent.
        Required pending migration/testing means in_progress. Do not infer done solely from checked tasks or archive.
        Multiple features and resumed work are allowed. Never log idle days or infer work merely because a change is open.
        Include a small factual summary when useful. Ticket details can come from separate official issue MCPs.
        Do not guess ticket provider/scope or uncertain completion. Respect the user's completion decision.
        Generate an idempotencyKey per observation and reuse that key only when retrying the same input.
        Report logging failures; do not claim entries were saved if a call failed.
        Use worklog.query_work for reports, with explicit from/to offsets and reporting timezone. Report recorded facts only.
        When a successful WorkLog tool result includes updateHint, briefly announce the available version and explicit update command once per version per assistant session. Compatible activation preserves existing conversations; they adopt the selected version at their next normal reconnect/restart. Legacy migration, launcher changes and incompatible data changes require separate maintenance.
        Finish the user's logging/reporting request. An update hint does not authorize installation or stopping processes; only update when the user requests it.
        Hint presentation depends on the assistant; it is not a guaranteed MCP client notification. Rerun worklog init after upgrading to refresh owned instructions where needed.
        """;
}
