import {
  Component,
  computed,
  inject,
  signal,
  OnDestroy,
  AfterViewInit,
  HostListener,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { DomSanitizer, SafeHtml } from "@angular/platform-browser";
import { NavigationEnd, Router, RouterOutlet } from "@angular/router";
import {
  api,
  ApiError,
  addDays,
  boundary,
  elapsed,
  humanize,
  localDate,
} from "./api";
import { renderMarkdown, Heading, Review } from "./markdown";
import { activityFor, archiveEntries, editorLabel, editorPresets, firstVisibleLine, sourceLineAfter, validEditor } from "./archive";
import { branchUrl, repositoryUrl, ticketTitle, ticketUrl } from "./links";
import { activityOf, quietDays } from "./quiet";
import { markdownObservationLimit, workItemMarkdown } from "./workitem-markdown";
import { changeStops, readMarks, ReadMarks, sourceSegments } from "./diff";
import {
  baselineKey,
  changeKey,
  countByChange,
  countByDocument,
  documentKey,
  groupReviews,
  removedReviews,
  ReviewGroup,
  ReviewScope,
  updatedDocuments,
} from "./reviews";
import {
  HistoryRange,
  defaultRange,
  effectiveRange,
  normalizeRange,
  rangeLabel,
  rangePresets,
  touchRecent,
  validDate,
  validTimezone,
} from "./range";
import {
  Catalog,
  Change,
  ChangeActivity,
  EditorPreset,
  DocumentContent,
  DocumentRequest,
  DocumentUnchanged,
  Entry,
  Insight,
  Match,
  ObservationPage,
  Preferences,
  ReadBaseline,
  ReadMarkResult,
  ReadSummary,
  ReviewItem,
  ReviewList,
  Row,
  SearchResults,
  Selection,
  Timeline,
  TimelineQuery,
} from "./models";

/** Most recently used reading positions and remembered document selections kept in preferences. */
export const positionLimit = 100;
export const documentLimit = 200;
const scrollSaveDelay = 1000;

@Component({
  selector: "worklog-app",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet],
  templateUrl: "./app.html",
})
export class App implements OnDestroy, AfterViewInit {
  readonly router = inject(Router);
  private sanitizer = inject(DomSanitizer);
  catalog = signal<Catalog | null>(null);
  preferences: Preferences = { schemaVersion: 1 };
  selectedRepos = signal<string[] | null>(null);
  view = signal<"ongoing" | "history" | "archive">("ongoing");
  ready = signal(false);
  unauthorized = signal(false);
  error = signal("");
  notice = signal("");
  loading = signal(false);
  documentLoading = signal(false);
  theme = signal<"dark" | "light">("dark");
  searchLoading = signal(false);
  timeline = signal<Timeline | null>(null);
  ongoing = signal<Row[]>([]);
  historyQuery: TimelineQuery | null = null;
  ongoingQuery: TimelineQuery | null = null;
  selectedChange = signal<Change | null>(null);
  copies = signal<Change[]>([]);
  document = signal<DocumentContent | null>(null);
  html = signal<SafeHtml>("");
  headings = signal<Heading[]>([]);
  reviews = signal<Review[]>([]);
  /** Open review comments across the selected repositories, active and archived. The overview's scope switch filters them. */
  inbox = signal<ReviewList | null>(null);
  inboxScope = signal<ReviewScope>("active");
  /** The fingerprint (length and write time) each document had when the user last read it, by baseline key. */
  readSummary = signal<ReadSummary["entries"]>({});
  /** The open document as the user last read it. */
  baseline = signal<ReadBaseline | null>(null);
  /** Where the open document differs from the baseline; null when it does not. */
  readMarks = signal<ReadMarks | null>(null);
  removedComments = signal<Review[]>([]);
  changeCount = signal(0);
  changePosition = signal(0);
  readonly inboxScopes: { value: ReviewScope; label: string }[] = [
    { value: "active", label: "Active" },
    { value: "archived", label: "Archived" },
    { value: "all", label: "All" },
  ];
  editingReview = signal<(Review & { revision: string }) | null>(null);
  deletingReview = signal<(Review & { revision: string }) | null>(null);
  sourceView = signal(false);
  /** Case-insensitive name filter of the Archive view. */
  archiveFilter = signal("");
  /** First and latest recorded observation of every change name in the selected repositories. */
  activity = signal<ChangeActivity[]>([]);
  private activityKey = "";
  /** The repository selection (as JSON) the loaded activity covers; null until the first load. */
  private activityScope = signal<string | null>(null);
  private activityEpoch = 0;
  /** The remembered editor preset for Open in editor. */
  editor = signal<EditorPreset>("vscode");
  readonly editorPresets = editorPresets;
  readonly editorLabel = editorLabel;
  selection = signal<Selection | null>(null);
  comment = signal("");
  commentStatus = signal("");
  saving = signal(false);
  searchText = "";
  searchScope = "active";
  searchResults = signal<SearchResults | null>(null);
  /** Effective History bounds currently queried (the focused day, or the evaluated range). */
  from = "";
  to = "";
  fromInput = "";
  toInput = "";
  timezone = "UTC";
  timezoneInput = "UTC";
  range: HistoryRange = { ...defaultRange };
  readonly rangePresets = rangePresets;
  readonly rangeLabel = rangeLabel;
  focusedDay = signal<string | null>(null);
  rangeError = signal("");
  timezoneError = signal("");
  /** The running application's one version label (plain version, or the development label). Empty when unknown. */
  versionLabel = signal("");
  preferencesNotice = signal("");
  private preferencesNoticeShown = false;
  private rememberedTimezone?: string;
  private evaluatedDate = "";
  private journalSnapshot: number | undefined;
  private searchRevision: string | null = null;
  private lastSaved = "";
  protected now = () => new Date();
  groupBy = "ticket";
  scale = "day";
  /** The History ticket filter as typed, and the trimmed filter the timeline is queried with ("" when off). */
  ticketInput = "";
  ticket = "";
  readonly ticketUrl = ticketUrl;
  readonly ticketTitle = ticketTitle;
  readonly repositoryUrl = repositoryUrl;
  readonly branchUrl = branchUrl;
  readonly quietDays = quietDays;
  /** A short confirmation after a copy action, and the text to copy by hand when the clipboard is unavailable. */
  copyToast = signal("");
  manualCopy = signal<{ title: string; text: string; multiline: boolean } | null>(null);
  markdownBusy = signal(false);
  private toastTimer?: ReturnType<typeof setTimeout>;
  repoMenu = signal(false);
  detail = signal<
    (Partial<Row> & { id: string; title: string; repository: string }) | null
  >(null);
  observations = signal<Entry[]>([]);
  observationNext = signal<number | null>(null);
  observationTotal = signal(0);
  detailDay = signal<string | null>(null);
  detailLoading = signal(false);
  detailQuery: TimelineQuery | null = null;
  private timer?: ReturnType<typeof setInterval>;
  private saveTimer?: ReturnType<typeof setTimeout>;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private historyEpoch = 0;
  private searchEpoch = 0;
  private documentEpoch = 0;
  private detailEpoch = 0;
  private polling = false;
  private reviewEpoch = 0;
  private summaryEpoch = 0;
  private overviewRevision: string | null = null;
  private changeCursor = -1;
  private marksMemo: { key: string; marks: ReadMarks | null } | null = null;
  private drafts = new Map<
    string,
    { selection: Selection | null; comment: string; editing: (Review & { revision: string }) | null }
  >();
  private navigating = false;
  readonly humanize = humanize;
  readonly elapsed = elapsed;
  readonly activeChanges = computed(() =>
    this.filteredChanges().filter((c) => !c.archived),
  );
  readonly archiveList = computed(() =>
    archiveEntries(
      this.catalog()?.changes ?? [],
      (repositories) => this.inScope(repositories),
      this.archiveFilter(),
      this.activity(),
    ),
  );
  readonly filteredChanges = computed(
    () =>
      this.catalog()?.changes.filter((c) => this.inScope(c.repositories)) ?? [],
  );
  readonly ongoingTickets = computed(() =>
    this.ongoing().filter(
      (r) =>
        r.status !== "done" &&
        !this.activeChanges().some(
          (c) =>
            c.repositories.includes(r.repository) && r.changes.includes(c.name),
        ),
    ),
  );
  readonly reviewGroups = computed<ReviewGroup[]>(() =>
    groupReviews(this.inbox()?.items ?? [], this.inboxScope()),
  );
  readonly reviewTotal = computed(() =>
    this.reviewGroups().reduce((total, group) => total + group.count, 0),
  );
  readonly changeComments = computed(() => countByChange(this.inbox()?.items ?? []));
  readonly documentComments = computed(() => countByDocument(this.inbox()?.items ?? []));
  /** Open comments of the change the reader shows, in document order. */
  readonly readerComments = computed(() => {
    const change = this.selectedChange();
    return change
      ? (this.inbox()?.items ?? []).filter(
          (item) => item.sourceId === change.sourceId && item.variant === change.variant,
        )
      : [];
  });
  readonly sourceParts = computed(() => {
    const d = this.document();
    return d ? sourceSegments(d.text, this.readMarks()) : [];
  });
  readonly selectedRepoLabel = computed(() => {
    const selected = this.selectedRepos();
    if (!selected) return "All repositories";
    return selected.length === 1
      ? this.repositoryLabel(selected[0])
      : selected.length + " repositories";
  });
  readonly missingDocuments = computed(() => {
    const change = this.selectedChange();
    return change
      ? ["proposal.md", "design.md", "tasks.md"].filter(
          (p) => !change.documents.some((d) => d.path === p),
        )
      : [];
  });
  readonly visibleObservations = computed(() => {
    const day = this.detailDay();
    return day
      ? this.observations().filter((e) => this.day(e.occurredAt) === day)
      : this.observations();
  });

  constructor() {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd && this.ready())
        void this.routeChanged();
    });
  }
  ngAfterViewInit() {
    void this.start();
  }
  ngOnDestroy() {
    clearInterval(this.timer);
    clearTimeout(this.saveTimer);
    clearTimeout(this.searchTimer);
    clearTimeout(this.toastTimer);
  }
  private async start() {
    try {
      const fragment = new URLSearchParams(window.location.hash.slice(1));
      const token = fragment.get("connect");
      if (token) {
        await api("bootstrap", { token });
        window.history.replaceState(
          null,
          "",
          window.location.pathname + window.location.search,
        );
      }
      const [catalog, state, info] = await Promise.all([
        api<Catalog>("catalog"),
        api<Preferences>("state"),
        api<{ timezone: string; version?: string }>("info"),
      ]);
      this.catalog.set(catalog);
      this.versionLabel.set(info.version ?? "");
      this.preferences = state;
      this.editor.set(validEditor(state.editor));
      this.applyTheme(state.theme === "light" ? "light" : "dark");
      const known = new Set(catalog.repositories.map((r) => r.id));
      const saved = state.repositories?.filter((r) => known.has(r));
      this.selectedRepos.set(
        saved?.length
          ? saved
          : state.repositories === null
            ? null
            : catalog.launchRepository
              ? [catalog.launchRepository]
              : null,
      );
      // A remembered zone must be accepted by the browser and the server; an older save the server rejects is dropped.
      // Any other probe failure leaves the zone "not verified": it is kept and History reports its own error.
      const savedZone =
        validTimezone(state.history?.timezone) &&
        (state.history!.timezone === info.timezone ||
          (await this.serverAcceptsTimezone(state.history!.timezone!).catch(
            () => true,
          )))
          ? state.history!.timezone
          : undefined;
      this.rememberedTimezone = savedZone;
      this.timezone = savedZone ?? info.timezone;
      this.timezoneInput = this.timezone;
      this.groupBy = state.history?.groupBy ?? "ticket";
      this.ticket = this.ticketInput = state.history?.ticket?.trim() ?? "";
      this.scale = state.history?.scale ?? "day";
      // A range saved by an earlier version has no kind and becomes the default relative range.
      this.range = normalizeRange(state.history?.range);
      this.evaluate();
      this.journalSnapshot = catalog.journalSnapshot;
      this.ready.set(true);
      if (
        window.location.pathname === "/" &&
        !window.location.search &&
        state.lastRoute
      ) {
        await this.router.navigateByUrl(state.lastRoute);
      } else await this.routeChanged();
      await this.refreshData();
      this.overviewRevision = this.catalog()?.revision ?? null;
      await this.refreshOverview();
      this.timer = setInterval(() => void this.poll(), 2000);
    } catch (error) {
      this.report(error);
    }
  }
  private report(error: unknown) {
    if (error instanceof ApiError && error.status === 401)
      this.unauthorized.set(true);
    this.error.set(error instanceof Error ? error.message : String(error));
  }
  private applyTheme(theme: "dark" | "light") {
    this.theme.set(theme);
    document.documentElement.dataset["theme"] = theme;
  }
  toggleTheme() {
    this.applyTheme(this.theme() === "dark" ? "light" : "dark");
    this.preferences.theme = this.theme();
    this.remember();
  }
  inScope(repositories: string[]) {
    return (
      this.selectedRepos() === null ||
      repositories.some((r) => this.selectedRepos()!.includes(r))
    );
  }
  repositoryLabel(id: string) {
    return this.catalog()?.repositories.find((r) => r.id === id)?.label ?? id;
  }
  sourceLabel(id: string) {
    return (
      this.catalog()?.sources.find((s) => s.id === id)?.location ??
      "Unavailable source"
    );
  }
  sourceShort(id: string) {
    return this.sourceLabel(id).split(/[\\/]/).at(-1) ?? this.sourceLabel(id);
  }
  statusLabel(status: string) {
    return status === "done"
      ? "Recorded done"
      : status === "started"
        ? "Recorded start"
        : "In progress";
  }
  day(value: string) {
    try {
      return localDate(new Date(value), this.timezone);
    } catch {
      return value.slice(0, 10);
    }
  }
  formatDate(value: string) {
    try {
      return new Intl.DateTimeFormat("en-GB", {
        timeZone: this.timezone,
        day: "numeric",
        month: "short",
        year: "numeric",
      }).format(new Date(value));
    } catch {
      return value;
    }
  }
  formatTime(value: string) {
    try {
      return new Intl.DateTimeFormat("en-GB", {
        timeZone: this.timezone,
        hour: "2-digit",
        minute: "2-digit",
      }).format(new Date(value));
    } catch {
      return "";
    }
  }
  /** Loads the open review comments and the read summary: the data behind the overview, the chips and the counts. */
  async refreshOverview() {
    await Promise.all([this.loadReviews(), this.loadReadSummary()]);
  }
  async loadReviews() {
    const epoch = ++this.reviewEpoch;
    try {
      // Always every scope: the reader and archived documents need their counts whichever scope the panel shows.
      const list = await api<ReviewList>("reviews", {
        repositories: this.selectedRepos() ?? [],
        scope: "all",
      });
      if (epoch === this.reviewEpoch) this.inbox.set(list);
    } catch (error) {
      if (epoch === this.reviewEpoch) this.report(error);
    }
  }
  /**
   * Loads the recorded activity of the selected repositories for the Archive view and the quiet indicators of Ongoing work.
   * Skipped while History is open, and when neither the journal nor the selection moved since the last load.
   */
  async loadActivity() {
    if (this.view() === "history") return;
    const repositories = this.selectedRepos() ?? [];
    const key = this.journalSnapshot + "|" + JSON.stringify(repositories);
    if (key === this.activityKey) return;
    const epoch = ++this.activityEpoch;
    try {
      const list = await api<ChangeActivity[]>("activity/changes", { repositories });
      if (epoch !== this.activityEpoch) return;
      this.activity.set(list);
      this.activityScope.set(JSON.stringify(repositories));
      this.activityKey = key;
    } catch (error) {
      if (epoch === this.activityEpoch) this.report(error);
    }
  }
  async loadReadSummary() {
    const epoch = ++this.summaryEpoch;
    try {
      const summary = await api<ReadSummary>("reads/summary");
      if (epoch === this.summaryEpoch) this.readSummary.set(summary.entries);
    } catch {
      // The baselines are unavailable (for example a locked file): no document is shown as updated, and there is nothing to report on every refresh.
      if (epoch === this.summaryEpoch) this.readSummary.set({});
    }
  }
  commentCount(change: Change) {
    return this.changeComments().get(changeKey(change.sourceId, change.variant)) ?? 0;
  }
  documentCommentCount(change: Change, path: string) {
    return this.documentComments().get(documentKey(change.sourceId, change.variant, path)) ?? 0;
  }
  updatedDocs(change: Change) {
    return updatedDocuments(change, this.readSummary());
  }
  setEditor(value: unknown) {
    this.editor.set(validEditor(value));
    this.preferences.editor = this.editor();
    this.remember();
  }
  /** The zero-based source line at the top of the reader: the first rendered block at or below its top edge. */
  readerLine(): number {
    const scroller = document.querySelector<HTMLElement>(".reader .document-scroll");
    if (!scroller) return 0;
    if (this.sourceView()) {
      const text = scroller.querySelector<HTMLElement>(".source-text");
      const rect = scroller.getBoundingClientRect();
      if (text) {
        // The text wraps and may sit below other content, so ask the layout which character is at the top edge.
        const box = text.getBoundingClientRect();
        if (box.top >= rect.top) return 0;
        const x = Math.min(Math.max(box.left + 1, rect.left + 1), rect.right - 1);
        const y = rect.top + 2;
        const caret = document.caretPositionFromPoint?.(x, y);
        const node = caret?.offsetNode ?? document.caretRangeFromPoint?.(x, y)?.startContainer;
        const offset = caret?.offset ?? document.caretRangeFromPoint?.(x, y)?.startOffset ?? 0;
        if (node && text.contains(node)) {
          const before = document.createRange();
          before.setStart(text, 0);
          before.setEnd(node, offset);
          return sourceLineAfter(before.toString());
        }
      }
      const style = text ? getComputedStyle(text) : null;
      const height = parseFloat(style?.lineHeight ?? "") || (parseFloat(style?.fontSize ?? "") || 12) * 1.5;
      return Math.max(0, Math.floor(scroller.scrollTop / height));
    }
    const top = scroller.getBoundingClientRect().top;
    return firstVisibleLine(
      Array.from(scroller.querySelectorAll<HTMLElement>(".markdown [data-start]")).map((e) => ({
        start: Number(e.dataset["start"]),
        top: e.getBoundingClientRect().top,
      })),
      top,
    );
  }
  /** Opens the document at a line in the remembered editor. The server resolves the file; only the document identity and line are sent. */
  async openInEditor(
    d: { sourceId: string; change: string; path: string; variant?: string | null },
    line: number,
  ) {
    try {
      await api("open", {
        document: { sourceId: d.sourceId, change: d.change, path: d.path, variant: d.variant ?? null },
        line,
        editor: this.editor(),
      });
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) this.report(error);
      else this.notice.set(error instanceof Error ? error.message : String(error));
    }
  }
  /** Opens the reader's document at the reader's current position, or at an explicit line (a heading). */
  async openCurrentInEditor(line?: number) {
    const d = this.document();
    if (d) await this.openInEditor({ ...d, variant: d.variant ?? this.selectedChange()?.variant }, line ?? this.readerLine());
  }
  async openReview(item: ReviewItem) {
    const change = this.catalog()?.changes.find(
      (c) => c.sourceId === item.sourceId && c.variant === item.variant,
    );
    if (!change) {
      this.notice.set("That change is no longer available. The overview refreshes with the next catalog update.");
      return;
    }
    await this.openChange(change, item.path, "note:" + item.line);
  }
  changeRow(change: Change) {
    return this.ongoing().find(
      (r) =>
        r.repository &&
        change.repositories.includes(r.repository) &&
        r.changes.includes(change.name),
    );
  }
  /**
   * Whether an active change has no recorded observations, a latest one older than 14 days, or a recent one.
   * Null while the recorded activity of the current selection is not loaded, so nothing is claimed early.
   */
  changeActivity(change: Change) {
    const scope = this.activityScope();
    if (scope === null || (scope !== "[]" && scope !== JSON.stringify(this.selectedRepos() ?? []))) return null;
    return activityOf(activityFor(change, this.activity())?.lastAt, this.now(), this.timezone);
  }
  latestActivity(change: Change) {
    return activityFor(change, this.activity());
  }
  trackChange(_index: number, c: Change) {
    return c.sourceId + c.variant;
  }
  toggleRepository(id: string) {
    const current = this.selectedRepos();
    this.selectedRepos.set(
      current === null
        ? [id]
        : current.includes(id)
          ? current.filter((r) => r !== id)
          : [...current, id],
    );
    if (this.selectedRepos()?.length === 0) this.selectedRepos.set(null);
    void this.scopeChanged();
  }
  selectAll() {
    this.selectedRepos.set(null);
    void this.scopeChanged();
  }
  private async scopeChanged() {
    this.repoMenu.set(false);
    this.searchResults.set(null);
    this.remember();
    void this.loadReviews();
    void this.loadActivity();
    await this.refreshData();
    if (this.searchText.trim()) void this.search();
  }
  async changeView(view: "ongoing" | "history" | "archive") {
    await this.router.navigate(["/" + view]);
  }
  async refreshData() {
    await Promise.all([this.loadHistory(), this.loadOngoing()]);
  }
  private query(): TimelineQuery {
    return {
      from: boundary(this.from, this.timezone),
      to: boundary(addDays(this.to, 1), this.timezone),
      timezone: this.timezone,
      groupBy: this.groupBy,
      repositories: this.selectedRepos() ?? [],
      ...(this.ticket ? { ticket: this.ticket } : {}),
    };
  }
  async loadHistory(more = false) {
    const epoch = ++this.historyEpoch;
    this.loading.set(true);
    this.error.set("");
    try {
      const query =
        more && this.historyQuery
          ? {
              ...this.historyQuery,
              snapshot: this.timeline()?.snapshot,
              offset: this.timeline()?.nextOffset ?? 0,
            }
          : this.query();
      const page = await api<Timeline>("timeline", query);
      if (epoch !== this.historyEpoch) return;
      if (more) page.rows = [...(this.timeline()?.rows ?? []), ...page.rows];
      this.historyQuery = { ...query, snapshot: page.snapshot, offset: 0 };
      this.timeline.set(page);
    } catch (error) {
      if (epoch === this.historyEpoch) this.report(error);
    } finally {
      if (epoch === this.historyEpoch) this.loading.set(false);
    }
  }
  private ongoingEpoch = 0;
  async loadOngoing() {
    const epoch = ++this.ongoingEpoch;
    try {
      const now = new Date();
      const query: TimelineQuery = {
        from: new Date(now.getTime() - 14 * 86400000).toISOString(),
        to: now.toISOString(),
        timezone: this.timezone,
        groupBy: "ticket",
        repositories: this.selectedRepos() ?? [],
      };
      let page = await api<Timeline>("timeline", query);
      const rows = [...page.rows];
      query.snapshot = page.snapshot;
      while (page.nextOffset !== null) {
        if (epoch !== this.ongoingEpoch) return;
        page = await api<Timeline>("timeline", {
          ...query,
          offset: page.nextOffset,
        });
        rows.push(...page.rows);
      }
      if (epoch === this.ongoingEpoch) {
        this.ongoing.set(rows);
        this.ongoingQuery = query;
      }
    } catch (error) {
      if (epoch === this.ongoingEpoch) this.report(error);
    }
  }
  async ticketChanged() {
    const value = this.ticketInput.trim();
    this.ticketInput = value;
    if (value === this.ticket) return;
    this.ticket = value;
    await this.controlsChanged();
  }
  async clearTicket() {
    this.ticket = this.ticketInput = "";
    await this.controlsChanged();
  }
  async controlsChanged() {
    this.remember();
    await this.loadHistory();
  }
  /** Computes the effective bounds from the focused day or the remembered range, as of now. */
  private evaluate() {
    const now = this.now();
    this.evaluatedDate = localDate(now, this.timezone);
    const focus = this.focusedDay();
    const effective = focus
      ? { from: focus, to: focus }
      : effectiveRange(this.range, this.timezone, now);
    this.from = this.fromInput = effective.from;
    this.to = this.toInput = effective.to;
    this.rangeError.set("");
  }
  async selectPreset(days: 7 | 30 | 90) {
    this.range = { kind: "relative", days };
    this.focusedDay.set(null);
    this.evaluate();
    this.remember();
    await this.loadHistory();
  }
  async datesChanged() {
    const from = this.fromInput,
      to = this.toInput;
    if (!validDate(from) || !validDate(to)) {
      this.rangeError.set("Enter both a From and a Through date.");
      return;
    }
    if (from > to) {
      this.rangeError.set("From must not be after Through.");
      return;
    }
    this.range = { kind: "custom", from, to };
    this.focusedDay.set(null);
    this.evaluate();
    this.remember();
    await this.loadHistory();
  }
  /** Asks the server for a one-day History slice in `zone`. False only for its 400 "Unknown reporting timezone." rejection. */
  private async serverAcceptsTimezone(zone: string): Promise<boolean> {
    const today = localDate(this.now(), zone);
    try {
      await api<Timeline>("timeline", {
        from: boundary(today, zone),
        to: boundary(addDays(today, 1), zone),
        timezone: zone,
        groupBy: "ticket",
        repositories: this.selectedRepos() ?? [],
      });
      return true;
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.status === 400 &&
        error.message === "Unknown reporting timezone."
      )
        return false;
      throw error;
    }
  }
  async timezoneChanged() {
    const zone = this.timezoneInput.trim();
    if (!validTimezone(zone)) {
      this.timezoneError.set(
        "This browser does not know the timezone \u201c" +
          zone +
          "\u201d. Use a name such as Europe/Warsaw.",
      );
      return;
    }
    if (zone !== this.timezone) {
      let accepted: boolean;
      try {
        accepted = await this.serverAcceptsTimezone(zone);
      } catch (error) {
        this.report(error);
        return;
      }
      if (!accepted) {
        // Nothing changes: the previous zone, results and saved preferences stay in effect.
        this.timezoneError.set(
          "The server does not accept the timezone \u201c" +
            zone +
            "\u201d. Use a name such as Europe/Warsaw.",
        );
        return;
      }
    }
    this.timezoneError.set("");
    this.timezone = this.timezoneInput = zone;
    this.rememberedTimezone = zone;
    this.evaluate();
    this.remember();
    await this.refreshData();
  }
  async focusDay(date: string) {
    this.focusedDay.set(date);
    this.evaluate();
    await this.loadHistory();
  }
  async clearFocus() {
    this.focusedDay.set(null);
    this.evaluate();
    await this.loadHistory();
  }
  async openChange(change: Change, path?: string, anchor?: string) {
    const same =
      this.catalog()?.changes.filter(
        (c) =>
          c.sourceId === change.sourceId &&
          (c.name === change.name || c.directoryName === change.name),
      ) ?? [];
    const key = change.sourceId + ":" + change.name;
    const remembered = this.preferences.documents?.[key];
    const file =
      path ??
      (remembered && change.documents.some((d) => d.path === remembered)
        ? remembered
        : change.documents[0]?.path);
    if (!file) {
      this.notice.set("This change has no readable Markdown documents.");
      return;
    }
    await this.router.navigate(["/" + this.view()], {
      queryParams: {
        source: change.sourceId,
        change: change.name,
        doc: file,
        anchor: anchor ?? null,
        variant: same.length > 1 ? change.variant : null,
      },
    });
  }
  async openDocument(path: string) {
    const d = this.document();
    if (!d) return;
    await this.router.navigate(["/" + this.view()], {
      queryParams: {
        source: d.sourceId,
        change: d.change,
        doc: path,
        variant:
          this.router.parseUrl(this.router.url).queryParams["variant"] ?? null,
      },
    });
  }
  async refreshDocument() {
    const current = this.document();
    if (current) await this.loadDocument(current);
  }
  async closeDocument() {
    this.stashDraft();
    this.documentEpoch++;
    this.documentLoading.set(false);
    this.document.set(null);
    this.selectedChange.set(null);
    this.copies.set([]);
    await this.router.navigate(["/" + this.view()]);
  }
  private async routeChanged() {
    if (this.navigating) return;
    const url = this.router.parseUrl(this.router.url);
    const route = url.root.children["primary"]?.segments[0]?.path;
    this.view.set(route === "history" ? "history" : route === "archive" ? "archive" : "ongoing");
    if (route !== "history") void this.loadActivity();
    const source = url.queryParams["source"] as string | undefined,
      change = url.queryParams["change"] as string | undefined,
      path = url.queryParams["doc"] as string | undefined;
    if (source && change && path) {
      const candidates =
        this.catalog()?.changes.filter(
          (c) =>
            c.sourceId === source &&
            (c.name === change || c.directoryName === change),
        ) ?? [];
      const variant = url.queryParams["variant"];
      const selected = candidates.filter(
        (c) => !variant || c.variant === variant,
      );
      if (selected.length > 1) {
        this.stashDraft();
        this.documentEpoch++;
        this.documentLoading.set(false);
        this.copies.set(selected);
        this.document.set(null);
        return;
      }
      const sourceRepos =
        this.catalog()?.sources.find((s) => s.id === source)?.repositories ??
        [];
      if (!this.inScope(sourceRepos)) {
        this.selectedRepos.set([
          ...new Set([...(this.selectedRepos() ?? []), ...sourceRepos]),
        ]);
        this.notice.set(
          "Repository filter updated to reveal the linked document.",
        );
        void this.refreshData();
      }
      this.copies.set([]);
      this.selectedChange.set(selected[0] ?? null);
      await this.loadDocument(
        { sourceId: source, change, path, variant },
        url.queryParams["anchor"],
      );
    } else {
      this.stashDraft();
      this.documentEpoch++;
      this.documentLoading.set(false);
      this.document.set(null);
      this.copies.set([]);
      this.selectedChange.set(null);
    }
    this.remember();
  }
  private documentKey(d: DocumentRequest) {
    return d.sourceId + ":" + d.change + ":" + d.path + ":" + (d.variant ?? "");
  }
  private stashDraft() {
    const d = this.document();
    if (d)
      this.drafts.set(this.documentKey(d), {
        selection: this.selection(),
        comment: this.comment(),
        editing: this.editingReview(),
      });
  }
  private async loadDocument(
    request: DocumentRequest,
    anchor?: string,
    poll = false,
    skipUnchanged = false,
  ) {
    const old = this.document();
    const same = old && this.documentKey(old) === this.documentKey(request);
    const epoch = ++this.documentEpoch;
    if (!poll) {
      this.stashDraft();
      this.documentLoading.set(true);
      this.commentStatus.set("");
      if (!same) {
        this.selection.set(null);
        this.comment.set("");
        this.editingReview.set(null);
        this.deletingReview.set(null);
      }
    }
    try {
      const body: DocumentRequest = {
        sourceId: request.sourceId,
        change: request.change,
        path: request.path,
        variant: request.variant,
      };
      if (skipUnchanged && same) body.knownRevision = old.revision;
      const answer = await api<DocumentContent | DocumentUnchanged>(
        "document",
        body,
      );
      if (epoch !== this.documentEpoch) return;
      // The open document is current: keep what is rendered and parse nothing.
      if ("unchanged" in answer && answer.unchanged) return;
      const value = answer as DocumentContent;
      if (poll && same && value.revision === old.revision && value.canComment === old.canComment) {
        this.document.set(value);
        return;
      }
      // A different document starts from its own baseline; the baseline is known before the first render so nothing flashes.
      if (!same) {
        await this.loadBaseline(value);
        if (epoch !== this.documentEpoch) return;
      }
      const pane = document.querySelector<HTMLElement>(".document-scroll");
      const scroll = pane?.scrollTop ?? 0;
      const currentHeading = same
        ? this.headings()
            .filter(
              (h) =>
                (document.getElementById(h.id)?.getBoundingClientRect().top ??
                  Infinity) < 250,
            )
            .at(-1)?.id
        : undefined;
      this.document.set(value);
      this.renderDocument(value);
      if (!same) {
        const draft = this.drafts.get(this.documentKey(value));
        this.selection.set(draft?.selection ?? null);
        this.comment.set(draft?.comment ?? "");
        this.editingReview.set(draft?.editing ?? null);
      } else if (poll && this.comment())
        this.commentStatus.set(
          this.editingReview()
            ? "Document updated. Your edit is retained; saving checks the original note."
            : "Document updated. Your comment is retained; reselect its passage before saving.",
        );
      if (!poll) this.useDocument(value);
      setTimeout(() => {
        if (epoch !== this.documentEpoch) return;
        this.attachTaskAnchors();
        this.syncStops();
        if (anchor) this.scrollTo(anchor);
        else if (currentHeading) this.scrollTo(currentHeading, false);
        else {
          const el = document.querySelector<HTMLElement>(".document-scroll");
          if (el)
            el.scrollTop = same
              ? scroll
              : (this.preferences.positions?.[this.documentKey(value)] ?? 0);
        }
        if (!same && !anchor)
          document
            .querySelector<HTMLElement>(".reader")
            ?.focus({ preventScroll: true });
      }, 0);
    } catch (error) {
      if (epoch === this.documentEpoch) {
        this.commentStatus.set(
          error instanceof Error ? error.message : String(error),
        );
        if (!poll) this.report(error);
      }
    } finally {
      if (epoch === this.documentEpoch) this.documentLoading.set(false);
    }
  }
  private renderDocument(value: DocumentContent) {
    const marks = this.marksFor(value);
    const rendered = renderMarkdown(value.text, value.canComment, marks);
    this.readMarks.set(marks);
    this.html.set(this.sanitizer.bypassSecurityTrustHtml(rendered.html));
    this.headings.set(rendered.headings);
    this.reviews.set(rendered.reviews);
    const baseline = this.baseline();
    this.removedComments.set(marks && baseline ? removedReviews(baseline.text, rendered.reviews) : []);
  }
  /** Differences between the baseline and `value`, worked out once per pair of revisions. */
  private marksFor(value: DocumentContent): ReadMarks | null {
    const baseline = this.baseline();
    if (!baseline || baseline.revision === value.revision) return null;
    const key = baseline.revision + ">" + value.revision;
    if (this.marksMemo?.key !== key)
      this.marksMemo = { key, marks: readMarks(baseline.text, value.text) };
    return this.marksMemo.marks;
  }
  private readRequest(d: DocumentRequest): DocumentRequest {
    return { sourceId: d.sourceId, change: d.change, path: d.path, variant: d.variant };
  }
  private logicalName(d: DocumentRequest) {
    return d.change === "@source"
      ? d.change
      : (this.catalog()?.changes.find(
          (c) => c.sourceId === d.sourceId && (c.name === d.change || c.directoryName === d.change),
        )?.name ?? d.change);
  }
  /** Records the fingerprint of a document just marked read, so its card stops showing it as updated. */
  private noteRead(d: DocumentRequest, result: ReadMarkResult) {
    const key = baselineKey(d.sourceId, this.logicalName(d), d.path);
    this.readSummary.update((summary) => ({ ...summary, [key]: result.fingerprint }));
  }
  /**
   * Loads the remembered content of a document that has just been opened. A document never read becomes its own baseline,
   * and one whose card said "updated" but whose content is unchanged is marked again so the false hint clears.
   * A failure only means no highlights are shown.
   */
  private async loadBaseline(value: DocumentContent) {
    this.baseline.set(null);
    this.readMarks.set(null);
    this.removedComments.set([]);
    try {
      const document = this.readRequest(value);
      let baseline = await api<ReadBaseline | null>("reads/get", { document });
      let marked: ReadMarkResult | undefined;
      if (!baseline) {
        marked = await api<ReadMarkResult>("reads/mark", { document, revision: value.revision });
        baseline = { revision: value.revision, text: value.text };
      } else if (baseline.revision === value.revision && this.looksUpdated(value)) {
        marked = await api<ReadMarkResult>("reads/mark", { document, revision: value.revision });
      }
      if (marked) this.noteRead(value, marked);
      this.baseline.set(baseline);
    } catch {
      // The document is still readable; it just has nothing to compare with.
    }
  }
  private looksUpdated(d: DocumentRequest) {
    const change = this.catalog()?.changes.find(
      (c) =>
        c.sourceId === d.sourceId &&
        (c.name === d.change || c.directoryName === d.change) &&
        (!d.variant || c.variant === d.variant),
    );
    return !!change && updatedDocuments(change, this.readSummary()).includes(d.path);
  }
  /** Makes the current content the baseline, which clears the highlights and the updated hint. */
  async markRead() {
    const d = this.document();
    if (!d || this.documentLoading()) return;
    try {
      const result = await api<ReadMarkResult>("reads/mark", {
        document: this.readRequest(d),
        revision: d.revision,
      });
      this.noteRead(d, result);
      if (this.document()?.revision === d.revision) {
        this.baseline.set({ revision: d.revision, text: d.text });
        this.renderDocument(d);
        setTimeout(() => this.syncStops(), 0);
      }
    } catch (error) {
      this.notice.set(
        error instanceof ApiError && error.status === 409
          ? "The document changed while you were reading. It has been reloaded; review the changes, then mark it read."
          : "Could not mark the document as read. " + (error instanceof Error ? error.message : String(error)),
      );
      if (error instanceof ApiError && error.status === 409) void this.loadDocument(d, undefined, true);
    }
  }
  /** Marks every document of the open change read, as each stands now. */
  async markChangeRead() {
    const change = this.selectedChange();
    if (!change) return;
    let failed = 0;
    for (const item of change.documents) {
      try {
        const request: DocumentRequest = {
          sourceId: change.sourceId,
          change: change.name,
          path: item.path,
          variant: change.variant,
        };
        const content = await api<DocumentContent>("document", request);
        const result = await api<ReadMarkResult>("reads/mark", { document: request, revision: content.revision });
        this.noteRead(request, result);
        const open = this.document();
        if (open && open.path === item.path && open.revision === content.revision) {
          this.baseline.set({ revision: content.revision, text: content.text });
          this.renderDocument(open);
          setTimeout(() => this.syncStops(), 0);
        }
      } catch {
        failed++;
      }
    }
    this.notice.set(
      failed
        ? failed + (failed === 1 ? " document" : " documents") + " changed while being marked and still show changes."
        : "Every document of this change is marked as read.",
    );
  }
  /** After the user's own comment write, the new revision becomes the baseline if the user had read everything before it. */
  private async advanceBaseline(d: DocumentRequest, written: DocumentContent, epoch: number) {
    try {
      const result = await api<ReadMarkResult>("reads/mark", {
        document: this.readRequest(d),
        revision: written.revision,
      });
      this.noteRead(d, result);
      if (epoch === this.documentEpoch) this.baseline.set({ revision: written.revision, text: written.text });
    } catch {
      // The baseline stays, so the write shows as a change rather than being hidden.
    }
  }
  private stops() {
    const root = document.querySelector(".document-scroll");
    return root ? changeStops(root) : [];
  }
  private syncStops() {
    this.changeCursor = -1;
    this.changePosition.set(0);
    this.changeCount.set(this.readMarks() ? this.stops().length : 0);
  }
  changeStatus(marks: ReadMarks) {
    if (marks.incomplete) return "Too many differences to mark individually.";
    const count = this.changeCount();
    return this.changePosition()
      ? "Change " + this.changePosition() + " of " + count
      : count + (count === 1 ? " change" : " changes");
  }
  setSourceView(value: boolean) {
    this.sourceView.set(value);
    setTimeout(() => this.syncStops(), 0);
  }
  /** Moves to the next (1) or previous (-1) change, wrapping at the ends. */
  navigateChange(direction: 1 | -1) {
    const stops = this.stops();
    if (!stops.length) return;
    this.changeCursor =
      direction === 1
        ? (this.changeCursor + 1) % stops.length
        : this.changeCursor <= 0
          ? stops.length - 1
          : this.changeCursor - 1;
    this.changeCount.set(stops.length);
    this.changePosition.set(this.changeCursor + 1);
    const target = stops[this.changeCursor];
    target.scrollIntoView({ block: "center" });
    (target.querySelector<HTMLElement>("summary") ?? target).focus({ preventScroll: true });
  }
  private useDocument(value: DocumentContent) {
    this.preferences.documents ??= {};
    touchRecent(
      this.preferences.documents,
      value.sourceId + ":" + value.change,
      value.path,
      documentLimit,
    );
    const key = this.documentKey(value);
    const position = this.preferences.positions?.[key];
    if (position !== undefined)
      touchRecent(this.preferences.positions!, key, position, positionLimit);
  }
  taskAnchor(
    task: { number: string | null; text: string; line: number },
    index: number,
  ) {
    return task.number
      ? "task-" + task.number
      : "task-" +
          task.text.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, "-") +
          "-" +
          index;
  }
  private attachTaskAnchors() {
    const d = this.document();
    if (!d) return;
    for (const [i, task] of d.tasks.entries()) {
      const node = document.querySelector<HTMLElement>(
        '.markdown [data-start="' + task.line + '"]',
      );
      if (node) node.id = this.taskAnchor(task, i);
    }
  }
  scrollTo(anchor: string, report = true) {
    if (this.sourceView()) {
      this.setSourceView(false);
      setTimeout(() => this.scrollTo(anchor, report), 0);
      return;
    }
    this.attachTaskAnchors();
    let target: HTMLElement | null = null;
    // "note:" cannot be produced by a heading slug, which keeps only letters, digits, "-" and "_".
    if (anchor.startsWith("note:")) {
      target = document.querySelector<HTMLElement>(
        '.markdown [data-review-line="' + Number(anchor.slice(5)) + '"]',
      );
    } else if (anchor.startsWith("line-")) {
      const line = Number(anchor.slice(5));
      const blocks = Array.from(
        document.querySelectorAll<HTMLElement>(".markdown [data-start]"),
      );
      target =
        blocks
          .filter(
            (e) => +e.dataset["start"]! <= line && +e.dataset["end"]! > line,
          )
          .at(-1) ?? null;
    } else {
      const nodes = Array.from(
        document.querySelectorAll<HTMLElement>(".markdown [id]"),
      ).filter((n) => n.id === anchor);
      if (nodes.length === 1) target = nodes[0];
    }
    if (target) {
      target.scrollIntoView({ block: "start" });
      target.focus({ preventScroll: true });
    } else if (report)
      this.notice.set(
        "That section or task is unavailable or ambiguous. The current document is open.",
      );
  }
  selectBlock(event: Event) {
    const element = (event.target as HTMLElement).closest<HTMLElement>(
      "[data-start]",
    );
    if (!element || !this.document()?.canComment) return;
    if (this.saving() || this.documentLoading()) return;
    const start = Number(element.dataset["start"]),
      end = Number(element.dataset["end"]);
    const text = this.document()!
      .text.replace(/\r\n|\r/g, "\n")
      .split("\n")
      .slice(start, end)
      .join("\n");
    this.selection.set({ start, end, text });
    this.editingReview.set(null);
    this.deletingReview.set(null);
    this.commentStatus.set("");
  }
  readerKey(event: KeyboardEvent) {
    if (
      event.key === "Enter" &&
      !(event.target as HTMLElement).closest("a,button,input")
    ) {
      this.selectBlock(event);
      event.preventDefault();
    }
  }
  readerClick(event: MouseEvent) {
    const note = (event.target as HTMLElement).closest<HTMLElement>("[data-review-line]");
    if (note) {
      const review = this.reviews().find(r => r.line === Number(note.dataset["reviewLine"]));
      if (!review) return;
      const action = (event.target as HTMLElement).closest<HTMLElement>("[data-review-action]")?.dataset["reviewAction"];
      if (action === "delete") this.requestDeleteReview(review);
      else this.editReview(review);
      return;
    }
    const link = (event.target as HTMLElement).closest<HTMLAnchorElement>("a");
    if (link) {
      event.preventDefault();
      void this.followLink(link.getAttribute("href") ?? "");
      return;
    }
    this.selectBlock(event);
  }
  /** The one explicit external navigation: opens an http(s) address in a new tab without handing over the opener. */
  openExternal(event: Event, url: string) {
    event.preventDefault();
    window.open(url, "_blank", "noopener,noreferrer");
  }
  private async followLink(target: string) {
    if (/^https?:\/\//i.test(target)) {
      window.open(target, "_blank", "noopener,noreferrer");
      return;
    }
    if (/^[a-z][a-z\d+.-]*:/i.test(target) || target.startsWith("//")) {
      this.notice.set("This link type is unavailable.");
      return;
    }
    const d = this.document();
    if (!d) return;
    try {
      const value = await api<{
        sourceId: string;
        change: string;
        path: string;
        anchor: string | null;
      }>("link", { document: d, target });
      await this.router.navigate(["/" + this.view()], {
        queryParams: {
          source: value.sourceId,
          change: value.change,
          doc: value.path,
          anchor: value.anchor,
        },
      });
    } catch (error) {
      this.report(error);
    }
  }
  async saveComment() {
    const d = this.document(),
      block = this.selection(), edit = this.editingReview();
    if (!d?.canComment || (!block && !edit) || !this.comment().trim() || this.saving()) return;
    const epoch = this.documentEpoch;
    // Whether the user had read everything in the document before this write: only then does the write move the baseline.
    const read = this.baseline()?.revision === d.revision;
    this.saving.set(true);
    this.commentStatus.set("");
    try {
      const written = await api<DocumentContent>(edit ? "comments/edit" : "comments", edit ? {
        ...d, revision: edit.revision, line: edit.line, markerText: edit.marker,
        id: edit.id, comment: this.comment(),
      } : {
        ...d,
        revision: d.revision,
        startLine: block!.start,
        endLine: block!.end,
        blockText: block!.text,
        comment: this.comment(),
      });
      if (read) await this.advanceBaseline(d, written, epoch);
      void this.loadReviews();
      this.drafts.delete(this.documentKey(d));
      if (epoch !== this.documentEpoch) return;
      this.comment.set("");
      this.selection.set(null);
      this.editingReview.set(null);
      await this.loadDocument(d, undefined, true);
      this.commentStatus.set("Review comment saved.");
    } catch (error) {
      if (epoch !== this.documentEpoch) return;
      this.commentStatus.set(
        error instanceof Error ? error.message : String(error),
      );
      if (error instanceof ApiError && error.status === 409)
        await this.loadDocument(d, undefined, true);
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
    }
  }
  editReview(review: Review) {
    const d = this.document();
    if (!d?.canComment || this.saving() || this.documentLoading()) return;
    this.editingReview.set({ ...review, revision: d.revision });
    this.deletingReview.set(null);
    this.selection.set(null);
    this.comment.set(review.text);
    this.commentStatus.set("");
    setTimeout(() => document.querySelector<HTMLTextAreaElement>('.comment-entry textarea')?.focus(), 0);
  }
  cancelReviewEdit() {
    this.editingReview.set(null);
    this.selection.set(null);
    this.comment.set("");
    this.commentStatus.set("");
    const d = this.document();
    if (d) this.drafts.delete(this.documentKey(d));
  }
  requestDeleteReview(review: Review) {
    const d = this.document();
    if (!d?.canComment || this.saving() || this.documentLoading()) return;
    this.deletingReview.set({ ...review, revision: d.revision });
    this.commentStatus.set("");
    setTimeout(() => document.querySelector<HTMLButtonElement>('.review-delete-confirm button')?.focus(), 0);
  }
  async deleteReview() {
    const d = this.document(), review = this.deletingReview();
    if (!d?.canComment || !review || this.saving()) return;
    const epoch = this.documentEpoch;
    const read = this.baseline()?.revision === d.revision;
    this.saving.set(true);
    try {
      const written = await api<DocumentContent>("comments/delete", {
        ...d, revision: review.revision, line: review.line,
        markerText: review.marker, id: review.id,
      });
      if (read) await this.advanceBaseline(d, written, epoch);
      void this.loadReviews();
      if (epoch !== this.documentEpoch) return;
      this.deletingReview.set(null);
      if (this.editingReview()?.marker === review.marker) this.cancelReviewEdit();
      await this.loadDocument(d, undefined, true);
      this.commentStatus.set("Review comment deleted.");
    } catch (error) {
      if (epoch !== this.documentEpoch) return;
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
      if (error instanceof ApiError && error.status === 409) await this.loadDocument(d, undefined, true);
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
    }
  }
  async copyLink(anchor?: string) {
    const d = this.document();
    if (!d) return;
    const url = new URL("/" + this.view(), window.location.origin);
    url.searchParams.set("source", d.sourceId);
    url.searchParams.set("change", d.change);
    url.searchParams.set("doc", d.path);
    const variant = this.router.parseUrl(this.router.url).queryParams[
      "variant"
    ];
    if (variant) url.searchParams.set("variant", variant);
    if (anchor) url.searchParams.set("anchor", anchor);
    try {
      await navigator.clipboard.writeText(url.toString());
      this.notice.set("Link copied.");
    } catch {
      this.copyFallback.set(url.toString());
    }
  }
  copyFallback = signal("");
  onScroll(event: Event) {
    const d = this.document();
    if (!d) return;
    this.rememberPosition(
      this.documentKey(d),
      (event.target as HTMLElement).scrollTop,
    );
    this.remember(scrollSaveDelay);
  }
  rememberPosition(key: string, top: number) {
    this.preferences.positions ??= {};
    touchRecent(this.preferences.positions, key, top, positionLimit);
  }
  searchChanged() {
    clearTimeout(this.searchTimer);
    if (!this.searchText.trim()) {
      this.searchEpoch++;
      this.searchResults.set(null);
      return;
    }
    this.searchTimer = setTimeout(() => void this.search(), 300);
  }
  async search() {
    const query = this.searchText.trim();
    if (!query) return;
    const epoch = ++this.searchEpoch;
    this.searchLoading.set(true);
    this.searchRevision = this.catalog()?.revision ?? null;
    try {
      const value = await api<SearchResults>("search", {
        query,
        repositories: this.selectedRepos() ?? [],
        scope: this.searchScope,
      });
      if (epoch === this.searchEpoch) this.searchResults.set(value);
    } catch (error) {
      if (epoch === this.searchEpoch) this.report(error);
    } finally {
      if (epoch === this.searchEpoch) this.searchLoading.set(false);
    }
  }
  async openMatch(match: Match) {
    const change = this.catalog()?.changes.find(
      (c) => c.sourceId === match.sourceId && c.variant === match.variant,
    );
    if (change) await this.openChange(change, match.path, "line-" + match.line);
  }
  async openItem(item: Row | Insight, day?: string, ongoing = false) {
    const epoch = ++this.detailEpoch;
    const row =
      "id" in item
        ? item
        : {
            id: item.itemId,
            title: item.title,
            repository: item.repository,
            tickets: item.tickets ?? [],
          };
    this.detail.set(row);
    this.detailDay.set(day ?? null);
    this.observations.set([]);
    this.observationNext.set(null);
    this.detailLoading.set(true);
    this.detailQuery = ongoing ? this.ongoingQuery : this.historyQuery;
    try {
      if (this.detailQuery) {
        const page = await api<ObservationPage>("observations", {
          query: this.detailQuery,
          id: row.id,
          day: day ?? null,
        });
        if (epoch !== this.detailEpoch) return;
        this.observations.set(page.entries);
        this.observationNext.set(page.nextOffset);
        this.observationTotal.set(page.total);
      }
    } catch (error) {
      this.report(error);
    } finally {
      if (epoch === this.detailEpoch) this.detailLoading.set(false);
    }
  }
  async moreObservations() {
    const d = this.detail();
    if (!d || !this.detailQuery || this.observationNext() === null) return;
    const epoch = this.detailEpoch;
    this.detailLoading.set(true);
    try {
      const page = await api<ObservationPage>("observations", {
        query: this.detailQuery,
        id: d.id,
        offset: this.observationNext(),
        day: this.detailDay(),
      });
      if (epoch !== this.detailEpoch) return;
      this.observations.update((e) => [...e, ...page.entries]);
      this.observationNext.set(page.nextOffset);
    } catch (error) {
      this.report(error);
    } finally {
      if (epoch === this.detailEpoch) this.detailLoading.set(false);
    }
  }
  async showAllObservations() {
    const d = this.detail();
    if (!d) return;
    await this.openItem({
      itemId: d.id,
      title: d.title,
      repository: d.repository,
      at: "",
      tickets: d.tickets,
    });
  }
  private showToast(text: string) {
    this.copyToast.set(text);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.copyToast.set(""), 3000);
  }
  private async copyText(text: string, title: string, done: string, multiline = false) {
    try {
      await navigator.clipboard.writeText(text);
      this.manualCopy.set(null);
      this.showToast(done);
    } catch {
      this.manualCopy.set({ title, text, multiline });
    }
  }
  copyTicket(id: string) {
    return this.copyText(id, "Copy this ticket ID", "Ticket ID copied.");
  }
  /** Copies the open work item as Markdown, loading further observation pages up to the bounded maximum first. */
  async copyMarkdown() {
    const d = this.detail();
    if (!d || !this.detailQuery || this.markdownBusy()) return;
    const epoch = this.detailEpoch;
    this.markdownBusy.set(true);
    try {
      let entries = [...this.observations()];
      let next = this.observationNext();
      while (next !== null && entries.length < markdownObservationLimit) {
        const page = await api<ObservationPage>("observations", {
          query: this.detailQuery,
          id: d.id,
          offset: next,
          day: this.detailDay(),
        });
        if (epoch !== this.detailEpoch) return;
        entries = entries.concat(page.entries);
        next = page.nextOffset;
      }
      const text = workItemMarkdown({
        title: d.title,
        repository: d.repository,
        repositoryLabel: this.repositoryLabel(d.repository),
        tickets: d.tickets ?? [],
        changes: d.changes ?? [],
        firstAt: d.firstAt,
        lastAt: d.lastAt,
        status: d.status,
        day: this.detailDay(),
        entries,
        total: this.observationTotal(),
        timezone: this.timezone,
        formatDate: (v) => this.formatDate(v),
        formatDateTime: (v) => this.formatDate(v) + " " + this.formatTime(v),
        statusLabel: (v) => this.statusLabel(v),
      });
      await this.copyText(text, "Copy this Markdown", "Markdown copied.", true);
    } catch (error) {
      if (epoch === this.detailEpoch) this.report(error);
    } finally {
      this.markdownBusy.set(false);
    }
  }
  associatedChanges() {
    const names = new Set([
      ...(this.detail()?.changes ?? []),
      ...this.observations()
        .map((e) => e.change)
        .filter((x): x is string => x !== null),
    ]);
    return this.filteredChanges().filter(
      (c) =>
        names.has(c.name) &&
        c.repositories.includes(this.detail()?.repository ?? ""),
    );
  }
  private remember(delay = 350) {
    if (!this.ready()) return;
    clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => void this.persist(), delay);
  }
  /** Saves preferences when they differ from the last successful save. The timezone is saved only once the user chose it. */
  private async persist() {
    if (!this.ready()) return;
    const history: NonNullable<Preferences["history"]> = {
      range: this.range,
      groupBy: this.groupBy,
      scale: this.scale,
    };
    if (this.ticket) history.ticket = this.ticket;
    if (this.rememberedTimezone) history.timezone = this.rememberedTimezone;
    this.preferences = {
      ...this.preferences,
      schemaVersion: 1,
      repositories: this.selectedRepos(),
      history,
      lastRoute: this.router.url,
    };
    const body = JSON.stringify(this.preferences);
    if (body === this.lastSaved) return;
    try {
      await api("state", this.preferences, "PUT");
      this.lastSaved = body;
    } catch (error) {
      this.preferencesFailed(error);
    }
  }
  private preferencesFailed(error: unknown) {
    if (error instanceof ApiError && error.status === 401) {
      this.report(error);
      return;
    }
    // One notice per visit; the in-memory preferences stay in effect.
    if (this.preferencesNoticeShown) return;
    this.preferencesNoticeShown = true;
    this.preferencesNotice.set(
      "Preferences could not be saved" +
        (error instanceof Error && error.message
          ? " (" + error.message + ")"
          : "") +
        ". Your current view is unaffected.",
    );
  }
  private dayRolledOver() {
    return localDate(this.now(), this.timezone) !== this.evaluatedDate;
  }
  private async poll() {
    if (
      document.hidden ||
      this.polling ||
      this.documentLoading() ||
      this.saving()
    )
      return;
    this.polling = true;
    const documentEpoch = this.documentEpoch;
    try {
      const snapshot = await api<Catalog>("catalog");
      this.catalog.set(snapshot);
      // The overview and the read hints follow document contents, so they refresh when the catalog revision moves.
      if (snapshot.revision !== this.overviewRevision) {
        this.overviewRevision = snapshot.revision;
        void this.refreshOverview();
      }
      // Navigation or review may start while catalog discovery is in flight.
      // A background refresh must never supersede that foreground request.
      if (
        documentEpoch !== this.documentEpoch ||
        this.documentLoading() ||
        this.saving()
      ) return;
      const d = this.document();
      if (d) {
        const candidates = snapshot.changes.filter(
          (c) =>
            c.sourceId === d.sourceId &&
            (c.name === d.change || c.directoryName === d.change) &&
            (!d.variant || c.variant === d.variant),
        );
        this.selectedChange.set(candidates.length === 1 ? candidates[0] : null);
        // An archive move changes the document's state without changing its text, so it needs the full response.
        const moved = candidates.length === 1 && candidates[0].archived !== d.archived;
        await this.loadDocument(d, undefined, true, !moved);
      }
      if (this.searchText.trim() && snapshot.revision !== this.searchRevision)
        void this.search();
      const rolled = this.dayRolledOver();
      if (rolled || snapshot.journalSnapshot !== this.journalSnapshot) {
        this.journalSnapshot = snapshot.journalSnapshot;
        if (rolled) this.evaluate();
        void this.loadActivity();
        await this.refreshData();
      }
    } catch (error) {
      this.report(error);
    } finally {
      this.polling = false;
    }
  }
  @HostListener("document:keydown", ["$event"])
  keyboard(event: KeyboardEvent) {
    if (
      (event.key === "[" || event.key === "]") &&
      this.document() &&
      !event.ctrlKey &&
      !event.altKey &&
      !event.metaKey &&
      !(event.target instanceof Element && event.target.closest("input,textarea,select,[contenteditable]"))
    ) {
      this.navigateChange(event.key === "]" ? 1 : -1);
      event.preventDefault();
      return;
    }
    if (event.key === "Escape") {
      const target = event.target instanceof Element ? event.target : null;
      const field = target?.closest("textarea,input,select,.review-delete-confirm");
      if (field && (this.document() || this.copies().length || this.detail())) {
        // Escape leaves the field or the confirmation first; it closes the reader only from outside them.
        if (event.isComposing) return;
        if (this.deletingReview()) this.deletingReview.set(null);
        else if (this.selection() && !this.comment().trim() && !this.editingReview())
          this.selection.set(null);
        if (field instanceof HTMLElement && field.matches("textarea,input,select")) field.blur();
        document
          .querySelector<HTMLElement>(".reader")
          ?.focus({ preventScroll: true });
        event.preventDefault();
        return;
      }
      if (this.detail()) {
        this.detail.set(null);
        return;
      }
      if (this.document() || this.copies().length) {
        void this.closeDocument();
        return;
      }
      this.repoMenu.set(false);
    }
    if (event.key !== "Tab") return;
    const modal =
      document.querySelector<HTMLElement>(".activity-detail") ??
      document.querySelector<HTMLElement>(".reader");
    if (!modal) return;
    const nodes = Array.from(
      modal.querySelectorAll<HTMLElement>(
        'button:not([disabled]),a[href],input:not([disabled]),textarea,select,[tabindex="0"]',
      ),
    ).filter((e) => e.getClientRects().length > 0);
    const first = nodes[0],
      last = nodes.at(-1);
    if (!first || !last) return;
    if (
      event.shiftKey &&
      (document.activeElement === first || document.activeElement === modal)
    ) {
      last.focus();
      event.preventDefault();
    } else if (
      !event.shiftKey &&
      (document.activeElement === last || document.activeElement === modal)
    ) {
      first.focus();
      event.preventDefault();
    }
  }
  rangeDays() {
    return Math.max(
      1,
      Math.round(
        (Date.parse(addDays(this.to, 1)) - Date.parse(this.from)) / 86400000,
      ),
    );
  }
  timelineWidth() {
    return Math.max(
      640,
      this.rangeDays() *
        (this.scale === "day" ? 28 : this.scale === "week" ? 12 : 5),
    );
  }
  ticks() {
    const step =
      this.scale === "day"
        ? Math.max(1, Math.ceil(this.rangeDays() / 45))
        : this.scale === "week"
          ? 7
          : 30;
    const values = [];
    for (let i = 0; i < this.rangeDays(); i += step) {
      const date = addDays(this.from, i);
      values.push({
        date,
        left: (i / this.rangeDays()) * 100,
        label: new Intl.DateTimeFormat("en-GB", {
          day: "numeric",
          month: "short",
          timeZone: "UTC",
        }).format(new Date(date + "T12:00:00Z")),
      });
    }
    return values;
  }
  position(date: string) {
    const start = Date.parse(this.from + "T00:00:00Z");
    return Math.max(
      0,
      Math.min(
        100,
        ((Date.parse(date.slice(0, 10) + "T12:00:00Z") - start) /
          (this.rangeDays() * 86400000)) *
          100,
      ),
    );
  }
  spanLeft(row: Row) {
    return Math.max(
      0,
      (((Date.parse(this.day(row.firstAt) + "T00:00:00Z") -
        Date.parse(this.from + "T00:00:00Z")) /
        86400000 +
        0.5) /
        this.rangeDays()) *
        100,
    );
  }
  spanWidth(row: Row) {
    return Math.max(
      0.2,
      this.position(this.day(row.lastAt)) - this.spanLeft(row),
    );
  }
}
