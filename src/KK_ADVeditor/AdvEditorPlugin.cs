using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Studio;
using KKAPI.Utilities;
using Manager;
using UnityEngine;

namespace KK_ADVeditor
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    public class AdvEditorPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_ADVeditor";
        public const string PluginName = "ADV Scene Editor";
        public const string Version = "1.0";

        private const string DescrFileName = "CommandDescriptions.txt";

        internal static AdvEditorPlugin Instance;
        public static new ManualLogSource Logger { get; set; }

        private static Harmony _hi;
        private readonly AdvEditor _advEditor = new AdvEditor();

        internal static ConfigEntry<Rect> AddWinRect;
        internal static ConfigEntry<Rect> VarWinRect;
        internal static ConfigEntry<Rect> ListWinRect;
        internal static ConfigEntry<KeyboardShortcut> OpenShortcut;

        private void OnDestroy()
        {
#if DEBUG
            _hi?.UnpatchSelf();
#endif
            _watcher?.Dispose();

            // Only update saved descriptions if user used this plugin at all
            if (_commandDescriptions != null)
            {
                foreach (var commandInfo in AdvCommandInfo.AllCommands)
                    CommandDescriptions[commandInfo.CommandName] = commandInfo.Description.Trim();

                var vals = CommandDescriptions.Select(x => x.Key.PadRight(24) + ", " + x.Value.Replace("\r", "").Escape());
                var localPath = Path.Combine(Paths.ConfigPath, DescrFileName);
                File.Delete(localPath + ".bak");
                File.Move(localPath, localPath + ".bak");
                File.WriteAllLines(localPath, vals.ToArray());
            }
        }

        private static FileSystemWatcher _watcher;
        private static Dictionary<string, string> _commandDescriptions;
        public static Dictionary<string, string> CommandDescriptions
        {
            get
            {
                if (_commandDescriptions == null) ReadDescriptions();

                return _commandDescriptions;
            }
        }

        private void Awake()
        {
            if (StudioAPI.InsideStudio) throw new NotImplementedException("Shouldn't load in studio");

            Instance = this;
            Logger = base.Logger;


            //if (!TomlTypeConverter.CanConvert(typeof(Rect))) //todo remove and use one in kkapi
            //{
            //    TomlTypeConverter.AddConverter(typeof(Rect), new TypeConverter
            //    {
            //        ConvertToObject = (s, type) =>
            //        {
            //            var result = new Rect();
            //            if (s != null)
            //            {
            //                var cleaned = s.Trim('{', '}').Replace(" ", "");
            //                foreach (var part in cleaned.Split(','))
            //                {
            //                    var parts = part.Split(':');
            //                    if (parts.Length == 2 && float.TryParse(parts[1], out var value))
            //                    {
            //                        var id = parts[0].Trim('"');
            //                        if (id == "x") result.x = value;
            //                        else if (id == "y") result.y = value;
            //                        // Check z and w in case something was using Vector4 to serialize a Rect before
            //                        else if (id == "width" || id == "z") result.width = value;
            //                        else if (id == "height" || id == "w") result.height = value;
            //                    }
            //                }
            //            }
            //            return result;
            //        },
            //        ConvertToString = (o, type) =>
            //        {
            //            var rect = (Rect)o;
            //            return string.Format(CultureInfo.InvariantCulture,
            //                "{{ \"x\":{0}, \"y\":{1}, \"width\":{2}, \"height\":{3} }}",
            //                rect.x, rect.y, rect.width, rect.height);
            //        }
            //    });
            //}


            var windDescr = new ConfigDescription("Drag the window's bottom right corner to change these.", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            AddWinRect = Config.Bind("Windows", "Editor window rect", new Rect(10, 10, 500, 350), windDescr);
            ListWinRect = Config.Bind("Windows", "List window rect", new Rect(510, 10, 700, 350), windDescr);
            VarWinRect = Config.Bind("Windows", "Inspect window rect", new Rect(1220, 10, 370, 350), windDescr);

            OpenShortcut = Config.Bind("General", "Open ADV editor", new KeyboardShortcut(KeyCode.Pause, KeyCode.LeftShift));

            _hi = Harmony.CreateAndPatchAll(typeof(PreventAdvCrashHooks), GUID);

#if DEBUG
            if (Game.Instance?.actScene?.isEventNow == true)
                _advEditor.Enabled = true;
#endif
        }

        private static void ReadDescriptions()
        {
            var localPath = Path.Combine(Paths.ConfigPath, DescrFileName);

            if (!File.Exists(localPath))
            {
                var containingAssembly = typeof(AdvEditorPlugin).Assembly;
                var data = ResourceUtils.GetEmbeddedResource(DescrFileName, containingAssembly);
                File.WriteAllBytes(localPath, data);
            }

            if (_watcher == null)
            {
                _watcher = new FileSystemWatcher(Paths.ConfigPath, DescrFileName);
                _watcher.EnableRaisingEvents = true;
                _watcher.Changed += (sender, args) =>
                {
                    ReadDescriptions();
                    foreach (var commandInfo in AdvCommandInfo.AllCommands) commandInfo.UpdateDescription();
                };
            }

            Logger.LogInfo("Reading description file from " + localPath);
            var descrs = File.ReadAllLines(localPath);
            _commandDescriptions = descrs.Select(x => x.Split(new[] { ',' }, 2)).ToDictionary(x => x[0].Trim(), x => x[1].Trim().Unescape());
        }

        private void Update()
        {
            if (OpenShortcut.Value.IsDown())
                _advEditor.Enabled = !_advEditor.Enabled;
        }

        private void OnGUI()
        {
            _advEditor.ShowAdvEditor();
        }
    }
}
