import { describe, it, expect } from "vitest";
import { renderMarkdown } from "./markdown";
import { boundary, elapsed, addDays } from "./api";

describe("OpenSpec document rendering", () => {
  it("retains heading identity and source block ranges", () => {
    const result = renderMarkdown(
      "# Design\n\n## Decision\n\nParagraph.\n\n## Decision\n\n- [x] 1.1 Done\n- [ ] 1.2 Next\n",
    );
    expect(result.headings.map((h) => h.id)).toEqual([
      "design",
      "decision",
      "decision-2",
    ]);
    const host = document.createElement("div");
    host.innerHTML = result.html;
    expect(host.querySelector('[data-start="4"]')?.textContent).toContain(
      "Paragraph.",
    );
    expect(host.querySelectorAll("input[type=checkbox]").length).toBe(2);
    expect(host.querySelector("input:checked")).not.toBeNull();
  });
  it("renders review notes without interpreting examples in code fences", () => {
    const result = renderMarkdown(
      "Paragraph\n\n<!-- REVIEW: Please fix -&#45;> this\nSecond line -->\n\n```md\n<!-- REVIEW: example -->\n```",
    );
    expect(result.reviews.length).toBe(1);
    expect(result.reviews[0].text).toBe("Please fix --> this\nSecond line");
    expect(result.html).toContain("review-note");
    expect(result.html).toContain("&lt;!-- REVIEW: example --&gt;");
  });
  it("does not create executable markup, images, or automatic remote resources", () => {
    const result = renderMarkdown(
      '<script>alert(1)</script>\n\n![secret](https://example.test/image)\n\n[bad](javascript:alert(1))\n\n<!-- REVIEW: <img src="https://example.test/review"> -->',
    );
    const host = document.createElement("div");
    host.innerHTML = result.html;
    expect(host.querySelector("script,img,iframe,object,svg")).toBeNull();
    expect(host.querySelector('a[href^="javascript:"]')).toBeNull();
    expect(host.textContent).toContain("Image: secret");
  });
  it("separates structured references from feedback and exposes only active-note controls", () => {
    const marker = '<!-- REVIEW: id=123456781234123412341234567890ab\nTarget: preceding passage\nQuote: Original <passage>\nComment:\nPlease fix -&#45;> this.\n-->';
    const rendered = renderMarkdown("Original <passage>\n\n" + marker, true);
    expect(rendered.reviews[0]).toMatchObject({
      id: "123456781234123412341234567890ab", quote: "Original <passage>",
      text: "Please fix --> this.", line: 2, marker,
    });
    const host = document.createElement("div");
    host.innerHTML = rendered.html;
    expect(host.querySelectorAll('[data-review-action]').length).toBe(2);
    expect(host.querySelector('.review-reference')?.textContent).toContain('Original <passage>');
    expect(host.querySelector('.review-note p')?.textContent).toBe('Please fix --> this.');
    expect(host.querySelector('passage')).toBeNull();
    expect(renderMarkdown(marker).html).not.toContain('data-review-action');
    const legacy = renderMarkdown('Paragraph\n\n<!-- REVIEW old note -->\n\n    <!-- REVIEW: code example -->', true);
    expect(legacy.reviews).toHaveLength(1);
    expect(legacy.reviews[0].text).toBe('old note');
  });
});
describe("timeline date semantics", () => {
  it("uses actual instants for Warsaw daylight saving boundaries", () => {
    expect(boundary("2026-10-25", "Europe/Warsaw")).toBe(
      "2026-10-24T22:00:00.000Z",
    );
    expect(boundary("2026-10-26", "Europe/Warsaw")).toBe(
      "2026-10-25T23:00:00.000Z",
    );
    expect(addDays("2026-10-31", 1)).toBe("2026-11-01");
    expect(elapsed(49 * 3600)).toBe("2d 1h");
  });
});
