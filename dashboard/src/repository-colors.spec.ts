import { describe, expect, it } from "vitest";
import { repositoryColorKey } from "./repository-colors";

describe("repository color mapping", () => {
  it("is deterministic for representative repository identities", () => {
    expect(["github.com/acme/api", "github.com/acme/web", "gitlab.com/acme/tools"].map(repositoryColorKey)).toEqual(["green", "violet", "teal"]);
    expect(repositoryColorKey("github.com/acme/api")).toBe(repositoryColorKey("github.com/acme/api"));
  });

  it("does not require a catalog entry and treats missing IDs as neutral", () => {
    expect(repositoryColorKey("catalog-removed/repository")).not.toBeNull();
    expect(repositoryColorKey(undefined)).toBeNull();
    expect(repositoryColorKey(null)).toBeNull();
    expect(repositoryColorKey("  ")).toBeNull();
  });

  it("uses the repository ID exactly and does not depend on labels or ordering", () => {
    const ids = ["one", "two", "three"];
    const initial = new Map(ids.map((id) => [id, repositoryColorKey(id)]));
    expect([...ids].reverse().map((id) => [id, repositoryColorKey(id)])).toEqual([...initial.entries()].reverse());
    expect(repositoryColorKey("one")).toBe(repositoryColorKey("one"));
  });
});
