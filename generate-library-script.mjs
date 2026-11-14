import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const input = join(root, "SignBuilderSymbols", "hk", "library.json");
const output = join(root, "Resources", "Renderer", "hk-library.js");
const library = JSON.parse(readFileSync(input, "utf8"));
writeFileSync(output, `window.HKSignBuilderLibrary = ${JSON.stringify(library)};\n`);
