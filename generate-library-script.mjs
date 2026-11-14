import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { execFileSync } from "node:child_process";

const root = dirname(fileURLToPath(import.meta.url));
const bundled = join(root, "Resources", "SymbolLibraries");
const project = join(root, "tools", "BuildSymbolLibraries", "BuildSymbolLibraries.csproj");
const libraries = JSON.parse(execFileSync("dotnet", ["run", "--project", project, "--configuration", "Release", "--", bundled],
  { encoding: "utf8", maxBuffer: 16 * 1024 * 1024 }));
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
