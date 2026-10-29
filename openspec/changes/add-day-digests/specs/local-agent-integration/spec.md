# Spec Delta

## ADDED Requirements

### Requirement: Day digest instructions
When managed instructions are enabled, both integrations SHALL receive identical instructions for digest requests. After a successful `log_work` result that includes a digest request, the instructions SHALL direct the agent to finish handling the user's request first, then write one or two sentences stating what the listed observations achieved that day for that item, using the listed summaries and the session's own knowledge, and call `save_digest` once with exactly the listed observation IDs. The digest SHALL state outcomes rather than process and SHALL NOT include secrets or personal data. The instructions SHALL direct the agent not to retry a digest rejected as stale, to skip the digest when `save_digest` is unavailable, to report a failed save as it reports logging failures, and never to delay or block the user's request for a digest. Disabling managed instructions SHALL suppress this guidance like other WorkLog-owned instructions.

#### Scenario: Digest guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same digest guidance

#### Scenario: Digest after the user's request
- **WHEN** a logging result includes a digest request while the user is waiting on other work
- **THEN** the instructions direct the agent to complete the user's request before writing and saving the digest

#### Scenario: Older server without the tool
- **WHEN** the managed instructions mention digests but the connected WorkLog server does not offer `save_digest`
- **THEN** the instructions direct the agent to skip the digest without reporting an error
