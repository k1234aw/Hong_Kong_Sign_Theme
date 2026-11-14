using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignBuilder.Core;

if (args.Length != 1)
    throw new ArgumentException("Usage: BuildSymbolLibraries <symbol library directory>");

var root = Path.GetFullPath(args[0]);
var result = new JObject();
var resolved = 0;
foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path))
{
    var file = Path.Combine(directory, "library.json");
    if (!File.Exists(file)) continue;
    var library = JObject.Parse(File.ReadAllText(file));
    var id = (string)library["id"] ?? Path.GetFileName(directory);
    if (id != Path.GetFileName(directory)) throw new InvalidDataException($"Library ID mismatch: {directory}");
    var legend = Colors(library["legend"]);
    if (library["symbols"] is JObject symbols)
    {
        foreach (var property in symbols.Properties().ToList())
        {
            if (!(property.Value is JObject symbol)) throw new InvalidDataException($"Invalid symbol {id}/{property.Name}");
            var svg = (string)symbol["file"];
            if (string.IsNullOrEmpty(svg))
            {
                if (!(symbol["viewBox"] is JArray) || !(symbol["parts"] is JArray))
                    throw new InvalidDataException($"Symbol {id}/{property.Name} has no drawable geometry");
                continue;
            }
            var path = Path.GetFullPath(Path.Combine(directory, svg));
            if (!path.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Symbol path escapes its library: {id}/{property.Name}");
            var shapes = SvgShapes.Read(path, symbol["legend"] != null ? Colors(symbol["legend"]) : legend);
            symbol.Remove("file");
            symbol["viewBox"] = shapes.viewBox;
            symbol["parts"] = shapes.parts;
            resolved++;
        }
    }
    result[id] = library;
}

Console.WriteLine(result.ToString(Formatting.None));
Console.Error.WriteLine($"Resolved {resolved} SVG symbols in {result.Count} libraries");

static HashSet<string> Colors(JToken token) => new HashSet<string>(
    (token as JArray)?.Values<string>().Select(SvgShapes.Normalize) ?? Enumerable.Empty<string>());
