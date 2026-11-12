# Tasks

## 1. Keyboard focus during Retry updates (ANALYSIS-705)

- [x] 1.1 Keep keyboard focus inside the reader, or the page status area or main content, when Retry updates is activated from the keyboard, including while the control is disabled or removed during recovery. Leave focus untouched when it was elsewhere at activation, for example an unsaved draft. Verify with unit tests, written to fail first, for reader and page Retry and for the draft case, plus an e2e assertion that `document.activeElement` stays inside the reader after Enter on Retry while disconnected.

## 2. Monotonic pending live delivery (ANALYSIS-703)

- [x] 2.1 Make `Slot.Offer` in `DashboardLiveConnections.cs` merge an offered vector into the pending one for the same instance: the component-wise maximum of the domain revisions, the union of `Dirty`, and the maximum checkpoint. A different instance replaces the pending vector. Verify with .NET unit tests, written to fail first: offering N+1 then N delivers N+1 with both hints, a pending checkpoint survives, and a new instance replaces the pending vector. Then run the full .NET suite.

## 3. Documentation and completion

- [x] 3.1 Update `docs/dashboard-validation.md`. Record that the user dropped the freshness p95 measurement, server-work capture, the 100-cycle matrix and the remaining minor suggestions (ANALYSIS-704, QA-600, QA-601, QA-602, QA-651), not deferred them, so `realtime-dashboard-performance-evidence` will not be created. Mark QA-652 as resolved by ANALYSIS-782 and ANALYSIS-705/703 as fixed here. Verify with a hygiene re-scan and `openspec validate dashboard-live-polish --strict`.
- [x] 3.2 Run the dashboard unit suite and the full e2e suite. Obtain a light QA review of 1.1 and a code-analyzer review of 2.1 on the stable target. Reconcile the findings ledger, and commit after user approval. A new dev build is installed only if the user asks.
