export const repositoryPaletteKeys = ["teal", "blue", "sand", "green", "rose", "violet", "aqua", "amber"] as const;
export type RepositoryPaletteKey = (typeof repositoryPaletteKeys)[number];

/** Maps the authoritative repository ID to a fixed palette entry, independent of catalog state. */
export function repositoryColorKey(id: string | null | undefined): RepositoryPaletteKey | null {
  if (typeof id !== "string" || id.trim().length === 0) return null;
  let hash = 2166136261;
  for (let index = 0; index < id.length; index++) {
    hash ^= id.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return repositoryPaletteKeys[(hash >>> 0) % repositoryPaletteKeys.length];
}
