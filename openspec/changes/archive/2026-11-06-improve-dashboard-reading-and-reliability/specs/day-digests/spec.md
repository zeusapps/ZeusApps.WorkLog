# Spec Delta

## ADDED Requirements

### Requirement: Nonblocking digest failure visibility
Failures of optional digest computation or retrieval SHALL leave observation logging, journal querying, and the dashboard's recorded-work views usable. An actual failure SHALL be distinguishable from the normal absence of a stored digest. MCP-side computation failures SHALL produce a bounded sanitized diagnostic through the server's diagnostic channel while stdout remains valid MCP protocol traffic and existing tool input/output contracts remain unchanged. Dashboard retrieval failures SHALL identify that daily summaries are unavailable while retaining recorded observations and their fallback summaries. Repeated identical failures SHALL be coalesced, and recovery SHALL clear the unavailable indication. Diagnostics SHALL NOT include observation text, credentials, or authorization secrets. This behavior SHALL be shared by the Claude Code and Codex integrations.

#### Scenario: Digest computation fails while logging
- **WHEN** optional digest computation or storage access fails after an observation is saved
- **THEN** logging succeeds with the observation intact, no invalid digest request is returned, and a bounded diagnostic identifies the optional failure without corrupting protocol output

#### Scenario: Daily cannot retrieve stored digests
- **WHEN** stored digests cannot be read for the Daily view
- **THEN** the view identifies daily summaries as unavailable and continues to show recorded observations and their normal fallback summaries

#### Scenario: No digest has been stored
- **WHEN** a readable or absent digest store contains no digest for a displayed item
- **THEN** the view uses its ordinary fallback summary without reporting a storage failure

#### Scenario: Repeated failure and recovery
- **WHEN** an unchanged digest failure occurs on successive requests and storage later becomes usable
- **THEN** diagnostics and UI notices remain bounded during the failure and the unavailable indication clears after a successful retrieval

#### Scenario: Same behavior in both agent integrations
- **WHEN** equivalent logging requests through Claude Code and Codex encounter the same optional digest failure
- **THEN** both receive the same successful logging contract and equivalent diagnostic behavior without changed observations or invalid stdout traffic
