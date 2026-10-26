import MarkdownIt from "markdown-it";
import DOMPurify from "dompurify";
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
export function renderMarkdown(text: string, canComment = false): RenderedDocument {
  const md = new MarkdownIt({
    html: false,
    linkify: false,
    typographer: false,
  });
  const headings: Heading[] = [],
    reviews: Review[] = [];
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
      token.content = state.src
        .slice(offset + prefix[0].length, close).trim();
      token.meta = { marker: state.src.slice(offset, close + 3) };
      state.line = next;
      return true;
    },
    { alt: ["paragraph", "reference", "blockquote", "list"] },
  );
  md.renderer.rules["worklog_review"] = (tokens, index) => {
    const t = tokens[index];
    const structured = t.content.match(/^id=([a-f0-9]{32})\nTarget: preceding passage\nQuote: ([^\n]*)\nComment:\n([\s\S]*)$/);
    const decode = (value: string) => value.replaceAll("-&#45;", "--");
    const review: Review = {
      text: decode(structured ? structured[3].trim() : t.content),
      line: t.map?.[0] ?? 0,
      id: structured?.[1] ?? null,
      quote: structured ? decode(structured[2]) : "",
      marker: String(t.meta?.["marker"] ?? ""),
    };
    reviews.push(review);
    return (
      '<aside class="review-note" data-review-line="' + review.line + '">'
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
  const tokens = md.parse(text, {});
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
      ">" +
      render(ts, index, options, env, renderer) +
      "</div>";
  }
  const html = DOMPurify.sanitize(md.renderer.render(tokens, md.options, {}), {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ["img", "svg", "math", "iframe", "object", "style", "script"],
    FORBID_ATTR: ["style"],
    ADD_ATTR: ["data-start", "data-end", "data-review-line", "data-review-action", "tabindex"],
  });
  return { html, headings, reviews };
}
