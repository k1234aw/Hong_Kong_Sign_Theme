import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join, resolve } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const destination = process.argv[2];
if (!destination) throw new Error("Usage: node prepare-public-theme.mjs <add-on package directory>");

const source = join(root, "SignBuilderThemes", "hk");
const target = join(resolve(destination), "SignBuilderThemes", "hk");
mkdirSync(join(target, "templates"), { recursive: true });

function writeLegacyTheme(relative) {
  const document = JSON.parse(readFileSync(join(source, relative), "utf8"));
  document.formatVersion = 1;
  writeFileSync(join(target, relative), JSON.stringify(document, null, 2) + "\n");
}

writeLegacyTheme("theme.json");
for (const file of readdirSync(join(source, "templates")).filter((file) => file.endsWith(".json")))
  writeLegacyTheme(join("templates", file));
