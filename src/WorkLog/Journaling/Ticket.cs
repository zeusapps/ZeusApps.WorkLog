using System.ComponentModel;

namespace WorkLog.Journaling;

[Description("A recorded issue reference. WorkLog stores the reference without connecting to the issue service.")]
public sealed record Ticket(
    [property: Description("Issue provider: jira or azure-devops.")] string Provider,
    [property: Description("Provider namespace that qualifies the ID, such as a Jira site or an Azure DevOps organization/project.")] string Scope,
    [property: Description("Issue identifier within the provider scope, such as DEP-1232 or an Azure DevOps numeric ID.")] string Id,
    [property: Description("Optional absolute HTTP(S) URL supplied for this issue.")] string? Url = null);
