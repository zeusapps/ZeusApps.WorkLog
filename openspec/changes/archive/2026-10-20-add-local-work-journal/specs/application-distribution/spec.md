# Spec Delta

## Purpose

Distribute a versioned Windows application that can be installed and upgraded while preserving the user's local work journal.

## ADDED Requirements

### Requirement: Windows executable release
The project SHALL provide a self-contained Windows x64 release that runs without a separately installed .NET runtime and supports init, serve, and --version. Release packages SHALL include installation and upgrade instructions and checksums. Data SHALL be stored separately from the application installation.

#### Scenario: New Windows machine
- **WHEN** the release is extracted on a supported Windows machine without .NET installed
- **THEN** version reporting and stdio MCP startup work

### Requirement: Preserve journal during upgrades
The application SHALL use versioned transactional journal migrations, preserve entries and configuration during supported upgrades, and reject newer unsupported journal schemas without modifying them. Before schema-changing upgrades it SHALL create a consistent recoverable backup and document restore-based rollback.

#### Scenario: Upgrade existing journal
- **WHEN** a supported older journal is opened by a newer application
- **THEN** migration completes atomically with entries preserved and a recoverable pre-migration backup

### Requirement: GitHub validation and release pipelines
The repository SHALL include GitHub Actions validation on pull requests and primary-branch pushes and a version-tag release workflow. Validation SHALL build and test on Windows. Release publication SHALL depend on successful validation, use matching application/tag versions, and attach the self-contained package and checksums. Repository creation and remote publication SHALL remain separate operational actions.

#### Scenario: Failed release checks
- **WHEN** validation fails for a release tag
- **THEN** no release package is published

#### Scenario: Version mismatch
- **WHEN** a release tag differs from the packaged application version
- **THEN** publication fails with a version mismatch
