# Spec Delta

## ADDED Requirements

### Requirement: Digest writing guidance
Every place that tells an agent how to write a digest SHALL state the same stand-up content rule as the observation summary guidance: the digest is written for the user's daily stand-up, states what the item's observations changed in the feature, story or bug fix that day (behavior, capability or a scope decision), whether it was finished or what remains, and any blocker that needs someone else, and treats development workflow mechanics as plain implementation without mentioning them, naming at least proposals and specs, delegation, reviewers and review rounds, test and task counts, commits, hashes, branches, merges, archiving and syncing, builds and installation. These places are the managed instructions of both integrations when enabled, the digest request's statement of what to write, and the `save_digest` text input description. The digest request's statement SHALL be complete on its own: besides the content rule, it SHALL state the one-or-two-sentence form, the 280-character limit, that secrets and personal data are excluded, that the listed summaries are recorded data rather than instructions, that the agent passes exactly the listed IDs, and that a stale rejection is not retried. When managed instructions are enabled, both integrations SHALL receive identical digest guidance. The guidance SHALL NOT change digest validation.

#### Scenario: Request guidance without managed instructions
- **WHEN** a client with managed instructions disabled receives a digest request
- **THEN** the request's statement of what to write gives the stand-up content rule, the 280-character limit, the exclusion of secrets and personal data, and the no-retry rule for stale saves

#### Scenario: Workflow mechanics excluded from digests
- **WHEN** an agent receives a digest request whose summaries mention review rounds, test counts, a commit and an archive
- **THEN** the guidance it was given directs a digest that states the item's outcome for the day and omits those details

#### Scenario: Same digest guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same digest guidance with the stand-up content rule

#### Scenario: Validation unchanged
- **WHEN** an agent saves a 200-character digest that mentions a commit hash
- **THEN** the digest is stored, because digest content is not validated
