# Spec Delta

## MODIFIED Requirements

### Requirement: Private repository initialization
The application SHALL provide `worklog init` with repository and client selection for Codex, Claude Code, or both. Omitted repository SHALL mean the current repository and omitted clients SHALL mean both. In a versioned installation it SHALL register the stable launcher as a stdio MCP server even when init executes inside a retained payload; supported source invocations SHALL retain their development registration behavior. When managed instructions are enabled for that repository and client, initialization SHALL install logging instructions using supported private settings. It SHALL NOT edit shared CLAUDE.md, AGENTS.md, .mcp.json, OpenSpec configuration, or generated OpenSpec skills. Created repository-local files SHALL be excluded through local Git exclusions. Already tracked destinations that initialization would modify SHALL cause a clear conflict instead of being modified. Installation resolution SHALL validate the relationship between the payload and stable launcher rather than trusting an arbitrary environment-supplied executable path.

#### Scenario: Claude local setup
- **WHEN** Claude Code integration is initialized with managed instructions enabled
- **THEN** MCP registration uses private local scope and instructions use a managed block in CLAUDE.local.md while existing instructions remain available

#### Scenario: Codex local setup
- **WHEN** Codex integration is initialized with managed instructions enabled
- **THEN** its private repository configuration adds the MCP server and additive instructions without replacing base instructions or shared AGENTS.md

#### Scenario: Existing application supplies instructions
- **WHEN** either client is initialized with `--no-instructions`
- **THEN** the private MCP registration is configured without adding WorkLog instructions or instruction imports, and unrelated instructions remain unchanged

#### Scenario: Unmodified instruction target is tracked
- **WHEN** Claude initialization has instructions disabled and a tracked CLAUDE.local.md has no owned block to remove
- **THEN** initialization leaves that file unchanged and does not reject it as a write destination

#### Scenario: Init from a retained payload
- **WHEN** init runs from a valid installed version directory before or after another version becomes selected
- **THEN** the owned registration and integration ownership metadata use the stable launcher path with the original repository and data-directory arguments

#### Scenario: Refresh legacy owned registration
- **WHEN** the user reruns init during the documented one-time migration
- **THEN** initialization reconciles only registrations proven owned, preserves unrelated registrations and instruction preferences, and explains normal trust/restart steps

### Requirement: Update hint handling instructions
When managed instructions are enabled, both private client integrations SHALL instruct the assistant to surface an update hint from a successful WorkLog tool result at most once per available version within the assistant session. Instructions SHALL identify the explicit stable-entry-point update command and distinguish activation for new processes from adoption by the current conversation. Compatible payload activation SHALL NOT require closing existing conversations; a normal reconnect or restart is required only for those conversations to adopt the new payload. Legacy migration, launcher changes, and incompatible data changes SHALL be described as maintenance cases. Instructions SHALL NOT treat a hint or an ordinary logging/reporting request as permission to install, reconfigure clients, or terminate processes. They SHALL explain that notice presentation depends on the assistant, rather than guaranteeing an MCP client notification or dedicated UI. Rerunning init SHALL refresh these owned instructions using the existing preservation rules when enabled, and SHALL preserve the absence of owned logging and update-hint guidance when disabled.

#### Scenario: First update hint
- **WHEN** the assistant receives a hint for an available version it has not announced during the session and managed instructions are enabled
- **THEN** instructions direct it to briefly announce the version and explicit update command, explain adoption at normal reconnect/restart, and avoid requiring session closure for compatible activation

#### Scenario: Repeated hint
- **WHEN** later tool results contain the same available version during that session and managed instructions are enabled
- **THEN** instructions direct the assistant to avoid repeating the announcement

#### Scenario: User continues journal work
- **WHEN** an update hint appears during a logging or reporting request and managed instructions are enabled
- **THEN** instructions direct the assistant to finish that request without starting installation or stopping processes based only on the hint

#### Scenario: Refresh private integration
- **WHEN** init is rerun after upgrading with managed instructions enabled
- **THEN** owned blocks gain the activation/adoption guidance while unrelated private and shared instructions remain preserved

#### Scenario: Refresh opted-out integration
- **WHEN** init is rerun after upgrading with managed instructions disabled
- **THEN** owned logging and update-hint instructions remain absent while registration uses the stable launcher
