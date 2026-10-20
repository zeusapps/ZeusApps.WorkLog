# Spec Delta

## Purpose

Initialize private repository integrations that let Codex and Claude Code record actual session work through the same local MCP tools.

## ADDED Requirements

### Requirement: Private repository initialization
The application SHALL provide `worklog init` with repository and client selection for Codex, Claude Code, or both. Omitted repository SHALL mean the current repository and omitted clients SHALL mean both. It SHALL register the installed executable as a stdio MCP server and install logging instructions using supported private settings. It SHALL NOT edit shared CLAUDE.md, AGENTS.md, .mcp.json, OpenSpec configuration, or generated OpenSpec skills. Created repository-local files SHALL be excluded through local Git exclusions. Already tracked destinations SHALL cause a clear conflict instead of being modified.

#### Scenario: Claude local setup
- **WHEN** Claude Code integration is initialized
- **THEN** MCP registration uses private local scope and instructions use a managed block in CLAUDE.local.md while existing instructions remain available

#### Scenario: Codex local setup
- **WHEN** Codex integration is initialized
- **THEN** its private repository configuration adds the MCP server and additive instructions without replacing base instructions or shared AGENTS.md

### Requirement: Repeatable safe initialization
Initialization SHALL preserve unrelated settings and content, update only owned blocks or keys, avoid duplicate registrations, and describe applied changes and restart or trust requirements. Unsupported client configuration and malformed files SHALL produce actionable errors without overwriting them or claiming complete success. A dry-run option SHALL describe planned changes without writes.

#### Scenario: Initialize twice
- **WHEN** init runs twice for the same repository and clients
- **THEN** one effective server registration and instruction block per client remain

#### Scenario: Existing configuration conflict
- **WHEN** an unrelated server already occupies the intended registration name
- **THEN** initialization reports the conflict and preserves that registration

### Requirement: Session-driven OpenSpec logging instructions
Both integrations SHALL instruct the agent to log started after successful change creation, including openspec-propose; log in_progress after meaningful actual work; and log done when the session establishes reporting completion independently of archive. Instructions SHALL allow feature switching and multiple features in a session, avoid activity on idle days, preserve a retry key for retries, and avoid guessing unknown ticket providers or completion. They SHALL explain that logging is agent-driven rather than a guaranteed lifecycle hook and that failures must be reported.

#### Scenario: Delayed archive
- **WHEN** a session establishes completion but archives later
- **THEN** instructions direct done to be recorded at completion rather than archive

#### Scenario: Required migration pending
- **WHEN** the session identifies a migration as required before reporting completion
- **THEN** instructions direct in_progress with optional pending-work summary rather than automatic done
