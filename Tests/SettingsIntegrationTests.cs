using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml.Linq;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Macros;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Tags;
using BeatSaberMarkupLanguage.TypeHandlers;
using BeatSaberMarkupLanguage.TypeHandlers.Settings;

internal static class SettingsIntegrationTests
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string game, pluginPath;
    private static int count;
    private static void Check(bool value, string message)
    { count++; if (!value) throw new Exception(message); }
    public static int Main(string[] args)
    {
        game = args[0]; pluginPath = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
            string file = new AssemblyName(request.Name).Name + ".dll";
            foreach (var folder in new[] { "Beat Saber_Data/Managed", "Plugins", "Libs" })
            { string path = Path.Combine(game, folder, file); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try { Run(); Console.WriteLine("PASS: actual BSML settings integration / " + count + " assertions"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        Check(typeof(BSMLParser).Assembly.GetName().Version == new Version(1,12,5,0), "Actual BSML 1.12.5 required");
        var plugin = Assembly.LoadFrom(pluginPath);
        var configType = plugin.GetType("Laki.LakiConfig", true);
        var config = Activator.CreateInstance(configType);
        configType.GetProperty("SessionsWithoutSecret").SetValue(config, 73);
        var stateType = plugin.GetType("Laki.LakiState", true);
        var state = Activator.CreateInstance(stateType, new[] { config });
        var hostType = plugin.GetType("Laki.SettingsMenu", true);
        var host = Activator.CreateInstance(hostType, All, null, new[] { null, config, null, state }, null);
        hostType.GetMethod("ValidateResource", All).Invoke(host, null);
        var configParameter = plugin.GetType("Laki.Plugin").GetConstructors().Single().GetParameters().Single(p => p.Name == "config");
        var nameAttribute = configParameter.GetCustomAttributes(false).Single(a => a.GetType().FullName == "IPA.Config.Config+NameAttribute");
        Check((string)nameAttribute.GetType().GetProperty("Name").GetValue(nameAttribute) == "Laki", "Config filename pinned despite renamed manifest");
        string resource;
        using (var stream = plugin.GetManifestResourceStream("Laki.Settings.bsml"))
        using (var reader = new StreamReader(stream)) resource = reader.ReadToEnd();
        var document = XDocument.Parse(resource);
        Check(document.Root.Name == "settings-container", "Settings scroll/layout container");
        Check(document.Descendants("toggle-setting").Count() == 4, "Four toggles");
        Check(document.Descendants("dropdown-list-setting").Count() == 1, "One dropdown");

        // Execute the real parser's attribute normalization and the real BSML handlers.
        // Managed-only setting shells deliberately do not create Unity UI objects.
        var tags = new List<BSMLTag> { new ScrollableSettingsContainerTag(), new TextTag(), new ToggleSettingTag(),
            (BSMLTag)Activator.CreateInstance(typeof(BSMLParser).Assembly.GetType("BeatSaberMarkupLanguage.Tags.Settings.DropdownListSettingTag", true)) };
        var parser = (BSMLParser)Activator.CreateInstance(typeof(BSMLParser), All, null,
            new object[] { tags, new List<BSMLMacro>(), new List<TypeHandler>() }, null);
        var parameters = (BSMLParserParams)Activator.CreateInstance(typeof(BSMLParserParams), All, null, new[] { host }, null);
        foreach (var property in hostType.GetProperties(All))
        {
            var attribute = property.GetCustomAttribute<UIValue>();
            if (attribute != null) parameters.Values.Add(attribute.Id, new BSMLPropertyValue(host, property, true));
        }
        Check(ReferenceEquals(parameters.Host, host), "Actual SettingsMenu host");
        var generic = new GenericSettingHandler();
        var dropdownHandler = new DropDownListSettingHandler();
        var getParameters = typeof(BSMLParser).GetMethod("GetParameters", All);
        Func<XElement, TypeHandler, Dictionary<string,string>> parseAttributes = (element, handler) => {
            object[] args = { element, handler.Props, parameters, null };
            return (Dictionary<string,string>)getParameters.Invoke(parser, args);
        };
        Check(parseAttributes(document.Root, new RectTransformHandler())["active"] == "false", "Real parser resolves fail-closed root visibility");
        string[] expectedOptions = { "Off", "Force Laki", "Force Super Laki", "Force Secret Laki" };
        var mappings = new Dictionary<string,string> { {"enabled","EnableLaki"}, {"effects","EnableEffects"},
            {"rare","EnableRareLaki"}, {"multiplayer","EnableInMultiplayer"}, {"test-mode","TestMode"} };
        foreach (var element in document.Descendants())
        {
            Check(tags.Any(tag => tag.Aliases.Contains(element.Name.LocalName)), "Real tag alias: " + element.Name);
            if (element.Attribute("value") == null) continue;
            bool dropdown = element.Name == "dropdown-list-setting";
            var setting = (GenericSetting)FormatterServices.GetUninitializedObject(dropdown ? typeof(DropDownListSetting) : typeof(ToggleSetting));
            var data = parseAttributes(element, generic);
            generic.HandleType(new BSMLParser.ComponentTypeWithData { Component = setting, Data = data }, parameters);
            Check(setting.UpdateOnChange && setting.AssociatedValue != null, "Actual immediate binding: " + data["value"]);
            var configProperty = configType.GetProperty(mappings[data["value"]]);
            if (dropdown)
            {
                var control = (DropDownListSetting)setting;
                dropdownHandler.HandleType(new BSMLParser.ComponentTypeWithData { Component = setting, Data = parseAttributes(element, dropdownHandler) }, parameters);
                Check(control.Values.GetType() == typeof(List<object>), "Actual options type List<object>");
                Check(control.Values is IList && control.Values.Cast<string>().SequenceEqual(expectedOptions), "IList + four exact options");
                for (int i = 0; i < 4; i++)
                {
                    typeof(DropDownListSetting).GetField("index", All).SetValue(control, i);
                    control.ApplyValue();
                    Check((string)configProperty.GetValue(config) == expectedOptions[i], "Real dropdown ApplyValue: " + expectedOptions[i]);
                    if (i == 0) continue;
                    stateType.GetMethod("TryBegin").Invoke(state, new object[] { false, true });
                    Check(!(bool)stateType.GetMethod("ConsumeTest").Invoke(state, new object[] { expectedOptions[i], false }), "Failed target does not consume");
                    Check((bool)stateType.GetMethod("ConsumeTest").Invoke(state, new object[] { expectedOptions[i], true }), "Successful target consumes");
                    Check((string)control.AssociatedValue.GetValue() == "Off", "Binding reads Off after one-shot");
                    stateType.GetMethod("SecretAppeared").Invoke(state, null);
                    stateType.GetMethod("End").Invoke(state, null);
                    Check((int)configType.GetProperty("SessionsWithoutSecret").GetValue(config) == 73, "Pity unchanged");
                }
            }
            else
            {
                foreach (bool value in new[] { false, true })
                {
                    typeof(ToggleSetting).GetField("currentValue", All).SetValue(setting, value);
                    setting.ApplyValue();
                    Check((bool)configProperty.GetValue(config) == value, "Real toggle ApplyValue: " + data["value"]);
                    configProperty.SetValue(config, !value);
                    Check((bool)setting.AssociatedValue.GetValue() == !value, "Reverse Config binding: " + data["value"]);
                }
            }
        }
        var broken = new XElement("toggle-setting", new XAttribute("value", "missing-binding"));
        try {
            generic.HandleType(new BSMLParser.ComponentTypeWithData {
                Component = (ToggleSetting)FormatterServices.GetUninitializedObject(typeof(ToggleSetting)), Data = parseAttributes(broken, generic) }, parameters);
            throw new Exception("Missing binding unexpectedly accepted");
        } catch (ValueNotFoundException) { Check(true, "Real BSML rejects unknown binding"); }
        Console.WriteLine("Actual DLL resource -> XDocument -> BSMLParser.GetParameters -> GenericSettingHandler / DropDownListSettingHandler -> ApplyValue -> actual Config: PASS");
        Console.WriteLine("No Unity native UI creation, full BSMLParser.Parse scene execution, layout rendering, or VR test was performed.");
    }
}
