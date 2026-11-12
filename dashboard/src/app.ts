import {
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  OnDestroy,
  AfterViewInit,
  HostListener,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { DomSanitizer, SafeHtml } from "@angular/platform-browser";
import { NavigationEnd, Router, RouterOutlet } from "@angular/router";
import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import {
  api,
  ApiError,
  addDays,
  boundary,
  elapsed,
  humanize,
  localDate,
} from "./api";
import { LiveConnectionController, LiveConnectionStatus } from "./live-connection";
import { LiveDomain, RefreshQueue, RevisionSnapshot, RevisionTracker, revisionToken } from "./live-updates";
import { renderMarkdown, Heading, Review } from "./markdown";
import { activityFor, archiveEntries, editorLabel, editorPresets, firstVisibleLine, sourceLineAfter, validEditor } from "./archive";
import { branchUrl, repositoryUrl, ticketTitle, ticketUrl } from "./links";
import { quietDays } from "./quiet";
import {
  Board,
  BoardCard,
  MoveAction,
  Stage,
  boardWindowDays,
  buildBoard,
  changeKey as boardChangeKey,
  dropWithin,
  isQuiet,
  filterBoard,
  fitOrder,
  moveWithin,
  pruneOrder,
  rankColumn,
  stages,
} from "./board";
import { DailyFormat, dailyText, dailyToday, dayLabel, digestFor, digestIndex, noSummary } from "./daily";
import { markdownObservationLimit, workItemMarkdown } from "./workitem-markdown";
import { changeStops, readMarks, ReadMarks, sourceSegments } from "./diff";
import { repositoryColorKey } from "./repository-colors";
import { groupDocuments } from "./document-navigation";
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
  DailyDigest,
  DailyItem,
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
  UnlinkedPage,
} from "./models";

export type ViewName = "ongoing" | "history" | "archive" | "daily";

/** Most recently used reading positions and remembered document selections kept in preferences. */
/** A Reconnecting state shorter than this is a routine reconciliation and does not raise the page-level status bar. */
export const reconnectingGraceMs = 2_000;
export const positionLimit = 100;
export const documentLimit = 200;
/** 60 KiB, not the server's 64 KiB: the server counts `&`, `+`, `<`, `>`, `'` and non-ASCII characters as 6-byte \uXXXX escapes. */
export const preferencesByteLimit = 61440;
const scrollSaveDelay = 1000;

type ReaderFocus = { id?: string; start?: string; reviewLine?: string };

function captureReaderFocus(pane: HTMLElement | null): ReaderFocus | null {
  const active = document.activeElement;
  if (!pane || !(active instanceof HTMLElement) || !pane.contains(active)) return null;
  const review = active.closest<HTMLElement>("[data-review-line]");
  if (review?.dataset["reviewLine"]) return { reviewLine: review.dataset["reviewLine"] };
  const block = active.closest<HTMLElement>("[data-start]");
  if (block?.dataset["start"]) return { start: block.dataset["start"] };
  return active.id ? { id: active.id } : null;
}

function restoreReaderFocus(pane: HTMLElement | null, focus: ReaderFocus | null) {
  if (!pane || !focus) return;
  const target = focus.id
    ? Array.from(pane.querySelectorAll<HTMLElement>("[id]")).find((node) => node.id === focus.id) ?? null
    : focus.reviewLine
      ? Array.from(pane.querySelectorAll<HTMLElement>("[data-review-line]")).find((node) => node.dataset["reviewLine"] === focus.reviewLine) ?? null
      : Array.from(pane.querySelectorAll<HTMLElement>("[data-start]")).find((node) => node.dataset["start"] === focus.start) ?? null;
  target?.focus({ preventScroll: true });
}

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
  view = signal<ViewName>("ongoing");
  ready = signal(false);
  /** The last status that was not Reconnecting; written synchronously with every status change so no steady state is missed. */
  private readonly steadyStatus = signal<LiveConnectionStatus>("connecting");
  private readonly liveStatusState = signal<LiveConnectionStatus>("connecting");
  readonly liveStatus = this.liveStatusState.asReadonly();
  /** The only writer of the status: it records the last steady (not Reconnecting) state synchronously. */
  setLiveStatus(value: LiveConnectionStatus) {
    if (value !== "reconnecting") this.steadyStatus.set(value);
    this.liveStatusState.set(value);
  }
  /** Set when revision work was postponed because the tab is hidden; that is not a failure. */
  private deferredWhileHidden = false;
  liveRetrying = signal(false);
  /** True once a Reconnecting state has lasted longer than the grace period; routine reconciliations never reach it. */
  private reconnectingLong = signal(false);
  /** The text of the polite status region: only real state changes are announced, never a routine reconciliation. */
  liveAnnouncement = signal("");
  private liveAnnounced = false;
  /** The page-level bar is for Updates paused and for Reconnecting that outlasts the grace period; Live uses the header indicator only. */
  readonly liveBarVisible = computed(() => {
    const status = this.presentedLiveStatus();
    return status === "paused" || status === "reconnecting";
  });
  lastSuccessfulRefresh = signal<string | null>(null);
  unauthorized = signal(false);
  startupRetrying = signal(false);
  error = signal("");
  notice = signal("");
  loading = signal(false);
  documentLoading = signal(false);
  theme = signal<"dark" | "light">("dark");
  searchLoading = signal(false);
  timeline = signal<Timeline | null>(null);
  ongoing = signal<Row[]>([]);
  /** Observations of the last 7 days with neither a ticket nor a change, and whether more exist than were returned. */
  unlinked = signal<Entry[]>([]);
  unlinkedTruncated = signal(false);
  /** The Daily view: the digest as last loaded and the start of today it was requested for. */
  dailyData = signal<{ digest: DailyDigest; before: string } | null>(null);
  dailyLoading = signal(false);
  /** The Daily view's copy format and link choice, as remembered. */
  dailyFormat = signal<DailyFormat>("text");
  dailyLinks = signal(false);
  /** The selected day of the Daily view, from the URL; never remembered. */
  dailyDay: string | null = null;
  private dailyEpoch = 0;
  private dataLoaded = false;
  readonly dayLabel = dayLabel;
  /** The board filter as typed. It is never remembered. */
  boardFilter = signal("");
  /** The saved priority order of board cards, by card key. */
  boardOrder = signal<string[]>([]);
  /** A spoken description of the last card move, for the board's live region. */
  boardAnnouncement = signal("");
  /** The card being dragged and the card edge it would land next to. */
  dragged = signal<{ key: string; stage: Stage } | null>(null);
  dropMark = signal<{ key: string; after: boolean } | null>(null);
  readonly boardStages: { stage: Stage; label: string; empty: string }[] = [
    { stage: "proposed", label: "Proposed", empty: "Nothing proposed" },
    { stage: "in_progress", label: "In progress", empty: "Nothing in progress" },
    { stage: "done", label: "Done", empty: "Nothing done yet" },
  ];
  readonly boardWindowDays = boardWindowDays;
  historyQuery: TimelineQuery | null = null;
  ongoingQuery: TimelineQuery | null = null;
  selectedChange = signal<Change | null>(null);
  documentFilter = signal("");
  mobileDocumentNavigationOpen = signal(false);
  copies = signal<Change[]>([]);
  /** The document the user asked for when a change card opened the copy chooser. */
  copyPath: string | undefined;
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
  /** The repository selection (as JSON) the loaded Ongoing rows and unlinked entries cover; null until the first load. */
  private ongoingScope = signal<string | null>(null);
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
  private digestRevision: number | undefined;
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
  readonly authorizationLostMessage = "Authorization was lost; run `worklog dashboard` again to authorize this browser.";
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
  private clockTimer?: ReturnType<typeof setTimeout>;
  private clockRevision = signal(0);
  private liveConnection?: LiveConnectionController;
  private readonly revisionTracker = new RevisionTracker();
  private readonly refreshQueue = new RefreshQueue();
  private revisionWork: Promise<boolean> | null = null;
  private pendingRevision: { snapshot: RevisionSnapshot; force: boolean } | null = null;
  private forceNextSnapshot = false;
  private selectionEpoch = 0;
  private foregroundWaiters: (() => void)[] = [];
  private destroyed = false;
  private supersessionFollowupEpoch = -1;
  private authorizationEpoch = 0;
  private readerScrollEpoch = 0;
  private startupAttempt?: Promise<void>;
  private autoStartLive = false;
  private catalogEpoch = 0;
  private pendingConnectToken: string | null = null;
  private documentFilterOwner = "";
  private saveTimer?: ReturnType<typeof setTimeout>;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private historyEpoch = 0;
  private searchEpoch = 0;
  private documentEpoch = 0;
  /** Counts renders made by loadDocument only, so a load that renders nothing cannot cancel the pending post-render work of an earlier render. */
  private renderEpoch = 0;
  private detailEpoch = 0;
  private reviewEpoch = 0;
  private summaryEpoch = 0;
  private changeCursor = -1;
  private marksMemo: { key: string; marks: ReadMarks | null } | null = null;
  private removedReviewsMemo: { key: string; reviews: Review[] } | null = null;
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
  /** Every card in priority order, before the filter. Reordering works on this. */
  readonly fullBoard = computed<Board>(() =>
    (this.clockRevision(), buildBoard(
      {
        changes: this.filteredChanges(),
        activity: this.activityReady() ? this.activity() : null,
        rows: this.ongoing(),
        unlinked: this.unlinked(),
        order: this.boardOrder(),
      },
      this.now(),
    )),
  );
  /** What the Daily view lists under Today: the unfiltered board, the current day's lines, and the saved priority order. */
  readonly dailyTodayView = computed(() => {
    const data = this.dailyData();
    return data && this.activityReady() && this.ongoingReady()
      ? dailyToday(this.fullBoard(), data.before, data.digest.today, { zone: this.timezone, order: this.boardOrder() })
      : null;
  });
  /** The agent digests of the loaded Daily view, indexed by the observations they cover. */
  readonly dailyDigests = computed(() => digestIndex(this.dailyData()?.digest.digests));
  readonly board = computed<Board>(() => filterBoard(this.fullBoard(), this.boardFilter()));
  readonly boardTotal = computed(() => stages.reduce((total, stage) => total + this.fullBoard()[stage].length, 0));
  readonly boardShown = computed(() => stages.reduce((total, stage) => total + this.board()[stage].length, 0));
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
  readonly documentNavigationGroups = computed(() =>
    groupDocuments(this.selectedChange()?.documents ?? [], this.documentFilter()),
  );
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
    effect((onCleanup) => {
      const status = this.liveStatus();
      untracked(() => {
        this.reconnectingLong.set(false);
        if (status === "paused") this.announceLive("Updates paused");
        else if (status === "live") this.announceLive(this.liveAnnounced ? "Live" : "");
      });
      if (status !== "reconnecting") return;
      const timer = setTimeout(() => {
        this.reconnectingLong.set(true);
        this.announceLive("Reconnecting");
      }, reconnectingGraceMs);
      onCleanup(() => clearTimeout(timer));
    });
  }
  private announceLive(text: string) {
    this.liveAnnounced = text !== "" && text !== "Live";
    this.liveAnnouncement.set(text);
  }
  ngAfterViewInit() {
    this.autoStartLive = true;
    void this.start();
  }
  ngOnDestroy() {
    this.destroyed = true;
    this.selectionEpoch++;
    this.catalogEpoch++;
    this.documentEpoch++;
    this.historyEpoch++;
    this.searchEpoch++;
    this.reviewEpoch++;
    this.summaryEpoch++;
    this.detailEpoch++;
    this.ongoingEpoch++;
    this.pendingRevision = null;
    this.releaseForegroundWaiters();
    clearTimeout(this.clockTimer);
    clearTimeout(this.saveTimer);
    clearTimeout(this.searchTimer);
    clearTimeout(this.toastTimer);
    void this.liveConnection?.stop();
    this.refreshQueue.clear();
  }
  private async start() {
    if (this.ready()) return;
    if (this.startupAttempt) return this.startupAttempt;
    this.startupRetrying.set(true);
    const attempt = this.initializeStart();
    this.startupAttempt = attempt;
    try {
      await attempt;
    } finally {
      if (this.startupAttempt === attempt) this.startupAttempt = undefined;
      this.startupRetrying.set(false);
    }
  }
  async retryStartup() {
    if (this.startupAttempt || this.ready()) return;
    this.error.set("");
    this.unauthorized.set(false);
    await this.start();
  }
  private async initializeStart() {
    try {
      const fragment = new URLSearchParams(window.location.hash.slice(1));
      const token = fragment.get("connect");
      if (token && this.pendingConnectToken === null) {
        this.pendingConnectToken = token;
        window.history.replaceState(
          null,
          "",
          window.location.pathname + window.location.search,
        );
      }
      let verifiedInfo: { timezone: string; version?: string } | undefined;
      if (this.pendingConnectToken !== null) {
        try {
          await api("bootstrap", { token: this.pendingConnectToken });
          this.pendingConnectToken = null;
        } catch (error) {
          if (!(error instanceof ApiError && error.status === 401)) throw error;
          // A one-time launch token may already have been used in this browser. Only the
          // protected endpoint below can prove that its existing cookie still works.
          this.pendingConnectToken = null;
          verifiedInfo = await api<{ timezone: string; version?: string }>("info");
        }
      }
      const [catalog, state, info] = await Promise.all([
        api<Catalog>("catalog"),
        api<Preferences>("state"),
        verifiedInfo ? Promise.resolve(verifiedInfo) : api<{ timezone: string; version?: string }>("info"),
      ]);
      this.catalog.set(catalog);
      this.versionLabel.set(info.version ?? "");
      this.preferences = state;
      this.boardOrder.set(
        Array.isArray(state.board?.order) ? state.board.order.filter((key) => typeof key === "string") : [],
      );
      this.editor.set(validEditor(state.editor));
      this.dailyFormat.set(state.daily?.format === "markdown" ? "markdown" : "text");
      this.dailyLinks.set(state.daily?.links === true);
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
      this.digestRevision = catalog.digestRevision;
      this.ready.set(true);
      if (
        window.location.pathname === "/" &&
        !window.location.search &&
        state.lastRoute
      ) {
        await this.router.navigateByUrl(state.lastRoute);
      } else await this.routeChanged();
      // From here on a route change loads the Daily view itself; the first refresh below covers the opening route.
      this.dataLoaded = true;
      await this.refreshData();
      await this.refreshOverview();
      if (this.autoStartLive) this.startLiveUpdates();
      this.armClockTimer();
    } catch (error) {
      this.report(error);
    }
  }
  private startLiveUpdates() {
    if (this.liveConnection) return;
    this.liveConnection = new LiveConnectionController({
      create: () => new HubConnectionBuilder()
        .withUrl("/api/live", {
          transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
          withCredentials: true,
        })
        .configureLogging(LogLevel.None)
        .build(),
      onRevision: (snapshot) => this.receiveRevision(snapshot),
      onSnapshot: async (snapshot) => {
        const force = this.forceNextSnapshot;
        this.forceNextSnapshot = false;
        const success = await this.applyRevisionSnapshot(snapshot, force);
        return !success && this.deferredWhileHidden ? "deferred" : success;
      },
      onStatus: (status) => this.setLiveStatus(status),
      onUnauthorized: () => {
        this.invalidateAuthorizationWork();
        this.notice.set(this.authorizationLostMessage);
      },
      onAuthorized: () => {
        this.authorizationEpoch++;
        this.pendingRevision = null;
        this.unauthorized.set(false);
        this.error.set("");
        this.notice.set("");
      },
    });
    void this.liveConnection.start();
  }
  /**
   * Keyboard focus on the Retry button would fall to the body once the button is disabled or removed while recovery runs.
   * Move it to a stable element of the same surface before awaiting; focus elsewhere (a comment draft) is never touched.
   */
  private keepFocusOffRetry(event: Event | undefined, target: HTMLElement | null | undefined) {
    const button = event?.currentTarget as HTMLElement | null | undefined;
    if (!button || !target || globalThis.document?.activeElement !== button) return;
    target.focus({ preventScroll: true });
  }
  async retryLiveUpdates(event?: Event) {
    this.keepFocusOffRetry(event, globalThis.document?.querySelector<HTMLElement>("[data-testid=live-indicator]") ?? globalThis.document?.querySelector<HTMLElement>("main"));
    if (!this.liveConnection) return;
    this.forceNextSnapshot = true;
    this.liveRetrying.set(true);
    try {
      await this.liveConnection.retry();
    } finally {
      this.liveRetrying.set(false);
    }
  }
  async retryLiveFromReader(event: Event) {
    const reader = (event.currentTarget as HTMLElement | null)?.closest<HTMLElement>(".reader") ?? null;
    this.keepFocusOffRetry(event, reader);
    await this.retryLiveUpdates();
    // The Retry button leaves the DOM once updates resume; keep focus inside the modal.
    setTimeout(() => {
      const active = globalThis.document?.activeElement;
      if (reader?.isConnected && (!active || active === globalThis.document.body)) reader.focus({ preventScroll: true });
    });
  }
  /** The status the page presents: a Reconnecting shorter than the grace period keeps showing the previous state. */
  readonly presentedLiveStatus = computed<LiveConnectionStatus>(() => {
    const status = this.liveStatus();
    if (status !== "reconnecting") return status;
    return this.reconnectingLong() ? status : this.steadyStatus();
  });
  presentedLabel() {
    return this.labelFor(this.presentedLiveStatus());
  }
  private labelFor(status: LiveConnectionStatus) {
    return status === "live" ? "Live" : status === "connecting" ? "Connecting…" : status === "reconnecting" ? "Reconnecting" : "Updates paused";
  }
  /** The header indicator's tooltip and accessible name. */
  liveIndicatorLabel() {
    return this.presentedLabel() + " · " + this.lastRefreshLabel();
  }
  lastRefreshLabel() {
    const value = this.lastSuccessfulRefresh();
    if (!value) return "No successful live refresh yet";
    try {
      return "Last successful refresh " + new Intl.DateTimeFormat(undefined, {
        timeZone: this.timezone,
        dateStyle: "medium",
        timeStyle: "short",
      }).format(new Date(value));
    } catch {
      return "Last successful refresh " + value;
    }
  }
  private receiveRevision(snapshot: RevisionSnapshot): Promise<void> {
    if (this.destroyed || this.unauthorized()) return Promise.resolve();
    // A hidden tab only postpones the work: the status stays as it is until the tab is visible again.
    if (!document.hidden) {
      if (this.liveConnection) this.liveConnection.markReconciling();
      else this.setLiveStatus("reconnecting");
    }
    return this.queueRevisionWork(snapshot, false).then((success) => {
      if (success) this.liveConnection?.markReconciled();
      else if (this.deferredWhileHidden) this.liveConnection?.markDeferred();
      else this.liveConnection?.markPaused();
    });
  }
  private applyRevisionSnapshot(snapshot: RevisionSnapshot, forcePending: boolean): Promise<boolean> {
    return this.queueRevisionWork(snapshot, forcePending);
  }
  private queueRevisionWork(snapshot: RevisionSnapshot, forcePending: boolean): Promise<boolean> {
    if (this.destroyed || this.unauthorized()) return Promise.resolve(false);
    if (this.revisionWork) {
      const pending = this.pendingRevision;
      this.pendingRevision = {
        snapshot: pending ? this.mergeRevisionSnapshots(pending.snapshot, snapshot) : snapshot,
        force: forcePending || !!pending?.force,
      };
      return this.revisionWork;
    }
    const work = this.drainRevisionWork(snapshot, forcePending);
    this.revisionWork = work;
    void work.then(() => this.finishRevisionWork(work), () => this.finishRevisionWork(work));
    return work;
  }
  private finishRevisionWork(work: Promise<boolean>) {
    if (this.revisionWork !== work) return;
    this.revisionWork = null;
    const pending = this.pendingRevision;
    this.pendingRevision = null;
    if (pending && !this.destroyed) void this.queueRevisionWork(pending.snapshot, pending.force);
  }
  private async drainRevisionWork(snapshot: RevisionSnapshot, force: boolean): Promise<boolean> {
    const authorizationEpoch = this.authorizationEpoch;
    let success = false;
    let current: { snapshot: RevisionSnapshot; force: boolean } | null = { snapshot, force };
    while (current) {
      if (this.destroyed || this.unauthorized() || authorizationEpoch !== this.authorizationEpoch) return false;
      success = await this.applyRevisionSnapshotNow(current.snapshot, current.force);
      if (this.destroyed || this.unauthorized() || authorizationEpoch !== this.authorizationEpoch) return false;
      current = this.pendingRevision;
      this.pendingRevision = null;
    }
    return success;
  }
  private mergeRevisionSnapshots(left: RevisionSnapshot, right: RevisionSnapshot): RevisionSnapshot {
    if (left.instance !== right.instance) return right;
    return {
      instance: right.instance,
      catalog: Math.max(left.catalog, right.catalog),
      documents: Math.max(left.documents, right.documents),
      journal: Math.max(left.journal, right.journal),
      digests: Math.max(left.digests, right.digests),
      reads: Math.max(left.reads, right.reads),
      dirty: [...new Set([...left.dirty, ...right.dirty])],
    };
  }
  private async applyRevisionSnapshotNow(snapshot: RevisionSnapshot, forcePending: boolean): Promise<boolean> {
    if (this.destroyed || this.unauthorized()) return false;
    const wasDeferred = this.deferredWhileHidden;
    this.deferredWhileHidden = false;
    let change;
    try {
      change = this.revisionTracker.accept(snapshot);
    } catch (error) {
      this.report(error);
      return false;
    }
    const pending = this.revisionTracker.pending();
    if (!change.changed && pending.length === 0) return true;
    if (document.hidden) {
      // Pending work waits for the tab to become visible again; that is not a failure.
      this.deferredWhileHidden = true;
      return false;
    }
    // The first visible apply after a deferral is forced, whichever of focus and visibility arrives first.
    if (!change.changed && !forcePending && !wasDeferred) return false;
    const domains = change.changed ? pending : pending;
    const target = this.revisionTracker.desired();
    if (!target) return false;
    this.journalSnapshot = target.journal;
    this.digestRevision = target.digests;
    const success = await this.reconcileDomains(target, domains, forcePending || wasDeferred);
    if (this.destroyed) return false;
    if (!success) return false;
    this.revisionTracker.markApplied(target.instance, target, domains);
    if (this.revisionTracker.pending().length !== 0) return false;
    this.supersessionFollowupEpoch = -1;
    this.lastSuccessfulRefresh.set(this.now().toISOString());
    this.liveConnection?.markReconciled();
    return true;
  }
  private async reconcileDomains(target: RevisionSnapshot, domains: readonly LiveDomain[], retryFailed: boolean): Promise<boolean> {
    if (this.destroyed || this.unauthorized()) return false;
    const authorizationEpoch = this.authorizationEpoch;
    const selectedAtStart = this.selectionEpoch;
    const changes = new Set(domains);
    const work: Promise<boolean>[] = [];
    const add = (name: string, depends: readonly LiveDomain[], scope: string, run: () => Promise<boolean>) => {
      if (!depends.some((domain) => changes.has(domain))) return;
      const token = revisionToken(target, depends) + ":" + scope;
      work.push(this.refreshQueue.enqueue(name, token, run, retryFailed));
    };

    add("catalog-and-reader", ["catalog", "documents"], this.document() ? this.documentKey(this.document()!) : "closed", async () => {
      if (!await this.refreshCatalog()) return false;
      if (selectedAtStart !== this.selectionEpoch) return true;
      await this.waitForForegroundIdle();
      if (selectedAtStart !== this.selectionEpoch) return true;
      return this.refreshSelectedDocument(changes.has("catalog"));
    });
    add("reviews", ["documents"], JSON.stringify(this.selectedRepos()), () => this.loadReviews());
    add("read-summary", ["documents", "reads"], JSON.stringify(this.selectedRepos()), () => this.loadReadSummary());
    add("open-read-baseline", ["reads"], this.document() ? this.documentKey(this.document()!) : "closed", async () => {
      await this.waitForForegroundIdle();
      if (this.destroyed || this.unauthorized() || authorizationEpoch !== this.authorizationEpoch) return false;
      return this.refreshOpenBaseline(this.selectionEpoch);
    });
    add("search", ["catalog", "documents"], this.searchText.trim() + ":" + this.searchScope + ":" + JSON.stringify(this.selectedRepos()), () => this.search());

    add("journal-view", ["journal"], this.journalScope(), () => this.loadVisibleJournal());
    if (this.view() === "daily")
      add("daily-digest", ["journal", "digests"], this.dailyScope(), () => this.loadDaily());
    const results = await Promise.all(work);
    if (this.destroyed || this.unauthorized() || authorizationEpoch !== this.authorizationEpoch) return false;
    if (selectedAtStart !== this.selectionEpoch) {
      if (this.supersessionFollowupEpoch !== this.selectionEpoch) {
        this.supersessionFollowupEpoch = this.selectionEpoch;
        const pending = this.pendingRevision;
        this.pendingRevision = {
          snapshot: pending ? this.mergeRevisionSnapshots(pending.snapshot, target) : target,
          force: true,
        };
      }
      return false;
    }
    return results.every(Boolean);
  }
  private rangeKey() {
    return [this.from, this.to, this.timezone, this.ticket, this.groupBy, this.focusedDay()].join("|");
  }
  private journalScope() {
    return this.view() + ":" + JSON.stringify(this.selectedRepos()) + ":" + this.rangeKey();
  }
  private dailyScope() {
    return (this.dailyDay ?? "latest") + ":" + JSON.stringify(this.selectedRepos()) + ":" + this.timezone + ":" + localDate(this.now(), this.timezone) + ":" + this.selectionEpoch;
  }
  private queueDailyRefresh(target: RevisionSnapshot, retryFailed = false) {
    return this.refreshQueue.enqueue(
      "daily-digest",
      revisionToken(target, ["journal", "digests"]) + ":" + this.dailyScope(),
      () => this.view() === "daily" ? this.loadDaily() : Promise.resolve(true),
      retryFailed,
    );
  }
  private refreshDaily(retryFailed = false): Promise<boolean> {
    if (this.view() !== "daily") return Promise.resolve(true);
    const target = this.revisionTracker.desired();
    if (!this.liveConnection || !target) return this.loadDaily();
    return this.queueDailyRefresh(target, retryFailed);
  }
  private async loadVisibleJournal(): Promise<boolean> {
    const journalWork: Promise<boolean>[] = [];
    if (this.view() === "history") journalWork.push(this.loadHistory());
    else if (this.view() === "archive") journalWork.push(this.loadActivity());
    else {
      journalWork.push(this.loadOngoing(), this.loadActivity());
    }
    if (this.detail()) journalWork.push(this.refreshDetail());
    const results = await Promise.all(journalWork);
    return results.every(Boolean);
  }
  private async activateLiveScope() {
    if (this.destroyed || document.hidden) return;
    const target = this.revisionTracker.desired();
    if (!target || !this.liveConnection) {
      // ORCH-023: before the first live revision the visible view must not wait for one. The opening route is loaded by
      // start(); once the live connection exists, any activation loads its own data. Live still waits for a reconciled revision.
      if (this.dataLoaded && this.liveConnection) await Promise.all([this.loadVisibleJournal(), this.refreshDaily()]);
      return;
    }
    const journal = this.refreshQueue.enqueue(
      "journal-view",
      revisionToken(target, ["journal"]) + ":" + this.journalScope(),
      () => this.loadVisibleJournal(),
    );
    const daily = this.view() === "daily"
      ? this.queueDailyRefresh(target)
      : Promise.resolve(true);
    const results = await Promise.all([journal, daily]);
    if (this.destroyed) return;
    if (results.every(Boolean) && this.revisionTracker.pending().length === 0) this.liveConnection.markReconciled();
    else this.liveConnection.markPaused();
  }
  private newestLoads = new Map<string, Promise<boolean>>();
  /**
   * Runs one call of a loader. A call that a newer call of the same loader supersedes publishes nothing and joins the newer call's
   * result through `newest()`, so a foreground reload during a live reconcile neither fails it nor acknowledges it early.
   * With no newer call (the epoch was bumped by a clear, a close or a teardown) `newest()` yields `whenAlone`.
   */
  private joined(name: string, run: (newest: (error?: unknown) => Promise<boolean>) => Promise<boolean>, whenAlone = false): Promise<boolean> {
    let call!: Promise<boolean>;
    const newest = (error?: unknown) => {
      // A 401 is session-wide: report it even from a superseded call.
      if (error instanceof ApiError && error.status === 401) {
        this.report(error);
        return Promise.resolve(false);
      }
      const latest = this.newestLoads.get(name);
      return this.destroyed || !latest || latest === call ? Promise.resolve(whenAlone) : latest;
    };
    call = run(newest);
    this.newestLoads.set(name, call);
    return call;
  }
  private waitForForegroundIdle(): Promise<void> {
    if (this.destroyed) return Promise.resolve();
    if (!this.saving() && !this.documentLoading()) return Promise.resolve();
    return new Promise((resolve) => this.foregroundWaiters.push(resolve));
  }
  private releaseForegroundWaiters() {
    const waiters = this.foregroundWaiters.splice(0);
    for (const resolve of waiters) resolve();
  }
  private async refreshCatalog(): Promise<boolean> {
    if (this.destroyed) return false;
    return this.joined("catalog", (newest) => this.refreshCatalogNow(newest));
  }
  private async refreshCatalogNow(newest: (error?: unknown) => Promise<boolean>): Promise<boolean> {
    const epoch = ++this.catalogEpoch;
    try {
      const snapshot = await api<Catalog>("catalog");
      if (epoch !== this.catalogEpoch) return newest();
      this.catalog.set(snapshot);
      return true;
    } catch (error) {
      if (epoch !== this.catalogEpoch) return newest(error);
      this.report(error);
      return false;
    }
  }
  private async refreshSelectedDocument(catalogChanged: boolean): Promise<boolean> {
    const selected = this.document();
    if (!selected) return true;
    const selection = this.selectionEpoch;
    const candidates = this.catalog()?.changes.filter((change) =>
      change.sourceId === selected.sourceId &&
      (change.name === selected.change || change.directoryName === selected.change) &&
      (!selected.variant || change.variant === selected.variant),
    ) ?? [];
    const matchingChange = candidates.length === 1 ? candidates[0] : null;
    const archiveStateChanged = matchingChange !== null && matchingChange.archived !== selected.archived;
    if (matchingChange) this.selectChange(matchingChange);
    else this.selectChange(null);
    if (selection !== this.selectionEpoch) return true;
    // Catalog or matching-change metadata can alter archive permissions without changing Markdown.
    // Ask for the full response on those transitions; otherwise preserve the unchanged-content fast path.
    return this.loadDocument(this.readRequest(selected), undefined, true, !(catalogChanged || archiveStateChanged));
  }
  private async refreshOpenBaseline(selection: number): Promise<boolean> {
    if (this.destroyed) return false;
    const initial = this.document();
    if (!initial) return true;
    const key = this.documentKey(initial);
    const pane = document.querySelector<HTMLElement>(".document-scroll");
    const readerFocus = captureReaderFocus(pane);
    const readerFocusedElement = pane?.contains(document.activeElement) ? document.activeElement : null;
    // A document refresh and a read-index revision can arrive together. If the
    // document advances while reads/get is in flight, retry once for that same
    // logical selection before leaving the shared read revision pending.
    for (let attempt = 0; attempt < 2; attempt++) {
      const selected = this.document();
      if (!selected) return true;
      if (selection !== this.selectionEpoch || this.documentKey(selected) !== key) {
        return false;
      }
      try {
        const baseline = await api<ReadBaseline | null>("reads/get", { document: this.readRequest(selected) });
        if (this.destroyed) return false;
        const current = this.document();
        if (!current) return true;
        if (selection !== this.selectionEpoch || this.documentKey(current) !== key) {
          return false;
        }
        if (current.revision !== selected.revision) continue;
        const restoreFocus = !!readerFocus && document.activeElement === readerFocusedElement;
        const scrollToPreserve = pane?.scrollTop;
        const scrollEpochForRestore = this.readerScrollEpoch;
        this.baseline.set(baseline);
        this.renderDocument(current);
        if (pane && scrollToPreserve !== undefined) pane.scrollTop = scrollToPreserve;
        setTimeout(() => {
          if (this.destroyed) return;
          const latest = this.document();
          if (!latest || selection !== this.selectionEpoch || this.documentKey(latest) !== key
            || latest.revision !== current.revision) return;
          const currentPane = document.querySelector<HTMLElement>(".document-scroll");
          if (currentPane && scrollToPreserve !== undefined && this.readerScrollEpoch === scrollEpochForRestore)
            currentPane.scrollTop = scrollToPreserve;
          if (restoreFocus) {
            const active = document.activeElement;
            const neutralFocus = active === document.body || active === document.documentElement || !active?.isConnected;
            const originalStillFocused = active === readerFocusedElement;
            const replacedReaderFocus = !readerFocusedElement?.isConnected && neutralFocus;
            if (originalStillFocused || replacedReaderFocus) restoreReaderFocus(currentPane, readerFocus);
          }
        }, 0);
        return true;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) this.report(error);
        return false;
      }
    }
    return false;
  }
  private async refreshDetail(): Promise<boolean> {
    const detail = this.detail();
    if (!detail) return true;
    const query = this.view() === "ongoing" || this.view() === "daily" ? this.ongoingQuery : this.historyQuery;
    if (!query) return true;
    const epoch = ++this.detailEpoch;
    try {
      const page = await api<ObservationPage>("observations", { query, id: detail.id, day: this.detailDay() });
      if (epoch !== this.detailEpoch || this.detail()?.id !== detail.id) return false;
      this.detailQuery = query;
      this.observations.set(page.entries);
      this.observationNext.set(page.nextOffset);
      this.observationTotal.set(page.total);
      return true;
    } catch (error) {
      if (epoch === this.detailEpoch) this.report(error);
      return false;
    }
  }
  private report(error: unknown) {
    if (error instanceof ApiError && error.status === 401) {
      this.invalidateAuthorizationWork();
      this.liveConnection?.authorizationLost();
    }
    this.error.set(error instanceof Error ? error.message : String(error));
  }
  private invalidateAuthorizationWork() {
    if (!this.unauthorized()) this.authorizationEpoch++;
    this.unauthorized.set(true);
    this.pendingRevision = null;
    this.refreshQueue.clear();
    this.releaseForegroundWaiters();
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

  repositoryColor(id: string | null | undefined) {
    return repositoryColorKey(id);
  }

  distinctRepositories(ids: string[]) {
    return [...new Set(ids.filter((id) => repositoryColorKey(id) !== null))];
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
    return this.joined("reviews", (newest) => this.loadReviewsNow(newest));
  }
  private async loadReviewsNow(newest: (error?: unknown) => Promise<boolean>) {
    const epoch = ++this.reviewEpoch;
    try {
      // Always every scope: the reader and archived documents need their counts whichever scope the panel shows.
      const list = await api<ReviewList>("reviews", {
        repositories: this.selectedRepos() ?? [],
        scope: "all",
      });
      if (epoch !== this.reviewEpoch) return newest();
      this.inbox.set(list);
      return true;
    } catch (error) {
      if (epoch !== this.reviewEpoch) return newest(error);
      this.report(error);
      return false;
    }
  }
  /**
   * Loads the recorded activity of the selected repositories for the Archive view and the quiet indicators of Ongoing work.
   * Skipped while History is open, and when neither the journal nor the selection moved since the last load.
   */
  async loadActivity() {
    if (this.view() === "history") return true;
    const repositories = this.selectedRepos() ?? [];
    const key = this.journalSnapshot + "|" + JSON.stringify(repositories);
    if (key === this.activityKey) return true;
    return this.joined("activity", (newest) => this.loadActivityNow(newest, repositories, key));
  }
  private async loadActivityNow(newest: (error?: unknown) => Promise<boolean>, repositories: readonly string[], key: string) {
    const epoch = ++this.activityEpoch;
    try {
      const list = await api<ChangeActivity[]>("activity/changes", { repositories });
      if (epoch !== this.activityEpoch) return newest();
      this.activity.set(list);
      this.activityScope.set(JSON.stringify(repositories));
      this.activityKey = key;
      this.armClockTimer();
      return true;
    } catch (error) {
      if (epoch !== this.activityEpoch) return newest(error);
      this.report(error);
      return false;
    }
  }
  async loadReadSummary() {
    return this.joined("summary", (newest) => this.loadReadSummaryNow(newest));
  }
  private async loadReadSummaryNow(newest: (error?: unknown) => Promise<boolean>) {
    const epoch = ++this.summaryEpoch;
    try {
      const summary = await api<ReadSummary>("reads/summary");
      if (epoch !== this.summaryEpoch) return newest();
      this.readSummary.set(summary.entries);
      return true;
    } catch (error) {
      // The baselines are unavailable (for example a locked file): no document is shown as updated, and there is nothing to report on every refresh.
      if (epoch === this.summaryEpoch) this.readSummary.set({});
      if (error instanceof ApiError && error.status === 401) this.report(error);
      return epoch !== this.summaryEpoch ? newest() : false;
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
  /** Whether the recorded activity covers the current repository selection. */
  private activityReady() {
    const scope = this.activityScope();
    return scope !== null && (scope === "[]" || scope === JSON.stringify(this.selectedRepos() ?? []));
  }
  /** Whether the loaded ticket rows and unlinked entries cover the current repository selection. */
  private ongoingReady() {
    const scope = this.ongoingScope();
    return scope !== null && scope === JSON.stringify(this.selectedRepos() ?? []);
  }
  /** Whether the card shows the quiet indication (an In-progress change idle for more than 14 days). */
  quiet(card: BoardCard) {
    this.clockRevision();
    return isQuiet(card, this.now(), this.timezone);
  }
  /** Opens a change card's document; a change present in several sources opens the copy chooser instead. */
  openCard(card: BoardCard, path?: string) {
    // Only the copies that contain the requested document are offered.
    const copies = path ? card.copies.filter((copy) => copy.documents.some((d) => d.path === path)) : card.copies;
    if (copies.length > 1) {
      this.copyPath = path;
      this.copies.set(copies);
    } else {
      this.copyPath = undefined;
      this.copies.set([]);
      void this.openChange(copies[0] ?? card.change!, path);
    }
  }
  cardComments(card: BoardCard) {
    return card.copies.reduce((total, copy) => total + this.commentCount(copy), 0);
  }
  cardUpdated(card: BoardCard) {
    return card.copies.flatMap((copy) => this.updatedDocs(copy));
  }
  latestActivity(change: Change) {
    return activityFor(change, this.activity());
  }
  trackChange(_index: number, c: Change) {
    return changeKey(c.sourceId, c.variant);
  }
  trackCopy(_index: number, c: Change) {
    return changeKey(c.sourceId, c.variant);
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
  async changeView(view: ViewName) {
    await this.router.navigate(["/" + view]);
  }
  async refreshData() {
    await Promise.all([this.loadHistory(), this.loadOngoing(), this.refreshDaily(true)]);
  }
  /** Loads the last recorded day (or the selected one) and today's lines in one request. */
  async loadDaily(): Promise<boolean> {
    const epoch = ++this.dailyEpoch;
    const selectedAtStart = this.selectionEpoch;
    const scopeAtStart = this.dailyScope();
    const scopeIsCurrent = () => selectedAtStart === this.selectionEpoch && scopeAtStart === this.dailyScope();
    this.dailyLoading.set(true);
    try {
      const before = boundary(localDate(this.now(), this.timezone), this.timezone);
      const digest = await api<DailyDigest>("daily", {
        before,
        timezone: this.timezone,
        repositories: this.selectedRepos() ?? [],
        ...(this.dailyDay ? { day: this.dailyDay } : {}),
      });
      if (epoch !== this.dailyEpoch) return false;
      if (!scopeIsCurrent()) return true;
      this.dailyData.set({ digest, before });
      return true;
    } catch (error) {
      if (epoch !== this.dailyEpoch) return false;
      if (error instanceof ApiError && error.status === 401) {
        this.report(error);
        return false;
      }
      if (!scopeIsCurrent()) return true;
      if (error instanceof ApiError && error.status === 400 && this.dailyDay) {
        // A day the server no longer accepts (for example after the date moved on): fall back to the last recorded day.
        this.dailyDay = null;
        this.notice.set("That day is not available. Showing the last recorded day.");
        await this.router.navigate(["/daily"], { replaceUrl: true });
        return this.loadDaily();
      }
      this.report(error);
      return false;
    } finally {
      if (epoch === this.dailyEpoch && (scopeIsCurrent() || !this.refreshQueue.dirty("daily-digest")))
        this.dailyLoading.set(false);
    }
  }
  /** Moves the Daily view to another recorded day through the URL, so back and forward work. */
  async stepDaily(day: string | null) {
    if (day) await this.router.navigate(["/daily"], { queryParams: { day } });
  }
  setDailyFormat(value: unknown) {
    this.dailyFormat.set(value === "markdown" ? "markdown" : "text");
    this.rememberDaily();
  }
  setDailyLinks(value: boolean) {
    this.dailyLinks.set(value === true);
    this.rememberDaily();
  }
  private rememberDaily() {
    this.preferences = { ...this.preferences, daily: { format: this.dailyFormat(), links: this.dailyLinks() } };
    this.remember();
  }
  /** Copies the Yesterday, Today, and Blockers sections in the chosen format. */
  async copyDaily() {
    const data = this.dailyData();
    const today = this.dailyTodayView();
    if (!data || !today) return;
    const text = dailyText({
      day: data.digest.day,
      yesterday: data.digest.items,
      today: today.items,
      format: this.dailyFormat(),
      links: this.dailyLinks(),
      digests: data.digest.digests,
    });
    await this.copyText(text, "Copy this daily", "Daily copied.", true);
  }
  /** The heading of a digest line: a work item's title (a change name made readable), or an unlinked entry's own summary. */
  dailyTitle(item: DailyItem) {
    if (item.id.startsWith("adhoc:")) return item.title.trim() || noSummary;
    return item.tickets.length ? item.title : humanize(item.title);
  }
  readonly noSummary = noSummary;
  /** The agent digest shown in place of a Daily line's latest summary, or null. */
  dailyDigest(line: DailyItem | null) {
    return digestFor(this.dailyDigests(), line);
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
  async loadHistory(more = false): Promise<boolean> {
    return this.joined(more ? "history-more" : "history", (newest) => this.loadHistoryNow(newest, more));
  }
  private async loadHistoryNow(newest: (error?: unknown) => Promise<boolean>, more: boolean): Promise<boolean> {
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
      if (epoch !== this.historyEpoch) return newest();
      if (more) page.rows = [...(this.timeline()?.rows ?? []), ...page.rows];
      this.historyQuery = { ...query, snapshot: page.snapshot, offset: 0 };
      this.timeline.set(page);
      return true;
    } catch (error) {
      if (epoch !== this.historyEpoch) return newest(error);
      this.report(error);
      return false;
    } finally {
      if (epoch === this.historyEpoch) this.loading.set(false);
    }
  }
  private ongoingEpoch = 0;
  async loadOngoing(): Promise<boolean> {
    return this.joined("ongoing", (newest) => this.loadOngoingNow(newest));
  }
  private async loadOngoingNow(newest: (error?: unknown) => Promise<boolean>): Promise<boolean> {
    const epoch = ++this.ongoingEpoch;
    try {
      const now = this.now();
      const query: TimelineQuery = {
        from: new Date(now.getTime() - boardWindowDays * 86400000).toISOString(),
        to: now.toISOString(),
        timezone: this.timezone,
        groupBy: "ticket",
        repositories: this.selectedRepos() ?? [],
      };
      let page = await api<Timeline>("timeline", query);
      if (this.destroyed || epoch !== this.ongoingEpoch) return newest();
      const rows = [...page.rows];
      query.snapshot = page.snapshot;
      while (page.nextOffset !== null) {
        if (this.destroyed || epoch !== this.ongoingEpoch) return newest();
        page = await api<Timeline>("timeline", {
          ...query,
          offset: page.nextOffset,
        });
        if (this.destroyed || epoch !== this.ongoingEpoch) return newest();
        rows.push(...page.rows);
      }
      if (this.destroyed || epoch !== this.ongoingEpoch) return newest();
      // The same snapshot keeps both reads consistent while agents keep logging.
      const unlinked = await api<UnlinkedPage>("ongoing/unlinked", {
        repositories: query.repositories,
        from: query.from,
        to: query.to,
        snapshot: query.snapshot,
      });
      if (!this.destroyed && epoch === this.ongoingEpoch) {
        this.ongoing.set(rows);
        this.unlinked.set(unlinked.entries);
        this.unlinkedTruncated.set(unlinked.truncated);
        this.ongoingQuery = query;
        this.ongoingScope.set(JSON.stringify(query.repositories));
        this.armClockTimer();
        return true;
      }
      return newest();
    } catch (error) {
      if (epoch !== this.ongoingEpoch) return newest(error);
      this.report(error);
      return false;
    }
  }
  /** Moves a card within its column by a keyboard-accessible action and saves the order. */
  moveCard(card: BoardCard, action: MoveAction) {
    const column = this.fullBoard()[card.stage].map((c) => c.key);
    const visible = new Set(this.board()[card.stage].map((c) => c.key));
    const next = moveWithin(column, visible, card.key, action);
    if (!next) {
      this.boardAnnouncement.set(
        humanize(card.title) + " is already " + (action === "down" ? "last" : "first") + " in " + this.stageLabel(card.stage) + ".",
      );
    } else this.applyColumn(card, next);
    this.focusCard(card.key, action);
  }
  /** Ad-hoc cards expand in place; the expansion is not remembered. */
  private expandedCards = signal<ReadonlySet<string>>(new Set());
  isExpanded(card: BoardCard) {
    return this.expandedCards().has(card.key);
  }
  toggleCard(card: BoardCard) {
    const next = new Set(this.expandedCards());
    if (!next.delete(card.key)) next.add(card.key);
    this.expandedCards.set(next);
  }
  stageLabel(stage: Stage) {
    return this.boardStages.find((s) => s.stage === stage)!.label;
  }
  dragStart(card: BoardCard, event: DragEvent) {
    this.dragged.set({ key: card.key, stage: card.stage });
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = "move";
      event.dataTransfer.setData("text/plain", card.key);
      const element = (event.target as Element | null)?.closest?.("[data-card-key]");
      if (element) event.dataTransfer.setDragImage(element, 12, 12);
    }
  }
  dragEnd() {
    this.dragged.set(null);
    this.dropMark.set(null);
  }
  /** Accepts a drag only over a card of the column it started in, and marks the edge the card would land next to. */
  dragOver(card: BoardCard, event: DragEvent) {
    const dragged = this.dragged();
    if (!dragged || dragged.stage !== card.stage) {
      if (event.dataTransfer) event.dataTransfer.dropEffect = "none";
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
    const box = (event.currentTarget as Element).getBoundingClientRect();
    this.dropMark.set({ key: card.key, after: event.clientY > box.top + box.height / 2 });
  }
  /** Drops the dragged card next to the card under the pointer; a drop from another column is ignored. */
  dropOn(card: BoardCard, event: DragEvent): boolean {
    const dragged = this.dragged();
    const mark = this.dropMark();
    this.dragEnd();
    if (!dragged || dragged.stage !== card.stage) return false;
    event.preventDefault();
    const column = this.fullBoard()[card.stage];
    const moved = column.find((c) => c.key === dragged.key);
    const next = moved && dropWithin(column.map((c) => c.key), dragged.key, card.key, mark?.key === card.key && mark.after);
    if (!moved || !next) return false;
    this.applyColumn(moved, next);
    return true;
  }
  private applyColumn(card: BoardCard, column: string[]) {
    // Ranks of active changes outside the current repository selection stay; any other card that is not on the board loses its rank.
    const live = new Set<string>();
    for (const stage of stages) for (const c of this.fullBoard()[stage]) live.add(c.key);
    for (const change of this.catalog()?.changes ?? []) if (!change.archived) live.add(boardChangeKey(change));
    const order = pruneOrder(rankColumn(this.boardOrder(), column), live);
    this.boardOrder.set(order);
    this.preferences = { ...this.preferences, board: { ...this.preferences.board, order } };
    this.remember();
    const shown = this.board()[card.stage];
    this.boardAnnouncement.set(
      humanize(card.title) + " moved to position " + (shown.findIndex((c) => c.key === card.key) + 1) + " of " + shown.length + " in " + this.stageLabel(card.stage) + ".",
    );
  }
  private focusCard(key: string, action: MoveAction) {
    setTimeout(() => {
      const card = Array.from(document.querySelectorAll<HTMLElement>("[data-card-key]")).find((e) => e.dataset["cardKey"] === key);
      (card?.querySelector<HTMLElement>(`[data-move="${action}"]`) ?? card)?.focus();
    });
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
    this.armClockTimer();
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
    this.copies.set([]);
    this.copyPath = undefined;
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
  selectReaderDocument(path: string) {
    if (this.mobileDocumentNavigationOpen()) this.closeMobileDocumentNavigation();
    void this.openDocument(path);
  }
  closeMobileDocumentNavigation() {
    this.mobileDocumentNavigationOpen.set(false);
    document.querySelector<HTMLElement>(".mobile-document-selector-toggle")?.focus({ preventScroll: true });
  }
  private selectChange(change: Change | null) {
    const identity = change ? changeKey(change.sourceId, change.variant) : "";
    if (identity !== this.documentFilterOwner) {
      this.documentFilterOwner = identity;
      this.documentFilter.set("");
      this.mobileDocumentNavigationOpen.set(false);
    }
    this.selectedChange.set(change);
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
    this.selectChange(null);
    this.copies.set([]);
    this.copyPath = undefined;
    this.releaseForegroundWaiters();
    await this.router.navigate(["/" + this.view()]);
  }
  private async routeChanged() {
    if (this.destroyed) return;
    if (this.navigating) return;
    this.selectionEpoch++;
    const url = this.router.parseUrl(this.router.url);
    const route = url.root.children["primary"]?.segments[0]?.path;
    const previous = this.view();
    this.view.set(route === "history" ? "history" : route === "archive" ? "archive" : route === "daily" ? "daily" : "ongoing");
    if (route === "daily") {
      const day = url.queryParams["day"];
      const wanted = validDate(day) ? day : null;
      const changed = previous !== "daily" || wanted !== this.dailyDay;
      this.dailyDay = wanted;
      if (changed && this.dataLoaded && !this.liveConnection) void this.loadDaily();
    }
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
        this.copyPath = path;
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
      this.copyPath = undefined;
      this.selectChange(selected[0] ?? null);
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
      this.copyPath = undefined;
      this.selectChange(null);
    }
    this.remember();
    if (this.destroyed) return;
    await this.activateLiveScope();
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
  ): Promise<boolean> {
    return this.joined("document", (newest) => this.loadDocumentNow(newest, request, anchor, poll, skipUnchanged));
  }
  private async loadDocumentNow(
    newest: (error?: unknown) => Promise<boolean>,
    request: DocumentRequest,
    anchor: string | undefined,
    poll: boolean,
    skipUnchanged: boolean,
  ): Promise<boolean> {
    const old = this.document();
    const same = old && this.documentKey(old) === this.documentKey(request);
    const navigationFocus = !poll && document.activeElement instanceof HTMLElement
      && document.activeElement.closest(".document-navigation")
      ? document.activeElement
      : null;
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
      if (epoch !== this.documentEpoch) return newest();
      // The open document is current: keep what is rendered and parse nothing.
      if ("unchanged" in answer && answer.unchanged) return true;
      const value = answer as DocumentContent;
      if (poll && same && value.revision === old.revision && value.canComment === old.canComment) {
        this.document.set(value);
        return true;
      }
      // A different document starts from its own baseline; the baseline is known before the first render so nothing flashes.
      if (!same) {
        await this.loadBaseline(value);
        if (epoch !== this.documentEpoch) return newest();
      }
      const pane = document.querySelector<HTMLElement>(".document-scroll");
      const scroll = pane?.scrollTop ?? 0;
      const readerFocus = captureReaderFocus(pane);
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
      // Only a load's own render counts: re-renders of the same document (baseline, mark-read) must not cancel its pending post-render work.
      const render = ++this.renderEpoch;
      this.renderDocument(value);
      const renderedKey = this.documentKey(value);
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
        // Skip only when this render is no longer the visible one (a newer render, a close or a navigation).
        const visible = this.document();
        if (this.destroyed || render !== this.renderEpoch || !visible || this.documentKey(visible) !== renderedKey) return;
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
        if (same && poll) restoreReaderFocus(document.querySelector<HTMLElement>(".document-scroll"), readerFocus);
        if (!same && !anchor) {
          const active = document.activeElement;
          const focusMoved = active instanceof HTMLElement && active.isConnected
            && active !== document.body && active !== navigationFocus;
          if (!focusMoved) {
            if (navigationFocus?.isConnected) navigationFocus.focus({ preventScroll: true });
            else document.querySelector<HTMLElement>(".reader")?.focus({ preventScroll: true });
          }
        }
      }, 0);
      return true;
    } catch (error) {
      if (epoch === this.documentEpoch) {
        this.commentStatus.set(
          error instanceof Error ? error.message : String(error),
        );
        if (!poll || (error instanceof ApiError && error.status === 401)) this.report(error);
        return false;
      }
      return newest(error);
    } finally {
      if (epoch === this.documentEpoch) this.documentLoading.set(false);
      if (epoch === this.documentEpoch && !poll) this.releaseForegroundWaiters();
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
    if (marks && baseline) {
      const key = this.documentKey(value) + "|" + baseline.revision + ">" + value.revision;
      if (this.removedReviewsMemo?.key !== key)
        this.removedReviewsMemo = { key, reviews: removedReviews(baseline.text, rendered.reviews) };
      this.removedComments.set(this.removedReviewsMemo.reviews);
    } else {
      this.removedReviewsMemo = null;
      this.removedComments.set([]);
    }
    // Every re-render replaces the rendered nodes, so the task anchors are attached again to the new ones.
    setTimeout(() => this.attachTaskAnchors(), 0);
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
    this.removedReviewsMemo = null;
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
      if (error instanceof ApiError && error.status === 401) {
        this.report(error);
        this.notice.set(this.authorizationLostMessage);
        return;
      }
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
    let marked = 0;
    let conflicts = 0;
    let failed = 0;
    let authorizationLost = false;
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
        marked++;
        const open = this.document();
        if (open && open.path === item.path && open.revision === content.revision) {
          this.baseline.set({ revision: content.revision, text: content.text });
          this.renderDocument(open);
          setTimeout(() => this.syncStops(), 0);
        }
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          this.report(error);
          authorizationLost = true;
          break;
        }
        if (error instanceof ApiError && error.status === 409) conflicts++;
        else failed++;
      }
    }
    const outcomes = [
      marked ? `${marked} ${marked === 1 ? "document was" : "documents were"} marked as read` : "",
      conflicts ? `${conflicts} ${conflicts === 1 ? "document changed" : "documents changed"} while being marked and still show changes` : "",
      failed ? `${failed} ${failed === 1 ? "document could" : "documents could"} not be marked; refresh the change, confirm its documents and read state are available, then retry` : "",
      authorizationLost ? this.authorizationLostMessage : "",
    ].filter(Boolean);
    this.notice.set(outcomes.join(". ") + (outcomes.length ? "." : ""));
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
    } catch (error) {
      // The baseline stays, so the write shows as a change rather than being hidden.
      if (error instanceof ApiError && error.status === 401) this.report(error);
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
    if (marks.incomplete)
      return "Comparison is incomplete; individual passages are not marked. Use Next change to review this document.";
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
      if (error instanceof ApiError && error.status === 401) this.report(error);
      this.commentStatus.set(
        error instanceof Error ? error.message : String(error),
      );
      if (error instanceof ApiError && error.status === 409)
        await this.loadDocument(d, undefined, true);
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
      this.releaseForegroundWaiters();
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
      if (error instanceof ApiError && error.status === 401) this.report(error);
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
      if (error instanceof ApiError && error.status === 409) await this.loadDocument(d, undefined, true);
      this.commentStatus.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
      this.releaseForegroundWaiters();
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
    this.readerScrollEpoch++;
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
  async search(): Promise<boolean> {
    const query = this.searchText.trim();
    if (!query) return true;
    // A cleared query supersedes with no newer call: that work is complete, not a failed refresh.
    return this.joined("search", (newest) => this.searchNow(newest, query), true);
  }
  private async searchNow(newest: (error?: unknown) => Promise<boolean>, query: string): Promise<boolean> {
    const epoch = ++this.searchEpoch;
    this.searchLoading.set(true);
    this.searchRevision = this.catalog()?.revision ?? null;
    try {
      const value = await api<SearchResults>("search", {
        query,
        repositories: this.selectedRepos() ?? [],
        scope: this.searchScope,
      });
      // A newer search owns the result; this call joins its outcome.
      if (epoch !== this.searchEpoch) return newest();
      this.searchResults.set(value);
      return true;
    } catch (error) {
      if (epoch !== this.searchEpoch) return newest(error);
      this.report(error);
      return false;
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
      lastRoute: this.routeToSave(),
    };
    // The server rejects more than 64 KiB: drop the lowest board ranks until the whole document fits, whatever the key lengths.
    const order = this.preferences.board?.order;
    if (order?.length) {
      const bytes = (value: readonly string[]) =>
        new TextEncoder().encode(JSON.stringify({ ...this.preferences, board: { ...this.preferences.board, order: value } })).length;
      const fitted = fitOrder(order, bytes, preferencesByteLimit);
      if (fitted.length !== order.length) {
        this.preferences = { ...this.preferences, board: { ...this.preferences.board, order: fitted } };
        this.boardOrder.set(fitted);
      }
    }
    const body = JSON.stringify(this.preferences);
    if (body === this.lastSaved) return;
    try {
      await api("state", this.preferences, "PUT");
      this.lastSaved = body;
    } catch (error) {
      this.preferencesFailed(error);
    }
  }
  /** The current route without the Daily view's selected day, which is not remembered. */
  private routeToSave() {
    try {
      const url = new URL(this.router.url, "http://worklog.local");
      if (url.pathname !== "/daily") return this.router.url;
      url.searchParams.delete("day");
      return url.pathname + url.search;
    } catch {
      return this.router.url;
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
  private temporalBoundaries(): number[] {
    const boundaries: number[] = [];
    const now = this.now();
    const dayMs = 86400000;
    const expiry = (lastAt: string) => {
      const instant = Date.parse(lastAt);
      if (Number.isFinite(instant)) boundaries.push(instant + boardWindowDays * dayMs);
    };
    for (const row of this.ongoing()) expiry(row.lastAt);
    for (const entry of this.unlinked()) expiry(entry.occurredAt);
    for (const item of this.activity()) {
      const instant = Date.parse(item.lastAt);
      if (!Number.isFinite(instant)) continue;
      const date = localDate(new Date(instant), this.timezone);
      boundaries.push(Date.parse(boundary(addDays(date, quietDays + 1), this.timezone)));
    }
    const today = localDate(now, this.timezone);
    boundaries.push(Date.parse(boundary(addDays(today, 1), this.timezone)));
    return boundaries.filter((instant) => Number.isFinite(instant) && instant > now.getTime());
  }
  private armClockTimer() {
    clearTimeout(this.clockTimer);
    if (this.destroyed || document.hidden || !this.ready()) return;
    const next = Math.min(...this.temporalBoundaries(), Infinity);
    const untilBoundary = Number.isFinite(next) ? Math.max(1, next - this.now().getTime()) : 60_000;
    this.clockTimer = setTimeout(() => this.timeChanged(), Math.min(60_000, untilBoundary));
  }
  private timeChanged() {
    if (this.destroyed) return;
    if (document.hidden) {
      clearTimeout(this.clockTimer);
      return;
    }
    const today = localDate(this.now(), this.timezone);
    if (today !== this.evaluatedDate) {
      this.evaluate();
      if (this.view() === "history" && this.range.kind === "relative" && !this.focusedDay()) void this.loadHistory();
      if (this.view() === "daily") void this.refreshDaily();
    }
    this.clockRevision.update((value) => value + 1);
    this.armClockTimer();
  }
  @HostListener("document:visibilitychange")
  async visibilityChanged() {
    if (document.hidden) {
      clearTimeout(this.clockTimer);
      return;
    }
    this.timeChanged();
    this.forceNextSnapshot = true;
    await this.liveConnection?.resume();
    await this.activateLiveScope();
  }
  @HostListener("window:focus")
  windowFocused() {
    this.timeChanged();
    void this.liveConnection?.resume();
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
      if (this.mobileDocumentNavigationOpen()) {
        this.closeMobileDocumentNavigation();
        event.preventDefault();
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
