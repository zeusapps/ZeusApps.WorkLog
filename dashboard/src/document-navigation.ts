import type { Change } from "./models";

type NavigableDocument = Change["documents"][number];

export interface DocumentNavigationGroup {
  id: "overview" | "specs" | "other";
  label: string;
  documents: NavigableDocument[];
}

const overviewPaths = new Set(["proposal.md", "design.md", "tasks.md"]);

/** Groups the existing documents by their relative paths and applies a local path/label filter. */
export function groupDocuments(documents: readonly NavigableDocument[], filter = ""): DocumentNavigationGroup[] {
  const query = filter.trim().toLocaleLowerCase();
  const groups: DocumentNavigationGroup[] = [
    { id: "overview", label: "Overview", documents: [] },
    { id: "specs", label: "Specs", documents: [] },
    { id: "other", label: "Other documents", documents: [] },
  ];
  for (const document of documents) {
    if (query && !document.path.toLocaleLowerCase().includes(query) && !document.label.toLocaleLowerCase().includes(query)) continue;
    const path = document.path.toLocaleLowerCase();
    const group = overviewPaths.has(path)
      ? groups[0]
      : path.startsWith("specs/") || path === "specs"
        ? groups[1]
        : groups[2];
    group.documents.push(document);
  }
  return groups.filter((group) => group.documents.length > 0);
}
