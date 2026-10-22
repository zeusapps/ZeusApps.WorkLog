# Spec Delta

## MODIFIED Requirements

### Requirement: Recoverable Windows update completion
Replacement SHALL occur only after the updater's original process exits and all other processes using the target installation have stopped. The updater SHALL NOT terminate agent sessions or active MCP servers automatically. After acquiring the update lease and verifying the payload, the helper SHALL allow a bounded readiness interval of at most 30 seconds for transient target-process use or target-file sharing conflicts to clear before application-file mutation. A finite status read that overlaps readiness but exits and permits an idle window within this interval SHALL NOT alone cause update failure. Process and exclusive-file readiness checks SHALL both participate in this interval. Persistent use, deadline expiry, permanent validation or access failures, or another update in progress SHALL result in an actionable refusal without pre-readiness application-file replacement. Cancellation before mutation SHALL preserve installed files. The helper SHALL NOT broadly retry permanent failures or restart replacement after mutation begins. It SHALL distinguish staged, installed, and failed states, expose the eventual helper outcome through `worklog update --status`, and retain recoverable prior application files until the new executable passes a version check. Status reads SHALL remain local and offline. Failed replacement or verification SHALL restore prior application files when possible and otherwise retain recovery files with instructions. Staged guidance SHALL direct users to stop affected clients before updating, allow the helper to finish before inspecting status with the same data directory, and restart clients only after confirmed installation. Successful installation SHALL explain client restart and instruction-refresh steps. Existing package validation, transactional journal migrations, and restore-based rollback rules SHALL continue to apply.

#### Scenario: Active MCP process
- **WHEN** another process persistently uses the target installation
- **THEN** replacement is refused immediately or by the readiness deadline with instructions to stop affected clients and retry, without terminating those processes or changing installed files

#### Scenario: Helper has not finished
- **WHEN** the command has staged a verified update but the replacement helper has not completed
- **THEN** the command reports staging rather than successful installation and directs the user to allow completion before inspecting the eventual outcome without encouraging tight polling

#### Scenario: Replacement succeeds
- **WHEN** the idle installation is replaced and the new executable reports the selected version
- **THEN** the outcome reports installed and advises restarting MCP clients and rerunning init where managed instructions need refreshing

#### Scenario: Replacement fails
- **WHEN** replacement or post-install version verification fails
- **THEN** the outcome reports failure, attempts to restore the prior application, and leaves recovery information available without altering journal data

#### Scenario: Status reader overlaps readiness
- **WHEN** an installed status command overlaps the helper's readiness interval and exits in time for all readiness checks to succeed
- **THEN** the helper waits and completes the verified replacement rather than failing solely because the status process briefly used the installation

#### Scenario: Transient file sharing conflict
- **WHEN** a target-file sharing conflict appears after an idle process check but clears within the readiness deadline before mutation
- **THEN** the helper retries readiness within the same deadline and replaces files only after process and file checks both pass

#### Scenario: Readiness timeout or cancellation
- **WHEN** a target remains in use through the deadline or readiness is cancelled
- **THEN** no application files have been replaced, no processes have been stopped, journal/configuration remain unchanged, and an actionable failure outcome is recorded

#### Scenario: Permanent preflight error
- **WHEN** payload or installation validation fails permanently, or required access is denied
- **THEN** the helper reports failure without treating the error as a transient status-reader conflict or modifying application files
