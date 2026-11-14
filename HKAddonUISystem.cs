using System;
using System.Reflection;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using SignBuilder.Systems;

namespace HKSignBuilderAddon
{
    public sealed partial class HKAddonUISystem : UISystemBase
    {
        private const string Group = "HKSignBuilderAddon";
        private static readonly FieldInfo BaseOpenField = typeof(SignBuilderUISystem).GetField("m_EditorOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BaseEditingField = typeof(SignBuilderUISystem).GetField("m_EditingDesign", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static bool SupportsBaseEditor =>
            BaseOpenField?.FieldType == typeof(ValueBinding<bool>) &&
            BaseEditingField?.FieldType == typeof(ValueBinding<string>) &&
            typeof(SignBuilderUISystem).GetProperty("EditorOpen")?.PropertyType == typeof(bool) &&
            typeof(SignBuilderUISystem).GetMethod("RefreshLibrary", BindingFlags.Instance | BindingFlags.Public) != null &&
            typeof(SignBuilderUISystem).GetField("Group", BindingFlags.Static | BindingFlags.Public)?.GetRawConstantValue() as string == "SignBuilder";
        private ValueBinding<bool> m_Open;
        private string m_LastEditing;
        private bool m_UiReady;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(m_Open = new ValueBinding<bool>(Group, "editorOpen", false));
            AddBinding(new ValueBinding<string>(Group, "libraries", Mod.LibraryJson));
            AddBinding(new ValueBinding<int>(Group, "formatVersion", Mod.BaseFormat));
            AddBinding(new TriggerBinding<bool>(Group, "setEditorOpen", SetOpen));
            AddBinding(new TriggerBinding(Group, "editorReady", () => m_UiReady = true));
        }

        private void SetOpen(bool open)
        {
            if (!Mod.Ready) return;
            try
            {
                if (open) World.GetOrCreateSystemManaged<SignBuilderUISystem>().RefreshLibrary();
                m_Open.Update(open);
            }
            catch (Exception e) { Mod.Log.Warn(e, "Hong Kong editor could not open; original editor remains available"); }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Mod.CheckHealth();
            if (!Mod.Ready) { if (m_Open.value) m_Open.Update(false); return; }
            if (!m_UiReady || BaseOpenField == null || BaseEditingField == null) return;
            try
            {
                var original = World.GetExistingSystemManaged<SignBuilderUISystem>();
                if (original == null || !original.EditorOpen) return;
                var editing = (BaseEditingField.GetValue(original) as ValueBinding<string>)?.value;
                // The author's toolbar now controls this editor. A second click closes it;
                // a new edit payload opens the newly selected sign instead.
                var originalOpen = BaseOpenField.GetValue(original) as ValueBinding<bool>;
                originalOpen?.Update(false);
                if (m_Open.value && editing == m_LastEditing) m_Open.Update(false);
                else
                {
                    m_LastEditing = editing;
                    m_Open.Update(true);
                }
            }
            catch (Exception e) { Mod.Log.Warn(e, "Hong Kong edit handoff failed; original editor remains available"); }
        }

        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Mod.RegisterHost();
        }
    }
}
