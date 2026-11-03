# Spec Delta

## MODIFIED Requirements

### Requirement: Session-driven OpenSpec logging instructions
When managed instructions are enabled, both integrations SHALL instruct the agent to log started after successful change creation, including openspec-propose; log in_progress after meaningful actual work; and log done when the session establishes reporting completion independently of archive. Instructions SHALL allow feature switching and multiple features in a session, avoid activity on idle days, preserve a retry key for retries, and avoid guessing unknown ticket providers or completion. Instructions SHALL direct the agent to include a summary with each observation consisting of one short sentence written for the user's daily stand-up, without repeating the status or including secrets or personal data. The summary SHALL follow the stand-up content rule: it states what changed in the feature, story or bug fix (behavior, capability or a scope decision) and any blocker that needs someone else; for in_progress it states what was achieved and what remains in those terms when known, and for done what was completed. The rule SHALL treat development workflow mechanics as plain implementation of the item and SHALL direct the agent not to mention them, naming at least proposals and specs, delegation, reviewers and review rounds, test and task counts, commits, hashes, branches, merges, archiving and syncing, builds and installation. Both integrations SHALL receive identical summary guidance. They SHALL explain that logging is agent-driven rather than a guaranteed lifecycle hook and that failures must be reported. Disabling managed instructions SHALL suppress WorkLog-owned instructions without modifying rules maintained by the application or user.

#### Scenario: Delayed archive
- **WHEN** a session establishes completion but archives later
- **THEN** enabled managed instructions direct done to be recorded at completion rather than archive

#### Scenario: Required migration pending
- **WHEN** the session identifies a migration as required before reporting completion
- **THEN** enabled managed instructions direct in_progress with optional pending-work summary rather than automatic done

#### Scenario: Application-owned logging policy
- **WHEN** managed instructions are disabled and the application already contains logging rules
- **THEN** init preserves those rules without replacing or duplicating them

#### Scenario: Summary guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same one-sentence summary guidance with the stand-up content rule

#### Scenario: Workflow mechanics excluded
- **WHEN** init installs managed instructions
- **THEN** the summary guidance directs the agent to describe the feature, story or bug fix and names review rounds, test and task counts, commits, hashes, merges, archiving and installation as details to leave out

### Requirement: Summary guidance in the logging tool
The `log_work` tool's summary input SHALL carry a description stating the same one-sentence guidance and stand-up content rule as the managed instructions, including the named workflow mechanics to leave out, so that it is visible to clients regardless of the managed instruction mode. The summary SHALL remain optional, and its content and length SHALL NOT be validated beyond existing limits.

#### Scenario: Instructions disabled
- **WHEN** a client with managed instructions disabled lists the WorkLog tools
- **THEN** the log_work summary input describes the expected one-sentence stand-up summary and the workflow mechanics to leave out

#### Scenario: Summary omitted
- **WHEN** an agent logs an observation without a summary
- **THEN** the observation is saved as before

#### Scenario: Process-heavy summary accepted
- **WHEN** an agent logs a summary that lists review rounds and a commit hash
- **THEN** the observation is saved unchanged, because summary content is not validated
