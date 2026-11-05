import { describe, expect, it } from "vitest";
import { groupDocuments } from "./document-navigation";
import type { Change } from "./models";

const document = (path: string, label = path.split("/").at(-1) ?? path): Change["documents"][number] => ({ path, label, fingerprint: "1:1" });

describe("document navigation", () => {
  it.each([10, 30, 50])("groups %i documents by relative path and filters nested duplicates", (count) => {
    const documents = [document("proposal.md"), document("design.md"), document("tasks.md"),
      document("specs/accounts/spec.md", "spec.md"), document("specs/billing/spec.md", "spec.md"),
      ...Array.from({ length: count - 5 }, (_, index) => document(`notes/area-${index}/readme.md`, "readme.md"))];

    const groups = groupDocuments(documents);
    expect(groups.map((group) => [group.id, group.documents.length])).toEqual([
      ["overview", 3], ["specs", 2], ["other", count - 5],
    ]);
    const filtered = groupDocuments(documents, "billing/spec.md");
    expect(filtered).toHaveLength(1);
    expect(filtered[0].documents.map((item) => item.path)).toEqual(["specs/billing/spec.md"]);
  });

  it("returns no groups for an empty match and restores groups when the filter clears", () => {
    const documents = [document("design.md"), document("specs/accounts/spec.md")];
    expect(groupDocuments(documents, "missing")).toEqual([]);
    expect(groupDocuments(documents, "").map((group) => group.documents.length)).toEqual([1, 1]);
  });
});
