# Spec Delta

## MODIFIED Requirements

### Requirement: Session-driven OpenSpec logging instructions
When managed instructions are enabled, both integrations SHALL instruct the agent to log started after successful change creation, including openspec-propose; log in_progress after meaningful actual work; and log done when the session establishes reporting completion independently of archive. Instructions SHALL allow feature switching and multiple features in a session, avoid activity on idle days, preserve a retry key for retries, and avoid guessing unknown ticket providers or completion. Instructions SHALL direct the agent to include a summary with each observation consisting of one short sentence written for the user's daily stand-up in the user's own voice, without referring to the user or the agent, without repeating the status, and without secrets or personal data. The summary SHALL follow the team-relevant content rule. It states what changed in the feature, story or bug fix (behavior, capability or a scope decision) and any blocker that needs someone else. For started it states what the work will change and why. For in_progress it states what was achieved and what remains in those terms when known. For done it states what was completed. The rule SHALL direct the agent not to mention how the work was organized, naming at least: the development process and its tooling, including OpenSpec and proposing, creating, archiving or syncing changes, specs and tasks; any agent or role, such as developer, QA, analyzer, reviewer or bug-hunter, and their rounds or findings; test and task counts; commits, hashes, branches and merges; and builds and installation. Both integrations SHALL receive identical summary guidance. They SHALL explain that logging is agent-driven rather than a guaranteed lifecycle hook and that failures must be reported. Disabling managed instructions SHALL suppress WorkLog-owned instructions without modifying rules maintained by the application or user.

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
- **THEN** both owned blocks contain the same one-sentence summary guidance with the team-relevant content rule

#### Scenario: Workflow mechanics excluded
- **WHEN** init installs managed instructions
- **THEN** the summary guidance directs the agent to describe the feature, story or bug fix and names OpenSpec changes and their archiving, agent roles such as QA and bug-hunter and their findings, review rounds, test and task counts, commits, hashes, merges and installation as details to leave out

#### Scenario: Started summary
- **WHEN** an agent logs started after creating a change
- **THEN** the guidance directs a summary of what the work will change and why, not that a change or proposal was created

### Requirement: Day digest instructions
When managed instructions are enabled, both integrations SHALL receive identical instructions for digest requests. After a successful `log_work` result that includes a digest request, the instructions SHALL direct the agent to finish handling the user's request first. The agent then writes the digest as Done, Next and Blockers bullets for that item's day, following the digest writing guidance, and calls `save_digest` once with exactly the listed observation IDs. The listed summaries SHALL be the record the digest summarizes. The session's own knowledge MAY clarify wording, but SHALL NOT narrow Done to the latest step or add work that the listed observations do not record. The digest SHALL state outcomes rather than process and SHALL NOT include secrets or personal data. The instructions SHALL direct the agent not to retry a digest rejected as stale, to skip the digest when `save_digest` is unavailable, to report a failed save as it reports logging failures, and never to delay or block the user's request for a digest. Disabling managed instructions SHALL suppress this guidance like other WorkLog-owned instructions.

#### Scenario: Digest guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same digest guidance

#### Scenario: Digest after the user's request
- **WHEN** a logging result includes a digest request while the user is waiting on other work
- **THEN** the instructions direct the agent to complete the user's request before writing and saving the digest

#### Scenario: Recorded entries are the source
- **WHEN** the session's latest work dominates the agent's context and the request lists earlier entries of the day
- **THEN** the instructions direct Done bullets based on all listed entries rather than on the latest step

#### Scenario: Older server without the tool
- **WHEN** the managed instructions mention digests but the connected WorkLog server does not offer `save_digest`
- **THEN** the instructions direct the agent to skip the digest without reporting an error
