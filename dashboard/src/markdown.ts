import MarkdownIt, { type MarkdownIt as Parser, type Token } from "markdown-it";
import DOMPurify from "dompurify";
import { intersects, type ReadMarks } from "./diff";
export interface Heading {
  id: string;
  text: string;
  level: number;
  line: number;
}
export interface Review {
  text: string;
  line: number;
  id: string | null;
  quote: string;
  marker: string;
}
export interface RenderedDocument {
  html: string;
  headings: Heading[];
  reviews: Review[];
}
/** The marker text of every review comment in a document, found by the same rule the reader renders them with. */
export function parseReviews(text: string): Review[] {
  const md = createMarkdownParser();
  return md.parse(text, {}).filter((token) => token.type === "worklog_review").map(reviewFromToken);
}

/** The review block rule is shared by rendering and token-only baseline extraction. */
function createMarkdownParser(): Parser {
  const md = new MarkdownIt({ html: false, linkify: false, typographer: false });
  md.block.ruler.before(
    "fence",
    "worklog_review",
    (state, start, end, silent) => {
      const offset = state.bMarks[start] + state.tShift[start];
      const prefix = state.src.slice(offset, state.eMarks[start]).match(/^<!-- REVIEW(?::|(?=\s))/);
      if (!prefix || state.sCount[start] - state.blkIndent >= 4) return false;
      const close = state.src.indexOf("-->", offset + prefix[0].length);
      if (close < 0) return false;
      let next = start;
      while (next < end && state.bMarks[next] <= close) next++;
      if (close >= state.eMarks[next - 1] || state.src.slice(close + 3, state.eMarks[next - 1]).trim()) return false;
      if (silent) return true;
      const token = state.push("worklog_review", "aside", 0);
      token.map = [start, next];
      token.content = state.src.slice(offset + prefix[0].length, close).trim();
      token.meta = { marker: state.src.slice(offset, close + 3) };
      state.line = next;
      return true;
    },
    { alt: ["paragraph", "reference", "blockquote", "list"] },
  );
  return md;
}

function reviewFromToken(token: Token): Review {
  const structured = token.content.match(/^id=([a-f0-9]{32})\nTarget: preceding passage\nQuote: ([^\n]*)\nComment:\n([\s\S]*)$/);
  const decode = (value: string) => value.replaceAll("-&#45;", "--");
  return {
    text: decode(structured ? structured[3].trim() : token.content),
    line: token.map?.[0] ?? 0,
    id: structured?.[1] ?? null,
    quote: structured ? decode(structured[2]) : "",
    marker: String(token.meta?.["marker"] ?? ""),
  };
}

/**
 * Renders a document. With `marks`, blocks that share a line with a changed range carry the `changed-since-read` class,
 * and each removed passage becomes a collapsed `removed-since-read` disclosure before the top-level block it preceded.
 */
export function renderMarkdown(text: string, canComment = false, marks: ReadMarks | null = null): RenderedDocument {
  const md = createMarkdownParser();
  const headings: Heading[] = [],
    reviews: Review[] = [];
  md.renderer.rules["worklog_review"] = (tokens, index) => {
    const t = tokens[index];
    const review = reviewFromToken(t);
    reviews.push(review);
    const changed = t.meta?.["changed"] === true;
    return (
      '<aside class="review-note' + (changed ? ' changed-since-read" data-read-state="changed' : '') + '" tabindex="-1" data-review-line="' + review.line + '">'
      + '<div class="review-note-header"><strong>Review comment</strong>'
      + (canComment ? '<div class="review-note-actions"><button type="button" data-review-action="edit" aria-label="Edit review comment">Edit</button>'
        + '<button type="button" data-review-action="delete" aria-label="Delete review comment">Delete</button></div>' : '')
      + '</div><blockquote class="review-reference">Regarding the preceding passage'
      + (review.quote ? ': “' + md.utils.escapeHtml(review.quote) + '”' : '') + '</blockquote><p>'
      + md.utils.escapeHtml(review.text).replaceAll("\n", "<br>") + '</p></aside>'
    );
  };
  md.renderer.rules["image"] = (_tokens, index) =>
    '<span class="image-placeholder">[Image: ' +
    md.utils.escapeHtml(_tokens[index].content) +
    "]</span>";
  md.renderer.rules["worklog_removed"] = (tokens, index) => {
    const removed = tokens[index];
    const body = removed.content.replace(/\n$/, "");
    const count = body.split("\n").length;
    return (
      '<details class="removed-since-read" data-removed-line="' + removed.map![0] + '"><summary>Removed since last read ('
      + count + (count === 1 ? " line" : " lines") + ')</summary><pre>' + md.utils.escapeHtml(body) + '</pre></details>'
    );
  };
  let tokens = md.parse(text, {});
  if (marks) tokens = applyMarks(md, tokens, marks);
  const counts = new Map<string, number>();
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i];
    if (
      t.map &&
      t.type !== "inline" &&
      t.type !== "worklog_review" &&
      t.nesting !== -1
    ) {
      t.attrSet("data-start", String(t.map[0]));
      t.attrSet("data-end", String(t.map[1]));
      t.attrSet("tabindex", "0");
    }
    if (t.type === "heading_open") {
      const title = tokens[i + 1]?.content ?? "";
      const slug =
        title
          .toLowerCase()
          .trim()
          .replace(/[^\p{L}\p{N}\s_-]/gu, "")
          .replace(/\s+/g, "-") || "section";
      const count = counts.get(slug) ?? 0;
      counts.set(slug, count + 1);
      const id = slug + (count ? "-" + (count + 1) : "");
      t.attrSet("id", id);
      headings.push({
        id,
        text: title,
        level: Number(t.tag.slice(1)),
        line: t.map?.[0] ?? 0,
      });
    }
    if (
      t.type === "inline" &&
      t.children &&
      /^\[[ xX]*\]\s/.test(t.content) &&
      tokens[i - 1]?.type === "paragraph_open"
    ) {
      const child = t.children[0];
      if (child?.type === "text") {
        const match = child.content.match(/^\[([ xX]*)\]\s*/);
        if (match) {
          const checked = match[1].trim().toLowerCase() === "x";
          child.content = child.content.slice(match[0].length);
          const checkbox = Object.assign(
            Object.create(Object.getPrototypeOf(child)) as typeof child,
            child,
          );
          checkbox.type = "html_inline";
          checkbox.content =
            '<input type="checkbox" disabled' +
            (checked ? " checked" : "") +
            ' aria-label="Task ' +
            (checked ? "complete" : "incomplete") +
            '">';
          t.children.unshift(checkbox);
        }
      }
    }
  }
  for (const type of ["fence", "code_block"]) {
    const render = md.renderer.rules[type]!;
    md.renderer.rules[type] = (ts, index, options, env, renderer) =>
      "<div " +
      (ts[index].map
        ? 'data-start="' +
          ts[index].map![0] +
          '" data-end="' +
          ts[index].map![1] +
          '" tabindex="0"'
        : "") +
      (ts[index].meta?.["changed"] ? ' class="changed-since-read" data-read-state="changed"' : "") +
      ">" +
      render(ts, index, options, env, renderer) +
      "</div>";
  }
  const html = DOMPurify.sanitize(md.renderer.render(tokens, md.options, {}), {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ["img", "svg", "math", "iframe", "object", "style", "script"],
    FORBID_ATTR: ["style"],
    ADD_ATTR: ["data-start", "data-end", "data-review-line", "data-review-action", "data-read-state", "data-removed-line", "tabindex"],
  });
  return { html, headings, reviews };
}

/**
 * Marks the innermost blocks that share a line with a changed range, and inserts the removed passages. A container
 * (list, table, quote) is marked only when none of its visible children is, so one edited list item highlights that
 * item rather than the whole list.
 */
function applyMarks(md: Parser, tokens: Token[], marks: ReadMarks): Token[] {
  const stack: { token: Token; hit: boolean; child: boolean }[] = [];
  const mark = (token: Token) => {
    if (token.type === "fence" || token.type === "code_block" || token.type === "worklog_review")
      token.meta = { ...token.meta, changed: true };
    else {
      token.attrJoin("class", "changed-since-read");
      token.attrSet("data-read-state", "changed");
    }
    for (const ancestor of stack) ancestor.child = true;
  };
  for (const token of tokens) {
    // Hidden paragraphs of tight lists render no element, so the list item stands for them.
    if (token.hidden || token.type === "inline") continue;
    if (token.nesting === -1) {
      const top = stack.at(-1);
      if (top && top.token.type === token.type.replace(/_close$/, "_open")) {
        stack.pop();
        if (top.hit && !top.child) mark(top.token);
      }
      continue;
    }
    if (!token.map) continue;
    const hit = intersects(marks.changed, token.map[0], token.map[1]);
    if (token.nesting === 1) stack.push({ token, hit, child: false });
    else if (hit) mark(token);
  }
  return insertRemoved(md, tokens, marks);
}

/** Puts each removed passage before the top-level block that contains, or next follows, the line it preceded. */
function insertRemoved(md: Parser, tokens: Token[], marks: ReadMarks): Token[] {
  if (marks.removed.length === 0) return tokens;
  const Token = new md.core.State("", md, {}).Token;
  const before = new Map<number, Token[]>();
  const trailing: Token[] = [];
  for (const hunk of marks.removed) {
    const removed = new Token("worklog_removed", "", 0);
    removed.map = [hunk.line, hunk.line];
    removed.content = hunk.text;
    removed.block = true;
    const at = tokens.findIndex((t) => t.level === 0 && t.nesting !== -1 && !!t.map && t.map[1] > hunk.line);
    if (at < 0) trailing.push(removed);
    else before.set(at, [...(before.get(at) ?? []), removed]);
  }
  return [...tokens.flatMap((t, index) => [...(before.get(index) ?? []), t]), ...trailing];
}
