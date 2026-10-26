# Spec Delta

## MODIFIED Requirements

### Requirement: Windows executable release
The project SHALL provide a self-contained Windows x64 release that runs without a separately installed .NET runtime and supports init, serve, update, dashboard, and --version. The dashboard's web assets SHALL be embedded in the payload executable so the release file allowlist is unchanged and no Node.js or other web runtime is required at runtime. Release packages SHALL include installation and upgrade instructions and checksums. Data SHALL be stored separately from the application installation.

#### Scenario: New Windows machine
- **WHEN** the release is extracted on a supported Windows machine without .NET installed
- **THEN** version reporting and stdio MCP startup work

#### Scenario: Discover update commands
- **WHEN** the user requests application help
- **THEN** help describes checking for and explicitly installing an update

#### Scenario: Dashboard without a web runtime
- **WHEN** the release is extracted on a supported Windows machine without Node.js or internet access and the user launches the dashboard
- **THEN** the dashboard interface loads from assets embedded in the payload and the package contains no additional release files
