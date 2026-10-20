# Spec Delta

## ADDED Requirements

### Requirement: Update hint handling instructions
Both private client integrations SHALL instruct the assistant to surface an update hint from a successful WorkLog tool result at most once per available version within the assistant session. Instructions SHALL identify the explicit update command and client restart requirement, and SHALL NOT treat a hint or an ordinary logging/reporting request as permission to install an update or terminate processes. They SHALL explain that notice presentation depends on the assistant, rather than guaranteeing an MCP client notification or dedicated UI. Rerunning init SHALL refresh these owned instructions using the existing preservation rules.

#### Scenario: First update hint
- **WHEN** the assistant receives a hint for an available version it has not announced during the session
- **THEN** instructions direct it to briefly tell the user the version, update command, and need to restart clients

#### Scenario: Repeated hint
- **WHEN** later tool results contain the same available version during that session
- **THEN** instructions direct the assistant to avoid repeating the announcement

#### Scenario: User continues journal work
- **WHEN** an update hint appears during a logging or reporting request
- **THEN** instructions direct the assistant to complete that request without starting installation or stopping processes based only on the hint

#### Scenario: Refresh private integration
- **WHEN** init is rerun after upgrading to a version containing these instructions
- **THEN** the owned blocks gain update-hint guidance while unrelated private and shared instructions remain preserved
