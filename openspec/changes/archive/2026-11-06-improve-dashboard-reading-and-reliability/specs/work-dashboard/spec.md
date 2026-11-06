# Spec Delta

## ADDED Requirements

### Requirement: Persistent configuration reload failures
When repository configuration cannot be reloaded, the dashboard SHALL identify that it is retaining the last successfully loaded configuration and SHALL show an actionable warning until a reload succeeds. Unchanged failure conditions SHALL remain visible across refreshes without accumulating duplicate notices. A successful reload SHALL clear the warning and expose the successfully loaded configuration while preserving journal availability and valid repository selection behavior.

#### Scenario: Failed configuration persists across refreshes
- **WHEN** repository configuration becomes unreadable or invalid and multiple refreshes occur
- **THEN** the dashboard keeps one visible reload warning and identifies continued use of the last valid configuration

#### Scenario: Configuration recovers
- **WHEN** valid readable configuration becomes available after a reload failure
- **THEN** the dashboard retries, applies the recovered configuration, and clears the stale warning

### Requirement: Session-aware launch recovery
An expired or already-used launch token SHALL NOT prevent an already-authorized browser from opening the dashboard when the existing session is independently confirmed by a protected request. Without a valid session, the dashboard SHALL retain the authorization boundary and explain how to launch and authorize the browser. Startup failures SHALL produce a useful recovery action without an unbounded retry loop. A launch-token fragment SHALL be removed from the visible URL after it has been handled.

#### Scenario: Used token with a valid browser session
- **WHEN** an already-authorized browser opens a URL containing a used or expired launch token
- **THEN** the dashboard confirms the existing session and loads without requiring another authorization exchange or leaving the token in the URL

#### Scenario: Used token without a valid browser session
- **WHEN** a browser without a valid session opens an expired or used launch token
- **THEN** protected data stays inaccessible and the dashboard explains how to relaunch it to authorize the browser

#### Scenario: A startup request cannot complete
- **WHEN** startup fails because the dashboard cannot be reached or an unexpected server failure occurs
- **THEN** the interface identifies the failure and offers a retry rather than treating it as authorization success or repeatedly issuing requests without user feedback
