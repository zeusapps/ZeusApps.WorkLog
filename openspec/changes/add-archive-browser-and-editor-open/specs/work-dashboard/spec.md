# Spec Delta

## MODIFIED Requirements

### Requirement: Local data access boundary
Dashboard journal and document data SHALL be restricted to the local dashboard browser session and approved local sources. The service SHALL reject cross-origin data access, unexpected host names, unauthorized data requests, and requests escaping source roots, including through filesystem links. Its document writes SHALL be limited to adding, editing, and deleting review markers in active change Markdown files. Its only local launch operation SHALL be opening a discovered, contained document in one of the fixed editor presets. The service SHALL resolve the file from the document identity itself, SHALL reject unknown presets, and SHALL NOT accept file paths, command lines, or arguments from the client. Markdown content SHALL not execute scripts or trigger automatic remote resource requests. The dashboard SHALL not send journal data, documents, searches, or comments to external services; explicit external links SHALL require user navigation.

#### Scenario: Unrelated website attempts journal access
- **WHEN** a request from an unrelated web origin attempts to read dashboard data
- **THEN** the service rejects the request without returning private content

#### Scenario: Unexpected host name
- **WHEN** a request reaches the listener with a Host header other than the dashboard's loopback address
- **THEN** the service rejects it without returning private content

#### Scenario: Missing authorization
- **WHEN** a request without a valid dashboard session requests journal or document data
- **THEN** the service rejects it and the client explains how to launch the dashboard

#### Scenario: Document request escapes its source
- **WHEN** a document request uses encoded traversal, an absolute path, or a junction or symbolic link pointing outside its source root
- **THEN** the service rejects it without reading the outside file

#### Scenario: Unsafe document content
- **WHEN** a document contains executable markup or an automatically loaded remote image
- **THEN** the rendered view executes no document script and makes no automatic remote request

#### Scenario: Launch request names an unknown preset or escapes its source
- **WHEN** an open-in-editor request names an editor outside the fixed presets, or targets a document outside a discovered source through traversal or a filesystem link
- **THEN** the service rejects it without launching any process

#### Scenario: Unauthorized launch request
- **WHEN** an open-in-editor request arrives without a valid dashboard session or from another origin
- **THEN** the service rejects it without launching any process

## ADDED Requirements

### Requirement: Change activity summary
The dashboard SHALL provide, for each repository and recorded change name in the selected repositories, the latest recorded observation's status and occurrence time and the first recorded observation's occurrence time, computed from all journal history without modifying it. A change name with no observations SHALL be reported as having no recorded activity. The summary SHALL use the same read-only journal access as other dashboard views and SHALL treat a missing journal as empty.

#### Scenario: Latest status for a change
- **WHEN** a change has started, in_progress, and done observations
- **THEN** the summary reports done with the done observation's time as the latest activity

#### Scenario: Missing journal
- **WHEN** no journal exists in the data directory
- **THEN** the summary is empty and views show no recorded activity
