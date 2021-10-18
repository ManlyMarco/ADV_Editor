using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    public class AdvEditorPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_ADVeditor";
        public const string PluginName = "ADV Scene Editor";
        public const string Version = "1.2";

        private const string DescrFileName = "CommandDescriptions.txt";

        internal static AdvEditorPlugin Instance;
        public static new ManualLogSource Logger { get; set; }

        private static Harmony _hi;
        private readonly AdvEditor _advEditor = new AdvEditor();

        internal static ConfigEntry<Rect> AddWinRect;
        internal static ConfigEntry<Rect> VarWinRect;
        internal static ConfigEntry<Rect> ListWinRect;
        internal static ConfigEntry<KeyboardShortcut> OpenShortcut;
        internal static ConfigEntry<bool> SolidBackground;

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

            var windDescr = new ConfigDescription("Drag bottom right corner of the window to change these.", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            AddWinRect = Config.Bind("Windows", "Editor window rect", new Rect(10, 10, 500, 350), windDescr);
            ListWinRect = Config.Bind("Windows", "List window rect", new Rect(510, 10, 700, 350), windDescr);
            VarWinRect = Config.Bind("Windows", "Inspect window rect", new Rect(1220, 10, 370, 350), windDescr);
            SolidBackground = Config.Bind("Windows", "Solid window background", true, "Make window background solid for easier reading. If false, windows are transparent.");

            OpenShortcut = Config.Bind("General", "Open ADV editor", new KeyboardShortcut(KeyCode.Pause, KeyCode.LeftShift));

            _hi = Harmony.CreateAndPatchAll(typeof(PreventAdvCrashHooks), GUID);

#if DEBUG
#if KK
            if (Manager.Game.Instance?.actScene?.isEventNow == true)
                _advEditor.Enabled = true;
#elif KKS
            if (ActionScene.initialized && ActionScene.instance.isEventNow)
                _advEditor.Enabled = true;
#endif
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
