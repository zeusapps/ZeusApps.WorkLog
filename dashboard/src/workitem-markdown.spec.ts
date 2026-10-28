import { describe, it, expect } from "vitest";
import type { Entry } from "./models";
import { markdownObservationLimit, workItemMarkdown, WorkItemMarkdown } from "./workitem-markdown";

const entry = (n: number, extra: Partial<Entry> = {}): Entry => ({
  id: "e" + n, repository: "github.com/acme/api", change: null, status: n === 0 ? "started" : "in_progress",
  summary: "Summary " + n, occurredAt: new Date(Date.UTC(2026, 9, 1, 10, n)).toISOString(), agent: null, branch: null, tickets: [], ...extra,
});
const item = (entries: Entry[], total = entries.length): WorkItemMarkdown => ({
  title: "DEP-12", repository: "github.com/acme/api", repositoryLabel: "api",
  tickets: [{ provider: "jira", scope: "company.atlassian.net", id: "DEP-12" }, { provider: "jira", scope: "unknown", id: "DEP-13" }],
  changes: ["repair-links"], firstAt: "2026-10-01T10:00:00Z", lastAt: "2026-10-20T10:00:00Z", status: "done", day: null,
  entries, total, timezone: "UTC",
  formatDate: (v) => v.slice(0, 10), formatDateTime: (v) => v.slice(0, 16).replace("T", " "), statusLabel: (v) => v,
});

describe("workItemMarkdown", () => {
  it("formats title, linked tickets, repository, dates, status, changes, and observations in date order", () => {
    const text = workItemMarkdown(item([entry(2), entry(0), entry(1, { summary: null })]));
    expect(text.split("\n")).toEqual([
      "### DEP-12",
      "Tickets: [DEP-12](https://company.atlassian.net/browse/DEP-12), DEP-13 · Repository: [api](https://github.com/acme/api)",
      "Recorded: 2026-10-01 → 2026-10-20 · Latest status: done · Times in UTC",
      "Changes: repair-links",
      "",
      "- 2026-10-01 10:00 — started — Summary 0",
      "- 2026-10-01 10:01 — in_progress — No summary recorded.",
      "- 2026-10-01 10:02 — in_progress — Summary 2",
      "",
    ]);
  });
  it("gives a local repository no link and collapses multi-line summaries", () => {
    const base = item([entry(0, { summary: "line one\n\nline two" })]);
    const text = workItemMarkdown({ ...base, repository: "local:abc", repositoryLabel: "api" });
    expect(text).toContain("Repository: api\n");
    expect(text).toContain("started — line one line two\n");
  });
  it("states when the maximum truncated the list", () => {
    const entries = Array.from({ length: markdownObservationLimit + 5 }, (_, n) => entry(n % 50, { id: "x" + n }));
    const truncated = workItemMarkdown(item(entries, 1200));
    expect(truncated.split("\n").filter((l) => l.startsWith("- ")).length).toBe(markdownObservationLimit);
    expect(truncated.trimEnd().endsWith("(truncated at 1,000 observations)")).toBe(true);
    const complete = workItemMarkdown(item([entry(0), entry(1)]));
    expect(complete).not.toContain("truncated");
  });
  it("escapes brackets in the title, ticket ID, and repository label", () => {
    const base = item([entry(0)]);
    const text = workItemMarkdown({
      ...base, title: "Fix [urgent] bug", repositoryLabel: "api[v2]",
      tickets: [{ provider: "jira", scope: "company.atlassian.net", id: "DEP[1]" }],
    });
    const lines = text.split("\n");
    expect(lines[0]).toBe("### Fix \\[urgent\\] bug");
    expect(lines[1]).toContain("[DEP\\[1\\]](");
    expect(lines[1]).toContain("[api\\[v2\\]](https://github.com/acme/api)");
    const plain = workItemMarkdown({ ...base, repositoryLabel: "x[1]", repository: "local:x", tickets: [] });
    expect(plain).toContain("Repository: x\\[1\\]");
  });
  it("keeps a recorded URL containing a space valid in the link destination", () => {
    const text = workItemMarkdown({ ...item([entry(0)]), tickets: [{ provider: "jira", scope: "x", id: "T-1", url: "https://t.example.invalid/a b" }] });
    expect(text).toContain("[T-1](https://t.example.invalid/a%20b)");
  });
});
