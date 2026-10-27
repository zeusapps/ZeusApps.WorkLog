# Spec Delta

## ADDED Requirements

### Requirement: External ticket and repository links
Wherever the dashboard shows a recorded ticket, it SHALL offer a link to the ticket when one can be determined, and an action that copies the ticket ID. A ticket's recorded URL SHALL be used when present. Otherwise the dashboard SHALL build a link only for recognized forms: a Jira ticket whose scope is a site host name, and an Azure DevOps ticket whose scope is an organization or an organization and project. Unrecognized forms and unresolved extracted ticket IDs SHALL be shown without a link. Repository identities on recognized Git hosts (GitHub, GitLab, Bitbucket, and Azure DevOps) SHALL link to the repository's web page, and observations with a recorded branch SHALL link to that branch on the same host. Local-only repository identities SHALL be shown without a link. Links SHALL use HTTPS, SHALL open only after explicit user navigation, and SHALL NOT trigger automatic requests to the external service.

#### Scenario: Ticket with recorded URL
- **WHEN** an observation's ticket includes a URL
- **THEN** the ticket ID links to that URL

#### Scenario: Jira ticket without URL
- **WHEN** a ticket has provider jira, scope `company.atlassian.net`, and ID `DEP-12` with no URL
- **THEN** it links to `https://company.atlassian.net/browse/DEP-12`

#### Scenario: Azure DevOps ticket without URL
- **WHEN** a ticket has provider azure-devops, scope `contoso/Payments`, and ID `4211` with no URL
- **THEN** it links to that organization's and project's work item 4211 on `dev.azure.com`

#### Scenario: Unrecognized scope
- **WHEN** a ticket's scope matches no recognized form and it has no URL
- **THEN** the ticket ID is shown with a copy action and no link

#### Scenario: Branch on a recognized host
- **WHEN** an observation in repository `github.com/acme/api` recorded branch `feature/DEP-12`
- **THEN** the drill-down links the branch to that repository's branch page

#### Scenario: Local repository
- **WHEN** a repository identity starts with `local:`
- **THEN** no repository or branch link is shown

### Requirement: History ticket filter
History SHALL accept an optional ticket filter. A bare ticket ID SHALL match a stored ticket with that ID or an unresolved extracted ticket ID; a `provider:scope:id` value SHALL match only a stored ticket with that provider, scope, and ID. Surrounding whitespace SHALL be trimmed. Matching SHALL be identical to the `query_work` tool's ticket filter. The filter SHALL select work items that have at least one matching observation and SHALL keep each selected item's full lifecycle context, spans, and insights. The filter SHALL combine with the repository selection, date range, and grouping, and SHALL be shown clearly while active.

#### Scenario: Follow one ticket
- **WHEN** the user filters History by `DEP-12`
- **THEN** only work items with an observation referencing DEP-12 appear, with their full recorded spans

#### Scenario: Qualified filter excludes another site
- **WHEN** the user filters by `jira:company.atlassian.net:DEP-12` and another observation references DEP-12 on a different Jira site
- **THEN** only the work referencing the specified site appears

### Requirement: Copy a work item as Markdown
The drill-down SHALL offer an action that copies the work item as Markdown: title, tickets with links when available, repository, first and latest observation dates, latest recorded status, associated changes, and each observation in the loaded scope with date, status, and summary. When the observations span more pages than are loaded, the action SHALL load them up to a bounded maximum and SHALL state in the copied text when that maximum truncated the list. The copied text SHALL describe recorded facts only.

#### Scenario: Copy a ticket's history
- **WHEN** the user opens a ticket's drill-down and selects Copy as Markdown
- **THEN** the clipboard contains the ticket's title, link, dates, status, and observation summaries in date order

### Requirement: Quiet active changes
Ongoing work SHALL indicate active changes whose latest recorded observation is older than 14 days as quiet, showing the latest recorded date, and SHALL indicate active changes without recorded observations as having no recorded activity. Neither indication SHALL imply that the change is abandoned or complete.

#### Scenario: Change without recent activity
- **WHEN** an active change's latest observation was 20 days ago
- **THEN** its card shows that no activity was recorded in the last 14 days, with the latest recorded date
