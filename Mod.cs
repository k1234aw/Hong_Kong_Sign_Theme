using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using SignBuilder.Core;
using SignBuilder.Rendering;

namespace HKSignBuilderAddon
{
    /// <summary>Optional Hong Kong editor and renderer for the author's Sign Builder.</summary>
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger("HKSignBuilderAddon").SetShowsErrorsInUI(false);
        private static readonly FieldInfo BakerField = typeof(SignBuilderRuntime).GetField("s_Baker", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo BusyField = typeof(SignFaceBaker).GetField("m_Busy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Mod s_Instance;
        private SignFaceBaker m_Baker;
        private static string s_ResourcesDir;
        internal static string LibraryJson { get; private set; } = "{}";
        internal static int BaseFormat { get; private set; } = 1;

        public static bool Ready { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            try
            {
                // Accept later author builds that still read our theme and symbol formats.
                // If a required hook changes, keep the original renderer and editor untouched.
                var original = typeof(SignBuilderRuntime).Assembly;
                var formats = original.GetType("SignBuilder.Core.Formats", false);
                var symbols = original.GetType("SignBuilder.Core.SymbolLibraries", false);
                var themes = original.GetType("SignBuilder.Core.Themes", false);
                var v2 = symbols != null && FormatVersion(formats, "Theme") >= 2 &&
                    FormatVersion(formats, "Design") >= 2 && FormatVersion(formats, "Library") >= 1 &&
                    ConstantString(symbols, "ModFolder") == "SignBuilderSymbols";
                var v1 = symbols == null && FormatVersion(formats, "Theme") == 1 &&
                    FormatVersion(formats, "Design") == 1;
                if ((!v1 && !v2) ||
                    ConstantString(themes, "ModFolder") != "SignBuilderThemes" ||
                    !HKAddonUISystem.SupportsBaseEditor ||
                    BakerField?.FieldType != typeof(SignFaceBaker) || BakerField.IsInitOnly ||
                    BusyField?.FieldType != typeof(bool) ||
                    typeof(SignFaceBaker).GetConstructor(new[] { typeof(string), typeof(string) }) == null ||
                    typeof(SignFaceBaker).GetProperty("LastError")?.PropertyType != typeof(string))
                {
                    Log.Warn($"Sign Builder {original.GetName().Version} changed an API the Hong Kong add-on needs; the original mod stays active.");
                    return;
                }
                if (BakerField.GetValue(null) != null)
                {
                    Log.Warn("Sign Builder renderer is unavailable or already running; Hong Kong add-on stays inactive.");
                    return;
                }
                if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                {
                    Log.Warn("Hong Kong add-on path was not found; original Sign Builder remains active.");
                    return;
                }
                s_ResourcesDir = Path.Combine(Path.GetDirectoryName(asset.path), "Resources");
                var libraryFile = Path.Combine(s_ResourcesDir, "Renderer", "addon-libraries.json");
                if (!File.Exists(Path.Combine(s_ResourcesDir, "Renderer", "renderer.js")))
                {
                    Log.Warn("Hong Kong renderer is missing; original Sign Builder remains active.");
                    return;
                }
                LibraryJson = File.ReadAllText(libraryFile);

                // The original mod owns placement and save data. Replace only its baker instance,
                // before the first city-load warmup; no original files are changed on disk.
                string libraryVersion;
                using (var hash = SHA256.Create())
                    libraryVersion = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(LibraryJson))).Replace("-", "").Substring(0, 12);
                var cache = Path.Combine(UnityEngine.Application.persistentDataPath, "ModsData", "SignBuilderHK", "Faces", libraryVersion);
                m_Baker = new SignFaceBaker(s_ResourcesDir, cache);
                BakerField.SetValue(null, m_Baker);
                s_Instance = this;
                BaseFormat = v2 ? 2 : 1;
                Ready = true;
                updateSystem.UpdateAt<HKAddonUISystem>(SystemUpdatePhase.UIUpdate);
                RegisterHost();
                Log.Info($"Hong Kong sign add-on activated with Sign Builder format {(v2 ? 2 : 1)}");
            }
            catch (Exception e)
            {
                Ready = false;
                if (m_Baker != null && BakerField?.GetValue(null) == m_Baker) BakerField.SetValue(null, null);
                m_Baker?.Dispose();
                m_Baker = null;
                s_Instance = null;
                Log.Warn(e, "Hong Kong add-on did not activate; original Sign Builder remains active");
            }
        }

        private static int FormatVersion(Type formats, string name)
        {
            var field = formats?.GetField(name, BindingFlags.Public | BindingFlags.Static);
            var value = field == null ? null : field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
            return value is int version ? version : 0;
        }

        private static string ConstantString(Type owner, string name)
        {
            var field = owner?.GetField(name, BindingFlags.Public | BindingFlags.Static);
            return field != null && field.IsLiteral ? field.GetRawConstantValue() as string : null;
        }

        internal static void RegisterHost()
        {
            if (!Ready || s_ResourcesDir == null) return;
            try
            {
                GameManager.instance?.userInterface?.view?.uiSystem?.AddHostLocation("hksignbuilderaddon", s_ResourcesDir, shouldWatch: false);
            }
            catch (Exception e) { Log.Warn(e, "Hong Kong preview host could not be registered"); }
        }

        internal static void CheckHealth()
        {
            var instance = s_Instance;
            if (!Ready || instance?.m_Baker == null || instance.m_Baker.LastError == null) return;
            try
            {
                // Let a currently running bake finish before changing the shared baker.
                if (BusyField != null && (bool)BusyField.GetValue(instance.m_Baker)) return;
                Log.Warn("Hong Kong renderer failed: " + instance.m_Baker.LastError + "; restoring original Sign Builder renderer");
                instance.Deactivate();
            }
            catch (Exception e) { Log.Warn(e, "Could not check Hong Kong renderer health"); }
        }

        private void Deactivate()
        {
            Ready = false;
            try
            {
                if (m_Baker != null && BakerField?.GetValue(null) == m_Baker) BakerField.SetValue(null, null);
                m_Baker?.Dispose();
            }
            catch (Exception e) { Log.Warn(e, "Could not restore the original Sign Builder baker"); }
            m_Baker = null;
            s_Instance = null;
        }

        public void OnDispose()
        {
            Deactivate();
        }
    }
}
