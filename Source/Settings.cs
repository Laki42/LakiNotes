using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Settings;
using Zenject;
using UnityEngine;

namespace Laki
{
    public class LakiConfig
    {
        public virtual bool EnableLaki { get; set; } = true;
        public virtual bool EnableInMultiplayer { get; set; } = true;
        public virtual bool EnableEffects { get; set; } = true;
        public virtual bool EnableRareLaki { get; set; } = true;
        public virtual string TestMode { get; set; } = LakiTestMode.Off;
        // Persisted, deliberately not exposed to BSML.
        public virtual int SessionsWithoutSecret { get; set; }
    }

    internal sealed class SettingsMenu : IInitializable, IDisposable
    {
        private readonly BSMLSettings menu;
        private readonly LakiConfig config;
        private readonly LakiVisualFactory visuals;
        private readonly LakiState state;
        internal const string ResourceName = "Laki.Settings.bsml";
        [UIComponent("settings-root")] private RectTransform settingsRoot = null;
        [UIComponent("enabled-control")] private ToggleSetting enabledControl = null;
        [UIComponent("effects-control")] private ToggleSetting effectsControl = null;
        [UIComponent("rare-control")] private ToggleSetting rareControl = null;
        [UIComponent("multiplayer-control")] private ToggleSetting multiplayerControl = null;
        [UIComponent("test-mode-control")] private DropDownListSetting testModeControl = null;
        private SettingsRefresh refresh;
        private bool registered;
        public SettingsMenu(BSMLSettings menu, LakiConfig config, LakiVisualFactory visuals, LakiState state)
        { this.menu = menu; this.config = config; this.visuals = visuals; this.state = state; }
        [UIValue("enabled")] public bool Enabled { get => config.EnableLaki; set => config.EnableLaki = value; }
        [UIValue("multiplayer")] public bool Multiplayer { get => config.EnableInMultiplayer; set => config.EnableInMultiplayer = value; }
        [UIValue("effects")] public bool Effects { get => config.EnableEffects; set => config.EnableEffects = value; }
        [UIValue("rare")] public bool Rare { get => config.EnableRareLaki; set => config.EnableRareLaki = value; }
        [UIValue("test-mode-options")] public List<object> TestModeOptions { get; } = new List<object>
            { LakiTestMode.Off, LakiTestMode.ForceLaki, LakiTestMode.ForceSuper, LakiTestMode.ForceSecret };
        [UIValue("test-mode")] public string TestMode
        {
            get => LakiTestMode.TryKind(config.TestMode, out _) ? config.TestMode : LakiTestMode.Off;
            set => config.TestMode = LakiTestMode.TryKind(value, out _) ? value : LakiTestMode.Off;
        }
        public void Initialize()
        {
            try
            {
                ValidateResource();
                // BSML SettingsMenu.Setup owns the deferred Parse call and logs its full
                // exception as "Error adding settings menu ... (Laki Notes)" on failure.
                menu.AddSettingsMenu("Laki Notes", ResourceName, this);
                registered = true;
                state.TestModeConsumed += RefreshTestMode;
            }
            catch (Exception e) { Plugin.Log.Error("Laki Notes Settings UI initialization failure: " + e); }
            try { visuals.EnsureReady(); }
            catch (Exception e) { Plugin.Log.Warn("Laki Notes visual setup skipped: " + e); }
        }
        internal void ValidateResource()
        {
            string markup;
            using (var stream = typeof(SettingsMenu).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidDataException("Settings resource load failure: " + ResourceName);
                using (var reader = new StreamReader(stream)) markup = reader.ReadToEnd();
            }
            XDocument document;
            try { document = XDocument.Parse(markup); }
            catch (Exception e) { throw new InvalidDataException("BSML parse failure: " + ResourceName, e); }
            foreach (var element in document.Descendants())
            foreach (string attribute in new[] { "value", "options" })
            {
                string id = (string)element.Attribute(attribute);
                if (id == null) continue;
                var property = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .SingleOrDefault(p => p.GetCustomAttribute<UIValue>()?.Id == id);
                bool options = attribute == "options";
                if (property == null || !property.CanRead || (!options && !property.CanWrite) ||
                    (options && !(property.GetValue(this) is IList)))
                    throw new InvalidDataException("Binding failure: " + element.Name + " " + attribute + "=" + id);
            }
        }
        [UIAction("#post-parse")]
        private void PostParse()
        {
            try
            {
                var controls = new GenericSetting[] { enabledControl, effectsControl, rareControl, multiplayerControl, testModeControl };
                if (settingsRoot == null || controls.Any(c => c == null || c.AssociatedValue == null || !c.UpdateOnChange))
                    throw new InvalidOperationException("Binding failure: expected four bound toggles and one immediate dropdown.");
                if (testModeControl.Values == null || testModeControl.Values.Count != 4)
                    throw new InvalidOperationException("Binding failure: Test Mode requires four options.");
                foreach (var control in controls) control.ReceiveValue();
                // The settings container is hidden until all controls have parsed/bound.
                // A parser failure before this callback leaves partial controls disabled.
                settingsRoot.gameObject.SetActive(true);
                refresh = settingsRoot.gameObject.AddComponent<SettingsRefresh>();
                refresh.Refresh = RefreshControls;
                Plugin.Log.Info("Laki Notes Settings UI initialized: four toggles and Test Mode; resource=" + ResourceName);
            }
            catch (Exception e)
            {
                if (settingsRoot != null) settingsRoot.gameObject.SetActive(false);
                Plugin.Log.Error("Laki Notes Settings UI initialization / binding failure: " + e);
                throw; // BSML supplies its standard error page and logs the parse context.
            }
        }
        private void RefreshControls()
        {
            try
            {
                enabledControl.ReceiveValue(); effectsControl.ReceiveValue();
                rareControl.ReceiveValue(); multiplayerControl.ReceiveValue();
                testModeControl.ReceiveValue();
            }
            catch (Exception e)
            {
                if (settingsRoot != null) settingsRoot.gameObject.SetActive(false);
                Plugin.Log.Error("Laki Notes Settings UI synchronization failure: " + e);
            }
        }
        private void RefreshTestMode()
        {
            // A cached menu must also show Off after one-shot consumption.
            try { if (testModeControl != null) testModeControl.ReceiveValue(); }
            catch (Exception e) { Plugin.Log.Error("Laki Notes Test Mode binding refresh failure: " + e); }
        }
        public void Dispose()
        {
            state.TestModeConsumed -= RefreshTestMode;
            if (refresh != null) refresh.Refresh = null;
            if (registered) menu.RemoveSettingsMenu(this);
        }
    }
    internal sealed class SettingsRefresh : MonoBehaviour
    {
        internal Action Refresh;
        private void OnEnable() { Refresh?.Invoke(); }
    }
}
