import { Board, BoardCard, byPriority, isQuiet } from "./board";
import { ticketUrl } from "./links";
import type { DailyItem, DayDigest, Entry, Ticket } from "./models";

export type DailyFormat = "text" | "markdown";
export const noSummary = "No summary recorded";

export interface TodayEntry {
  card: BoardCard;
  /** What was recorded for the card today; null when nothing was. */
  line: DailyItem | null;
}
export interface DailyTodayOptions {
  /** The timezone whose calendar days decide the quiet indication. */
  zone: string;
  /** The saved board priority order. */
  order: readonly string[];
}

const ticketKey = (ticket: Ticket) => ticket.provider + "|" + ticket.scope + "|" + ticket.id;

/** The ticket of an entry, once per ticket ID. */
function distinctTickets(entries: readonly Entry[]): Ticket[] {
  const seen = new Map<string, Ticket>();
  for (const entry of entries) for (const ticket of entry.tickets) if (!seen.has(ticket.id)) seen.set(ticket.id, ticket);
  return [...seen.values()];
}

/**
 * An entry belongs to a change card when it was recorded for that change, or recorded without a change for one of the card's tickets
 * (the board folds a ticket whose history includes the change into the change card). Entries of another change never qualify.
 */
function qualifies(entry: Entry, card: BoardCard): boolean {
  if (entry.change === card.title) return true;
  if (entry.change) return false;
  const own = new Set(card.tickets.map(ticketKey));
  return entry.tickets.some((ticket) => own.has(ticketKey(ticket)));
}

/**
 * What a card recorded today: the `today` item of the work identity for ticket cards; for change cards a line built from today's own
 * entries of that change (an item's `changes` span its whole history, and a change's entries can sit in several items).
 */
function lineFor(card: BoardCard, today: readonly DailyItem[]): DailyItem | null {
  if (card.kind === "ticket") return today.find((item) => item.id === card.row?.id) ?? null;
  if (card.kind !== "change") return null;
  const entries = today
    .filter((item) => card.repositories.includes(item.repository))
    .flatMap((item) => [item.primary, ...item.others])
    .filter((entry) => qualifies(entry, card))
    .sort((a, b) => Date.parse(b.occurredAt) - Date.parse(a.occurredAt) || (a.id < b.id ? 1 : a.id > b.id ? -1 : 0));
  if (entries.length === 0) return null;
  const [primary, ...others] = entries;
  const tickets = distinctTickets(entries);
  return {
    id: card.key,
    title: card.title,
    repository: primary.repository,
    tickets: tickets.length ? tickets : card.tickets,
    changes: [card.title],
    status: primary.status,
    primary,
    others,
  };
}

/**
 * What Today lists, from the Ongoing board: the In-progress cards without the quiet indication, plus every card of any column with an
 * observation since `todayStart`, each once, in the board's priority order. `proposed` counts the Proposed cards, which are never listed.
 */
export function dailyToday(
  board: Board,
  todayStart: string,
  today: readonly DailyItem[],
  options: DailyTodayOptions,
): { items: TodayEntry[]; proposed: number } {
  const start = Date.parse(todayStart);
  // Quiet is judged in calendar days, and the start of today is on the same calendar day as now.
  const now = new Date(start);
  const chosen = new Map<string, BoardCard>();
  for (const card of board.in_progress) if (!isQuiet(card, now, options.zone)) chosen.set(card.key, card);
  for (const stage of ["proposed", "in_progress", "done"] as const)
    for (const card of board[stage])
      if (card.lastAt !== null && Date.parse(card.lastAt) >= start && !chosen.has(card.key)) chosen.set(card.key, card);
  const cards = [...chosen.values()].sort(byPriority(options.order));
  return { items: cards.map((card) => ({ card, line: lineFor(card, today) })), proposed: board.proposed.length };
}

const coverKey = (ids: readonly string[]) => [...ids].sort().join("|");

/** The digests indexed by their covered observations, for exact-set lookup. */
export function digestIndex(digests: readonly DayDigest[] | undefined): ReadonlyMap<string, DayDigest> {
  return new Map((digests ?? []).map((d) => [coverKey(d.covers), d]));
}

/** The digest covering exactly the observations the line represents (its primary plus its others); null when none does. */
export function digestFor(index: ReadonlyMap<string, DayDigest>, line: DailyItem | null): DayDigest | null {
  if (!line || index.size === 0) return null;
  return index.get(coverKey([line.primary.id, ...line.others.map((e) => e.id)])) ?? null;
}

export const noBlockers = "No blockers recorded";

/** What a line shows: Done / Next / Blockers bullets, and whether they come from a stored agent digest. */
export interface LinePresentation {
  done: string[];
  next: string[];
  blockers: string[];
  agent: boolean;
}

function oneLine(value: string | null | undefined): string | null {
  const text = value?.replace(/\s+/g, " ").trim();
  return text ? text : null;
}

const bulletsOf = (values: readonly string[] | undefined) => (values ?? []).map(oneLine).filter((v): v is string => v !== null);

/**
 * The bullets of a line: the lists of a matched structured digest; a matched digest without lists (a single text, as saved by an earlier
 * release) as one Done bullet; and with no match the line's latest summary, or the no-summary label, as one Done bullet.
 */
export function linePresentation(digest: DayDigest | null, summary: string | null | undefined): LinePresentation {
  if (digest) {
    const done = bulletsOf(digest.done);
    const next = bulletsOf(digest.next);
    const blockers = bulletsOf(digest.blockers);
    if (done.length || next.length || blockers.length) return { done, next, blockers, agent: true };
    return { done: [oneLine(digest.text) ?? noSummary], next: [], blockers: [], agent: true };
  }
  return { done: [oneLine(summary) ?? noSummary], next: [], blockers: [], agent: false };
}

/** The blockers of one Today line, with the line's tickets and title. */
export interface BlockerGroup {
  tickets: Ticket[];
  title: string;
  bullets: string[];
}

/** The blocker bullets of the digests shown on Today lines, in Today's order. Nothing else contributes. */
export function dailyBlockers(today: readonly TodayEntry[], index: ReadonlyMap<string, DayDigest>): BlockerGroup[] {
  const groups: BlockerGroup[] = [];
  for (const { card, line } of today) {
    const digest = digestFor(index, line);
    const bullets = digest ? linePresentation(digest, null).blockers : [];
    if (line && bullets.length) groups.push({ tickets: line.tickets, title: card.title, bullets });
  }
  return groups;
}

/** "Thu 22 Oct" for a yyyy-MM-dd date. */
export function dayLabel(date: string): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat("en-GB", { timeZone: "UTC", weekday: "short", day: "numeric", month: "short" })
      .formatToParts(new Date(date + "T12:00:00Z"))
      .map((part) => [part.type, part.value]),
  );
  return parts["weekday"] + " " + parts["day"] + " " + parts["month"];
}

export function statusWord(status: string): string {
  return status.replace(/_/g, " ");
}

interface CopyLine {
  tickets: Ticket[];
  title: string;
  /** Null for a line with nothing recorded: only the title or ticket IDs are copied. */
  status: string | null;
  /** The latest summary, copied on the line itself when no digest is shown. */
  summary: string | null;
  /** The shown digest's bullets, copied under their labels instead of a summary. */
  parts: LinePresentation | null;
}

function yesterdayLine(item: DailyItem, digest: DayDigest | null): CopyLine {
  const adhoc = item.id.startsWith("adhoc:");
  // An unlinked observation is titled by its own summary, which is not repeated.
  return {
    tickets: item.tickets,
    title: adhoc ? (oneLine(item.primary.summary) ?? noSummary) : item.title,
    status: item.status,
    summary: adhoc || digest ? null : oneLine(item.primary.summary),
    parts: adhoc || !digest ? null : linePresentation(digest, null),
  };
}

function todayLine(entry: TodayEntry, digest: DayDigest | null): CopyLine {
  const { card, line } = entry;
  if (!line) return { tickets: card.tickets, title: card.title, status: null, summary: null, parts: null };
  return {
    tickets: line.tickets,
    title: card.title,
    status: line.status,
    summary: digest ? null : oneLine(line.primary.summary),
    parts: digest ? linePresentation(digest, null) : null,
  };
}

function ticketIds(tickets: readonly Ticket[]): Ticket[] {
  const seen = new Set<string>();
  return [...tickets]
    .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0))
    .filter((t) => (seen.has(t.id) ? false : (seen.add(t.id), true)));
}

function label(line: CopyLine, format: DailyFormat, links: boolean): string {
  const tickets = ticketIds(line.tickets);
  if (tickets.length === 0) return line.title;
  return tickets
    .map((ticket) => {
      const url = links ? ticketUrl(ticket) : null;
      if (!url) return ticket.id;
      return format === "markdown" ? "[" + ticket.id + "](" + url + ")" : ticket.id + " (" + url + ")";
    })
    .join(" + ");
}

/** A line as a bullet; a shown digest follows as labelled lists, nested one level in Markdown and indented in plain text. */
function bullet(line: CopyLine, format: DailyFormat, links: boolean): string[] {
  // Exactly one Done bullet is copied inline, whatever its source; anything more is copied as labelled lists.
  const single = line.parts && line.parts.done.length === 1 && line.parts.next.length === 0 && line.parts.blockers.length === 0;
  const summary = single ? line.parts!.done[0] : line.summary;
  const listed = single ? null : line.parts;
  const parts = [label(line, format, links)];
  if (line.status) parts.push(statusWord(line.status));
  if (summary) parts.push(summary);
  const lines = ["- " + parts.join(" — ")];
  if (!listed) return lines;
  const markdown = format === "markdown";
  for (const [name, bullets] of [["Done", listed.done], ["Next", listed.next], ["Blockers", listed.blockers]] as const) {
    if (bullets.length === 0) continue;
    lines.push(markdown ? "  - " + name + ":" : "  " + name + ":");
    for (const text of bullets) lines.push((markdown ? "    - " : "  - ") + text);
  }
  return lines;
}

export interface DailyCopyInput {
  /** The yyyy-MM-dd date of the Yesterday section; null when no earlier work is recorded. */
  day: string | null;
  yesterday: readonly DailyItem[];
  today: readonly TodayEntry[];
  format: DailyFormat;
  links: boolean;
  /** The stored agent digests; a line they cover exactly is copied with its digest bullets in place of its latest summary; the Blockers section is derived from the Today lines. */
  digests?: readonly DayDigest[];
}

/** The Yesterday, Today, and Blockers sections as copyable text: only recorded facts, and the Blockers derived from Today's digests. */
export function dailyText(input: DailyCopyInput): string {
  const { format, links } = input;
  const heading = (name: string) => (format === "markdown" ? "**" + name + "**" : name + ":");
  const section = (name: string, bullets: string[]) =>
    format === "markdown"
      ? [heading(name), "", ...bullets, ""]
      : [heading(name), ...bullets];
  const index = digestIndex(input.digests);
  const yesterday = input.yesterday.flatMap((item) => bullet(yesterdayLine(item, digestFor(index, item)), format, links));
  const today = input.today.flatMap((entry) => bullet(todayLine(entry, digestFor(index, entry.line)), format, links));
  const blockers = dailyBlockers(input.today, index).flatMap((group) => {
    const name = label({ tickets: group.tickets, title: group.title, status: null, summary: null, parts: null }, format, links);
    return group.bullets.map((text) => "- " + name + " — " + text);
  });
  const lines = [
    ...section("Yesterday" + (input.day ? " (" + dayLabel(input.day) + ")" : ""), yesterday.length ? yesterday : ["- Nothing recorded"]),
    ...section("Today", today.length ? today : ["- Nothing recorded"]),
    ...section("Blockers", blockers.length ? blockers : ["- " + noBlockers]),
  ];
  if (format === "markdown") lines.pop();
  return lines.join("\n");
}
