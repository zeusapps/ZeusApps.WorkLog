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
import {
  Catalog,
  Change,
  DocumentContent,
  DocumentRequest,
  Entry,
  Insight,
  Match,
  ObservationPage,
  Preferences,
  Row,
  SearchResults,
  Selection,
  Timeline,
  TimelineQuery,
} from "./models";

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
  view = signal<"ongoing" | "history">("ongoing");
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
  editingReview = signal<(Review & { revision: string }) | null>(null);
  deletingReview = signal<(Review & { revision: string }) | null>(null);
  sourceView = signal(false);
  selection = signal<Selection | null>(null);
  comment = signal("");
  commentStatus = signal("");
  saving = signal(false);
  searchText = "";
  searchScope = "active";
  searchResults = signal<SearchResults | null>(null);
  from = "";
  to = "";
  timezone = "UTC";
  groupBy = "ticket";
  scale = "day";
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
  private pollCount = 0;
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
        api<{ timezone: string }>("info"),
      ]);
      this.catalog.set(catalog);
      this.preferences = state;
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
      this.timezone = state.history?.timezone ?? info.timezone;
      this.groupBy = state.history?.groupBy ?? "ticket";
      this.scale = state.history?.scale ?? "day";
      this.to = state.history?.to ?? localDate(new Date(), this.timezone);
      this.from = state.history?.from ?? addDays(this.to, -29);
      this.ready.set(true);
      if (
        window.location.pathname === "/" &&
        !window.location.search &&
        state.lastRoute
      ) {
        await this.router.navigateByUrl(state.lastRoute);
      } else await this.routeChanged();
      await this.refreshData();
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
  changeRow(change: Change) {
    return this.ongoing().find(
      (r) =>
        r.repository &&
        change.repositories.includes(r.repository) &&
        r.changes.includes(change.name),
    );
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
    await this.refreshData();
    if (this.searchText.trim()) void this.search();
  }
  async changeView(view: "ongoing" | "history") {
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
      this.remember();
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
  async controlsChanged() {
    this.remember();
    await this.loadHistory();
  }
  async focusDay(date: string) {
    this.from = date;
    this.to = date;
    await this.controlsChanged();
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
    this.view.set(
      url.root.children["primary"]?.segments[0]?.path === "history"
        ? "history"
        : "ongoing",
    );
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
      const value = await api<DocumentContent>("document", request);
      if (epoch !== this.documentEpoch) return;
      if (poll && same && value.revision === old.revision && value.canComment === old.canComment) {
        this.document.set(value);
        return;
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
      const rendered = renderMarkdown(value.text, value.canComment);
      this.html.set(this.sanitizer.bypassSecurityTrustHtml(rendered.html));
      this.headings.set(rendered.headings);
      this.reviews.set(rendered.reviews);
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
      this.preferences.documents ??= {};
      this.preferences.documents[value.sourceId + ":" + value.change] =
        value.path;
      setTimeout(() => {
        if (epoch !== this.documentEpoch) return;
        this.attachTaskAnchors();
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
      this.sourceView.set(false);
      setTimeout(() => this.scrollTo(anchor, report), 0);
      return;
    }
    this.attachTaskAnchors();
    let target: HTMLElement | null = null;
    if (anchor.startsWith("line-")) {
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
    this.saving.set(true);
    this.commentStatus.set("");
    try {
      await api<DocumentContent>(edit ? "comments/edit" : "comments", edit ? {
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
    this.saving.set(true);
    try {
      await api("comments/delete", {
        ...d, revision: review.revision, line: review.line,
        markerText: review.marker, id: review.id,
      });
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
    this.preferences.positions ??= {};
    this.preferences.positions[this.documentKey(d)] = (
      event.target as HTMLElement
    ).scrollTop;
    this.remember();
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
        : { id: item.itemId, title: item.title, repository: item.repository };
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
    });
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
  private remember() {
    if (!this.ready()) return;
    clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => {
      this.preferences = {
        ...this.preferences,
        schemaVersion: 1,
        repositories: this.selectedRepos(),
        history: {
          from: this.from,
          to: this.to,
          timezone: this.timezone,
          groupBy: this.groupBy,
          scale: this.scale,
        },
        lastRoute: this.router.url,
      };
      void api("state", this.preferences, "PUT").catch((e) => this.report(e));
    }, 350);
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
        await this.loadDocument(d, undefined, true);
      }
      if (this.searchText.trim()) void this.search();
      if (++this.pollCount % 5 === 0) await this.refreshData();
    } catch (error) {
      this.report(error);
    } finally {
      this.polling = false;
    }
  }
  @HostListener("document:keydown", ["$event"])
  keyboard(event: KeyboardEvent) {
    if (event.key === "Escape") {
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
