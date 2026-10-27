# Analysis report: <change> / <round>

Template only: replace placeholders with actual analysis evidence. Write to the orchestrator-reserved path; retain previous reports.

## Review target

- Reviewer / role: <identity> / code-analyzer
- Client / version / mode / model (resolved ID): <observed values, for example client name, version, mode, and the resolved model ID rather than an alias; or explicitly unavailable>
- Date: <UTC timestamp>
- Change root / assignment: <resolved root and assignment ID>
- Task and requirement references: <IDs and change-relative links>
- Scope and relevant neighboring paths: <explicit paths>
- Reading limits and paths read outside scope: <assignment limits; any other paths read, or none>
- Baseline commit: <commit>
- Implementation identity: <`scripts/review-identity.ps1` output before the analysis: HEAD, manifest, diff SHA-256, untracked hashes, summary line>
- Before/after identity: <the same command after the analysis and the comparison, or unexpected differences>
- Prior reports / rechecked findings: <relative links and IDs, or none>

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| <duplication / complexity / simplification / architecture / performance> | <inspection or exact check> | <specific observation or relative link> | <result or not applicable/not performed with reason> |

## Steady-state cost and bounds

Every row is required. Use not applicable only when the target has no long-running or accumulating component, and say why.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | <timer/poll/loop> | <measured / source observation / hypothesis> | <per-interval work and how often inputs change> |
| Cache lifetime versus interval and refresh duration | <caches> | <type> | <relief provided or not> |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | <state> | <type> | <bound/eviction or missing> |
| Repeated full reads where a change check would do | <requests> | <type> | <observation> |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | <lookups and spawned tools> | <type> | <install methods covered, and calls without a timeout> |

## Findings

State **No findings** explicitly when applicable. Otherwise repeat:

### <ANALYSIS-ID>: <title>

- Severity: <critical / warning / suggestion>
- Related task / requirement: <IDs>
- Locations: <repository-relative paths and lines at reviewed identity>
- Observation: <concrete duplication, complexity, drift, or performance evidence>
- Proposed improvement and benefit: <bounded recommendation>
- Behavior-preservation risks: <affected contracts and edge cases>
- Evidence type: <measured result / source observation / performance hypothesis>
- Verification / measurement plan: <proportional check; do not imply a hypothesis was measured>
- Relationship: <existing/canonical finding ID if repeated or duplicate>

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| <existing ID> | <identity> | <inspection/check and result> | <verified / still failing / not performed> |

## Assessment and limitations

<Scope covered, recommendations, measurements not performed, unexpected writes, and decisions needed. The orchestrator owns triage and ledger status.>
