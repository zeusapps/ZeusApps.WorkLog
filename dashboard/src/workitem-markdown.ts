import { repositoryUrl, ticketUrl } from "./links";
import type { Entry, Ticket } from "./models";

/** Most observations Copy as Markdown loads and writes. */
export const markdownObservationLimit = 1000;

export interface WorkItemMarkdown {
  title: string;
  repository: string;
  repositoryLabel: string;
  tickets: Ticket[];
  changes: string[];
  firstAt: string | null | undefined;
  lastAt: string | null | undefined;
  /** The item's latest recorded status when known; otherwise the last loaded observation's. */
  status?: string | null;
  /** The scope the entries cover when it is one local day. */
  day: string | null;
  entries: Entry[];
  /** Observations of the scope that exist, which may exceed the entries loaded. */
  total: number;
  timezone: string;
  formatDate(value: string): string;
  formatDateTime(value: string): string;
  statusLabel(status: string): string;
}

const oneLine = (text: string) => text.replace(/\s+/g, " ").trim();
const escapeText = (text: string) => text.replace(/([\[\]])/g, "\\$1");

/** Formats a work item as Markdown from recorded facts only: no inference, no wording about progress. */
export function workItemMarkdown(item: WorkItemMarkdown): string {
  const entries = [...item.entries]
    .sort((a, b) => Date.parse(a.occurredAt) - Date.parse(b.occurredAt))
    .slice(0, markdownObservationLimit);
  const truncated = item.total > entries.length;
  const tickets = new Map<string, Ticket>();
  for (const ticket of [...item.tickets, ...entries.flatMap((e) => e.tickets ?? [])])
    tickets.set(ticket.provider + "\u0000" + ticket.scope + "\u0000" + ticket.id, ticket);
  const ticketText = [...tickets.values()].map((ticket) => {
    const url = ticketUrl(ticket);
    return url ? "[" + escapeText(ticket.id) + "](" + url + ")" : escapeText(ticket.id);
  });
  const repositoryLink = repositoryUrl(item.repository);
  const repository = repositoryLink
    ? "[" + escapeText(item.repositoryLabel) + "](" + repositoryLink + ")"
    : escapeText(item.repositoryLabel);
  const changes = new Set([...item.changes, ...entries.map((e) => e.change).filter((c): c is string => !!c)]);
  const first = item.firstAt ?? entries[0]?.occurredAt;
  const last = item.lastAt ?? entries.at(-1)?.occurredAt;
  const latestStatus = item.status ?? entries.at(-1)?.status;
  const lines = ["### " + escapeText(oneLine(item.title))];
  lines.push(
    (ticketText.length ? "Tickets: " + ticketText.join(", ") + " · " : "") + "Repository: " + repository,
  );
  lines.push(
    (first && last ? "Recorded: " + item.formatDate(first) + " → " + item.formatDate(last) + " · " : "") +
      (latestStatus ? "Latest status: " + item.statusLabel(latestStatus) + " · " : "") +
      "Times in " + item.timezone,
  );
  if (changes.size) lines.push("Changes: " + [...changes].join(", "));
  if (item.day) lines.push("Scope: observations on " + item.day);
  lines.push("");
  for (const entry of entries)
    lines.push(
      "- " + item.formatDateTime(entry.occurredAt) + " — " + item.statusLabel(entry.status) + " — " +
        (entry.summary ? oneLine(entry.summary) : "No summary recorded."),
    );
  if (truncated)
    lines.push("", "(truncated at " + markdownObservationLimit.toLocaleString("en-US") + " observations)");
  return lines.join("\n") + "\n";
}
