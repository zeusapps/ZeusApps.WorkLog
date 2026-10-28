# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `fix-dashboard-reliability` and `add-archive-browser-and-editor-open`; verify `/api/activity/changes` exists before group 4.

## 1. Link builder

- [x] 1.1 Add `dashboard/src/links.ts` with `ticketUrl`, `repositoryUrl`, `branchUrl` (design Decision 1); verify with Angular unit tests for recorded URL precedence, Jira host scope, Azure DevOps org and org/project and visualstudio.com scopes, unrecognized scopes, GitHub/GitLab/Bitbucket/Azure DevOps HTTPS and SSH identities, `local:` and `path:` identities, and percent-encoding of branches with `/` and spaces
- [x] 1.2 Render ticket links and copy-ID actions on timeline rows, Ongoing items, insights, and drill-down observations; render repository and branch links in the drill-down; verify with unit tests that unlinkable tickets render without anchors and that link clicks go through the existing explicit external-navigation handler

## 2. Ticket filter

- [x] 2.1 Add `TicketFilter.Matches` in `src/WorkLog/Reporting` and a parity test against `SqliteEntryStore.ReadPage` with the same fixtures (Decision 2); verify the parity test passes for bare, qualified, whitespace, unresolved-ID, and different-site cases
- [x] 2.2 Add `ticket` to `TimelineQuery` and identity selection in `TimelineService.Project` that keeps full lifecycle context; verify with .NET tests that a filtered row's span and counts equal the unfiltered row's
- [x] 2.3 Add the History ticket filter input with an active-filter indicator and a clear action, remembered with the other history controls; verify with a unit test and a Playwright scenario

## 3. Copy as Markdown

- [x] 3.1 Implement Copy as Markdown with page loading up to 1,000 observations, truncation notice, timezone-aware dates, and clipboard fallback (Decision 3); verify with unit tests for the format and truncation and a Playwright scenario reading the clipboard

## 4. Quiet changes

- [x] 4.1 Show quiet and no-recorded-activity chips on active change cards using `/api/activity/changes`, refreshed on `journalSnapshot` change (Decision 4); verify with unit tests for 13-day, 15-day, and no-activity cases

## 5. Acceptance and docs

- [x] 5.1 Add Playwright scenarios with fixture tickets (Jira host, Azure DevOps org/project, recorded URL, unrecognized) and GitHub plus local repositories, confirming link targets without navigating; verify `npm run test:e2e --prefix dashboard` passes
- [x] 5.2 Document link rules, the recognized forms, and the ticket filter in `README.md`; run the full validation set and record results in `docs/dashboard-validation.md`
