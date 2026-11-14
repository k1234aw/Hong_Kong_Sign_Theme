// Adapted from Andy Smith's Sign Builder (MIT license) to build the add-on symbol catalog.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace SignBuilder.Core
{
    public static class SvgShapes
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static (JArray viewBox, JArray parts) Read(string path, HashSet<string> legend)
        {
            var root = XDocument.Load(path).Root ?? throw new InvalidDataException("empty file");
            var box = Numbers((string)root.Attribute("viewBox"));
            if (box.Length != 4) box = new[] { 0, 0, Number((string)root.Attribute("width")), Number((string)root.Attribute("height")) };
            var parts = new JArray();
            Walk(root, Identity, Style(root, "fill") ?? "#000000", Style(root, "fill-rule") ?? "nonzero", legend, parts);
            return (new JArray(box.Select(v => (object)Round(v)).ToArray()), parts);
        }

        private static readonly double[] Identity = { 1, 0, 0, 1, 0, 0 };

        private static void Walk(XElement element, double[] matrix, string fill, string rule, HashSet<string> legend, JArray parts)
        {
            foreach (var child in element.Elements())
            {
                var m = Multiply(matrix, Transform((string)child.Attribute("transform")));
                var f = Style(child, "fill") ?? fill;
                var r = Style(child, "fill-rule") ?? rule;
                var d = PathOf(child);
                if (child.Name.LocalName == "g" || child.Name.LocalName == "svg") Walk(child, m, f, r, legend, parts);
                else if (d != null && f != "none")
                {
                    var part = new JObject { ["d"] = d, ["fill"] = legend.Contains(Normalize(f)) ? "currentColor" : f };
                    if (!m.SequenceEqual(Identity)) part["transform"] = "matrix(" + string.Join(" ", m.Select(v => Round(v).ToString(Inv))) + ")";
                    if (r == "evenodd") part["rule"] = "evenodd";
                    parts.Add(part);
                }
            }
        }

        private static string PathOf(XElement e)
        {
            double A(string n) => Number((string)e.Attribute(n));
            switch (e.Name.LocalName)
            {
                case "path": return (string)e.Attribute("d");
                case "rect":
                    return $"M{F(A("x"))} {F(A("y"))}H{F(A("x") + A("width"))}V{F(A("y") + A("height"))}H{F(A("x"))}Z";
                case "circle":
                case "ellipse":
                    var rx = e.Name.LocalName == "circle" ? A("r") : A("rx");
                    var ry = e.Name.LocalName == "circle" ? A("r") : A("ry");
                    return $"M{F(A("cx") - rx)} {F(A("cy"))}A{F(rx)} {F(ry)} 0 1 0 {F(A("cx") + rx)} {F(A("cy"))}A{F(rx)} {F(ry)} 0 1 0 {F(A("cx") - rx)} {F(A("cy"))}Z";
                case "polygon":
                    var p = Numbers((string)e.Attribute("points"));
                    return p.Length < 4 ? null : "M" + string.Join("L", Enumerable.Range(0, p.Length / 2).Select(i => F(p[2 * i]) + " " + F(p[2 * i + 1]))) + "Z";
                default: return null;
            }
        }

        /// <summary>A presentation property from the element's style attribute, or the attribute itself.</summary>
        private static string Style(XElement e, string name)
        {
            var style = (string)e.Attribute("style");
            if (style != null)
            {
                foreach (var decl in style.Split(';'))
                {
                    var i = decl.IndexOf(':');
                    if (i > 0 && decl.Substring(0, i).Trim() == name) return decl.Substring(i + 1).Trim();
                }
            }
            return (string)e.Attribute(name);
        }

        /// <summary>A color as "#rrggbb" (from #rgb, rgb(r, g, b), white or black), so fills and lists compare.</summary>
        public static string Normalize(string color)
        {
            var c = (color ?? "").Replace(" ", "").ToLowerInvariant();
            if (c == "white") return "#ffffff";
            if (c == "black") return "#000000";
            if (Regex.IsMatch(c, "^#[0-9a-f]{3}$")) return "#" + string.Concat(c.Substring(1).Select(ch => new string(ch, 2)));
            var rgb = Regex.Match(c, @"^rgb\((\d+),(\d+),(\d+)\)$");
            if (rgb.Success) return "#" + string.Concat(Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value).ToString("x2")));
            return c;
        }

        private static double[] Transform(string t)
        {
            var m = Identity;
            if (string.IsNullOrEmpty(t)) return m;
            foreach (Match f in Regex.Matches(t, @"(matrix|translate|scale|rotate)\s*\(([^)]*)\)"))
            {
                var v = Numbers(f.Groups[2].Value);
                double[] next;
                switch (f.Groups[1].Value)
                {
                    case "matrix": next = v.Length == 6 ? v : Identity; break;
                    case "translate": next = new[] { 1, 0, 0, 1, v.ElementAtOrDefault(0), v.ElementAtOrDefault(1) }; break;
                    case "scale": next = new[] { v.ElementAtOrDefault(0), 0, 0, v.Length > 1 ? v[1] : v.ElementAtOrDefault(0), 0, 0 }; break;
                    default:
                        var a = v.ElementAtOrDefault(0) * Math.PI / 180;
                        double cx = v.ElementAtOrDefault(1), cy = v.ElementAtOrDefault(2), cos = Math.Cos(a), sin = Math.Sin(a);
                        next = new[] { cos, sin, -sin, cos, cx - cos * cx + sin * cy, cy - sin * cx - cos * cy };
                        break;
                }
                m = Multiply(m, next);
            }
            return m;
        }

        /// <summary>Applies <paramref name="b"/> first, then <paramref name="a"/> (SVG matrices a b c d e f).</summary>
        private static double[] Multiply(double[] a, double[] b) => new[]
        {
            a[0] * b[0] + a[2] * b[1], a[1] * b[0] + a[3] * b[1],
            a[0] * b[2] + a[2] * b[3], a[1] * b[2] + a[3] * b[3],
            a[0] * b[4] + a[2] * b[5] + a[4], a[1] * b[4] + a[3] * b[5] + a[5],
        };

        private static double[] Numbers(string s) => s == null ? new double[0]
            : Regex.Matches(s, @"-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?").Cast<Match>().Select(m => double.Parse(m.Value, Inv)).ToArray();

        private static double Number(string s) => Numbers(s?.Replace("px", "")).FirstOrDefault();

        private static double Round(double v) => Math.Round(v, 4);

        private static string F(double v) => Round(v).ToString(Inv);
    }
}
