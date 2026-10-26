import { createHash } from "node:crypto";
import { readdir, readFile, writeFile } from "node:fs/promises";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
const root = fileURLToPath(new URL("../", import.meta.url));
const hash = createHash("sha256");
async function walk(path) {
  for (const entry of (await readdir(path, { withFileTypes: true })).sort(
    (a, b) => a.name.localeCompare(b.name),
  )) {
    const file = join(path, entry.name);
    if (entry.isDirectory()) await walk(file);
    else {
      hash.update(relative(root, file).replaceAll("\\", "/"));
      hash.update(await readFile(file));
    }
  }
}
for (const name of [
  "package.json",
  "package-lock.json",
  "angular.json",
  "tsconfig.json",
  "tsconfig.app.json",
  "scripts/stamp.mjs",
]) {
  hash.update(name);
  hash.update(await readFile(join(root, name)));
}
await walk(join(root, "src"));
const digest = hash.digest("hex");
const stamp = join(root, "dist/dashboard/build.stamp");
if (process.argv.includes("--verify")) {
  if ((await readFile(stamp, "utf8")) !== digest)
    throw new Error(
      "Dashboard assets are stale. Run npm run build in dashboard before publishing.",
    );
} else await writeFile(stamp, digest);
