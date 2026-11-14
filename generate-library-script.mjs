import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const bundled = join(root, "Resources", "SymbolLibraries");
const libraries = {};
for (const entry of readdirSync(bundled, { withFileTypes: true }).filter((e) => e.isDirectory()).sort((a, b) => a.name.localeCompare(b.name))) {
  const library = JSON.parse(readFileSync(join(bundled, entry.name, "library.json"), "utf8"));
  if (library.id !== entry.name) throw new Error(`Library ID mismatch: ${entry.name}`);
  libraries[library.id] = library;
}
const hk = JSON.parse(readFileSync(join(root, "SignBuilderSymbols", "hk", "library.json"), "utf8"));
libraries[hk.id] = hk;
const json = JSON.stringify(libraries);
const renderer = join(root, "Resources", "Renderer");
writeFileSync(join(renderer, "addon-libraries.json"), json + "\n");
writeFileSync(join(renderer, "addon-libraries.js"), `window.HKSignBuilderLibraries = ${json};\n`);
// The subscribed 1.1.1 UI can remain active alongside a local update. Keep its
// script URL and single-library global so its preview loader can complete.
writeFileSync(join(renderer, "hk-library.js"),
  `window.HKSignBuilderLibraries = window.HKSignBuilderLibraries || ${json};\n` +
  `window.HKSignBuilderLibrary = window.HKSignBuilderLibraries.hk;\n`);
