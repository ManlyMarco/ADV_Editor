using System;
using System.CodeDom;
using System.Globalization;
using System.Linq;
using ADV;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Utilities;
using Microsoft.CSharp;
using RuntimeUnityEditor.Core.Inspector.Entries;
using UnityEngine;

namespace KK_ADVeditor
{
    public class AdvEditor
    {
        private TextScenario _currentScenario;
        private TextScenario CurrentScenario => _currentScenario != null ? _currentScenario : _currentScenario = Manager.Game.Instance?.actScene?.AdvScene?.Scenario;

        public bool Enabled { get; set; }

        public void ShowAdvEditor()
        {
            if (!Enabled) return;
            if (CurrentScenario == null)
            {
                Enabled = false;
                return;
            }

            _addWinRect = GUILayout.Window(4207123, _addWinRect, TesterWindow, "ADV Command Tester");
            _listWinRect = GUILayout.Window(4207124, _listWinRect, CommandListWindow, "ADV Command List");
            _varWinRect = GUILayout.Window(4207125, _varWinRect, VariableWindow, "ADV Inspector");
        }

        #region List window

        private Rect _listWinRect
        {
            get => AdvEditorPlugin.ListWinRect.Value;
            set => AdvEditorPlugin.ListWinRect.Value = value;
        }
        private string _searchStrList = "";
        private bool _searchListHideUnimportant = true;
        private bool _searchListOnlyText;

        private int _previousLine;
        private bool _gotoListClicked;
        private bool _autoScrollToCurrent = true;

        private void CommandListWindow(int id)
        {
            bool FilterPack(ScenarioData.Param x)
            {
                if (_searchStrList != "" &&
                    x.Command.ToString().IndexOf(_searchStrList, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (x.Args == null || !x.Args.Any(y => y.IndexOf(_searchStrList, StringComparison.OrdinalIgnoreCase) >= 0)))
                    return false;

                if (_searchListHideUnimportant &&
                    (x.Command == Command.VAR && x.Args.SafeGet(1).StartsWith("P_", StringComparison.Ordinal) ||
                     x.Command == Command.Replace && x.Args.SafeGet(0).StartsWith("P", StringComparison.Ordinal)))
                    return false;

                if (_searchListOnlyText &&
                    x.Command != Command.Text)
                    return false;

                return true;
            }

            var scenario = CurrentScenario;

            if (scenario == null || scenario.CommandPacks.IsNullOrEmpty())
            {
                GUILayout.Label("Nothing to show at this time. Trigger an ADV scene to see its commands.");
                return;
            }

            var commands = scenario.CommandPacks.Where(FilterPack);

            var commandPacksCount = scenario.CommandPacks.Count;
            //if (commandPacksCount == 0)
            //{
            //    Enabled = false;
            //    return;
            //}

            var currentLine = Mathf.Clamp(scenario.CurrentLine, 0, commandPacksCount - 1);
            var currentPack = commandPacksCount == 0 ? null : scenario.CommandPacks[currentLine];

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Label("Search:", GUILayout.ExpandWidth(false));

                        _searchStrList = GUILayout.TextField(_searchStrList, GUILayout.ExpandWidth(true));

                        _searchListHideUnimportant = GUILayout.Toggle(_searchListHideUnimportant, "Hide spam");
                        _searchListOnlyText = GUILayout.Toggle(_searchListOnlyText, "Only Text");
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Label(" Current line:", GUILayout.ExpandWidth(false));

                        GUI.changed = false;
                        var newLine = GUILayout.TextField(scenario.CurrentLine.ToString(), GUILayout.Width(35));
                        if (GUI.changed && int.TryParse(newLine, out var result))
                            scenario.CurrentLine = Mathf.Clamp(result + 1, 0, scenario.CommandPacks.Count - 1);

                        GUILayout.Label(" / ", GUILayout.ExpandWidth(false));
                        GUILayout.Label((scenario.CommandPacks.Count - 1).ToString(), GUILayout.Width(31));

                        //if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
                        //{
                        //    scenario.Jump(scenario.CurrentLine);
                        //}
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Label("Export current:", GUILayout.ExpandWidth(false));
                        if (GUILayout.Button("Code", GUILayout.ExpandWidth(false)))
                        {
                            GUIUtility.systemCopyBuffer = WriteToCode(currentPack);
                            Logger.LogMessage("Copied code of selected command to clipboard");
                        }
                        if (GUILayout.Button("CSV", GUILayout.ExpandWidth(false)))
                        {
                            GUIUtility.systemCopyBuffer = WriteToCsv(currentPack);
                            Logger.LogMessage("Copied CSV of selected command to clipboard");
                        }

                        GUILayout.Label("Export all:", GUILayout.ExpandWidth(false));
                        if (GUILayout.Button("Code", GUILayout.ExpandWidth(false)))
                        {
                            var result = string.Join("\r\n", commands.Select(WriteToCode).ToArray());
                            GUIUtility.systemCopyBuffer = result;
                            Logger.LogMessage("Copied code of visible commands to clipboard");
                        }
                        if (GUILayout.Button("CSV", GUILayout.ExpandWidth(false)))
                        {
                            var result = "Command,Multi,Args\r\n\r\n" + string.Join("\r\n", commands.Select(WriteToCsv).ToArray());
                            GUIUtility.systemCopyBuffer = result;
                            Logger.LogMessage("Copied CSV of visible commands to clipboard");
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        _autoScrollToCurrent = GUILayout.Toggle(_autoScrollToCurrent, "Scroll to current");
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndHorizontal();

                _cmdListScrollPos = GUILayout.BeginScrollView(_cmdListScrollPos, false, true);
                {
                    foreach (var commandInfo in commands)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            var commandIndex = scenario.CommandPacks.IndexOf(commandInfo);
                            var cmd = AdvCommandInfo.TryGetCommand(commandInfo.Command);

                            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                            {
                                // If the removed command is before current line, adjust current line to still point to the same command
                                if (commandIndex < currentLine)
                                    scenario.CurrentLine = currentLine; // CurrentScenario.CurrentLine has -1 in getter but not in setter

                                scenario.CommandPacks.RemoveAt(commandIndex);
                                GUILayout.EndHorizontal(); // Avoid layout errors
                                break;
                            }

                            if (currentPack == commandInfo)
                            {
                                GUI.color = Color.green;

                                // GetLastRect only works during repaint
                                if (Event.current.type == EventType.repaint && _previousLine != currentLine)
                                {
                                    _previousLine = currentLine;
                                    if (!_gotoListClicked && _autoScrollToCurrent)
                                    {
                                        var lastRect = GUILayoutUtility.GetLastRect();
                                        // Make the element appear to fill most of the window so it ends up in the middle of the window not at the edge
                                        lastRect.height = _listWinRect.height - 100;
                                        lastRect.y -= lastRect.height / 2;
                                        GUI.ScrollTo(lastRect);
                                    }
                                    _gotoListClicked = false;
                                }
                            }

                            if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                            {
                                scenario.CurrentLine = commandIndex + 1;
                                _gotoListClicked = true;
                            }

                            GUILayout.Label(commandIndex.ToString(), GUILayout.Width(22));
                            GUILayout.Label(cmd.CommandName, GUILayout.Width(60));
                            GUI.color = Color.white;

                            GUI.changed = false;
                            GUILayout.Label("Multi", GUILayout.ExpandWidth(false));
                            GUILayout.Toggle(commandInfo.Multi, "", GUILayout.ExpandWidth(false));
                            if (GUI.changed)
                                Traverse.Create(commandInfo).Field<bool>("_multi").Value = !commandInfo.Multi;

                            // Sometimes the Args array might be smaller than actual number of args if their values are not set
                            var count = Mathf.Max(cmd.ArgsLabel.Length, commandInfo.Args.Length);
                            for (var index = 0; index < count; index++)
                            {
                                GUILayout.Label(cmd.ArgsLabel.SafeGet(index) ?? "???", GUILayout.Width(50));
                                GUI.changed = false;
                                var newVal = GUILayout.TextField(commandInfo.Args.SafeGet(index) ?? "", GUILayout.Width(90));
                                if (GUI.changed)
                                {
                                    // Expand the args array if necessary
                                    if (index >= commandInfo.Args.Length)
                                    {
                                        var addCount = count - commandInfo.Args.Length;
                                        ExpandArgsArray(commandInfo, addCount);
                                        Logger.LogInfo("Expanding args array");
                                    }

                                    commandInfo.Args[index] = newVal;
                                }
                            }

                            if (GUILayout.Button("+", GUILayout.ExpandWidth(false)))
                                ExpandArgsArray(commandInfo, 1);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();

            }
            GUILayout.EndVertical();

            _listWinRect = IMGUIUtils.DragResizeEatWindow(id, _listWinRect);
        }

        private static string WriteToCode(ScenarioData.Param param)
        {
            var argStr = string.Join(", ", UnconvertArgs(param).Select(y => '"' + y + '"').ToArray());
            if (argStr.Length > 0) argStr = ", " + argStr;
            return
                $"list.Add(Program.Transfer.Create({param.Multi.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}, Command.{param.Command}{argStr}));";
        }

        private static string WriteToCsv(ScenarioData.Param param)
        {
            var argStr = string.Join(",", UnconvertArgs(param).Select(y => '"' + y + '"').ToArray());
            if (argStr.Length > 0) argStr = "," + argStr;
            return $"\"{param.Command}\",\"{param.Multi}\"{argStr}";
        }

        private static string[] UnconvertArgs(ScenarioData.Param param)
        {
            if (param.Command == Command.VAR || param.Command == Command.RandomVar)
            {
                try
                {
                    var copy = param.Args.ToArray();
                    var t = Type.GetType(param.Args[0]);

                    if (t == typeof(int)) copy[0] = "int";
                    else if (t == typeof(float)) copy[0] = "float";
                    else if (t == typeof(string)) copy[0] = "string";
                    else if (t == typeof(bool)) copy[0] = "bool";

                    return copy;
                }
                catch (Exception e)
                {
                    AdvEditorPlugin.Logger.LogWarning($"Could not unconvert arguments of {param.Command} - {string.Join(", ", param.Args)} because of exception: {e}");
                }
            }

            return param.Args;
        }

        private static void ExpandArgsArray(ScenarioData.Param commandInfo, int addCount)
        {
            Traverse.Create(commandInfo).Field<string[]>("_args").Value =
                commandInfo.Args.Concat(Enumerable.Repeat("", addCount)).ToArray();
        }

        #endregion

        #region Tester window

        private Rect _addWinRect
        {
            get => AdvEditorPlugin.AddWinRect.Value;
            set => AdvEditorPlugin.AddWinRect.Value = value;
        }
        private AdvCommandInfo _currentCommandInfo;
        private int _additionalArgCount;
        private readonly string[] _argValues = new string[69];
        private bool _isMulti;
        private Vector2 _leftScrollPos;
        private Vector2 _rightScrollPos;
        private Vector2 _cmdListScrollPos;
        private string _searchStr = "";

        private void TesterWindow(int id)
        {
            var scenario = CurrentScenario;

            if (_currentCommandInfo == null)
            {
                _currentCommandInfo = AdvCommandInfo.AllCommands.First();
                ResetArgValues();
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(210));
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Search:", GUILayout.ExpandWidth(false));

                        _searchStr = GUILayout.TextField(_searchStr);
                    }
                    GUILayout.EndHorizontal();

                    _leftScrollPos = GUILayout.BeginScrollView(_leftScrollPos, false, true);
                    {
                        var commands = string.IsNullOrEmpty(_searchStr) ?
                            AdvCommandInfo.AllCommands :
                            AdvCommandInfo.AllCommands.Where(x => x.CommandName.IndexOf(_searchStr, StringComparison.OrdinalIgnoreCase) >= 0);
                        foreach (var commandInfo in commands)
                        {
                            if (_currentCommandInfo == commandInfo)
                                GUI.color = Color.green;

                            if (GUILayout.Button(commandInfo.CommandName))
                            {
                                _currentCommandInfo = commandInfo; //todo reset other stuff?
                                _additionalArgCount = 0;

                                ResetArgValues();
                                _rightScrollPos = Vector2.zero;
                            }
                            GUI.color = Color.white;
                        }
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Current command: ");
                            GUILayout.Label(_currentCommandInfo.CommandName);
                        }
                        GUILayout.EndHorizontal();

                        _rightScrollPos = GUILayout.BeginScrollView(_rightScrollPos, false, true);
                        {
                            for (var i = 0; i < _currentCommandInfo.ArgsLabel.Length + _additionalArgCount; i++)
                            {
                                var argLab = _currentCommandInfo.ArgsLabel.SafeGet(i) ?? "";

                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label(argLab, GUILayout.Width(60));

                                    _argValues[i] = GUILayout.TextField(_argValues[i], GUILayout.ExpandWidth(true));

                                    //if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                                    //{
                                    //    var argDef = _currentCommandInfo.ArgsDefault.Length < i ? _currentCommandInfo.ArgsDefault[i] : null;
                                    //    _argValues[i] = argDef ?? "";
                                    //}
                                }
                                GUILayout.EndHorizontal();
                            }

                            if (GUILayout.Button("Add arg"))
                            {
                                _additionalArgCount++;
                                _argValues[_currentCommandInfo.ArgsLabel.Length - 1 + _additionalArgCount] = "";
                            }

                            _currentCommandInfo.Description = GUILayout.TextArea(_currentCommandInfo.Description, GUI.skin.label);
                        }
                        GUILayout.EndScrollView();
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        _isMulti = GUILayout.Toggle(_isMulti, "Multi");

                        void AddCommand(bool isNext)
                        {
                            scenario.CommandAdd(isNext, scenario.CurrentLine + 1,
                                _isMulti,
                                _currentCommandInfo.Command,
                                _argValues.Take(_currentCommandInfo.ArgsLabel.Length + _additionalArgCount).ToArray());
                        }
                        if (GUILayout.Button("Add")) AddCommand(false);
                        if (GUILayout.Button("Add and Execute")) AddCommand(true);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            _addWinRect = IMGUIUtils.DragResizeEatWindow(id, _addWinRect);
        }

        private void ResetArgValues()
        {
            for (var i = 0; i < _currentCommandInfo.ArgsLabel.Length; i++)
            {
                if (string.Equals(_currentCommandInfo.ArgsLabel[i], "No", StringComparison.OrdinalIgnoreCase))
                    _argValues[i] = "0";
                else
                {
                    var argDef = _currentCommandInfo.ArgsDefault != null && _currentCommandInfo.ArgsDefault.Length > i ? _currentCommandInfo.ArgsDefault[i] : null;
                    _argValues[i] = argDef ?? "";
                }
            }
        }

        #endregion

        #region Variable window

        private Rect _varWinRect
        {
            get => AdvEditorPlugin.VarWinRect.Value;
            set => AdvEditorPlugin.VarWinRect.Value = value;
        }
        private int _varTab;
        private Vector2 _varScrollPos;
        private string _varEditingKey;
        private string _varEditingValue;
        private string _searchVarStr = "";

        private void VariableWindow(int id)
        {
            var scenario = CurrentScenario;

            if (scenario == null || scenario.commandController == null)
            {
                GUILayout.Label("No scenario loaded");
                return;
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Inspect: ");
                if (GUILayout.Button("Manager.Game"))
                    SendToInspector("Manager.Game", Manager.Game.Instance);
                if (GUILayout.Button("TextScenario"))
                    SendToInspector("TextScenario", scenario);
                if (GUILayout.Button("Controller"))
                    SendToInspector("commandController", scenario.commandController);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            {

            }
            GUILayout.EndVertical();

            _varTab = GUILayout.SelectionGrid(_varTab, new[] { "VARs", "Charas", "Heroines" }, 3);

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Search:", GUILayout.ExpandWidth(false));

                    _searchVarStr = GUILayout.TextField(_searchVarStr);
                }
                GUILayout.EndHorizontal();

                _varScrollPos = GUILayout.BeginScrollView(_varScrollPos, false, true);
                switch (_varTab)
                {
                    case 0:
                        ShowVars(scenario);
                        break;
                    case 1:
                        ShowCharas(scenario);
                        break;
                    case 2:
                        ShowHeroines(scenario);
                        break;
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();

            _varWinRect = IMGUIUtils.DragResizeEatWindow(id, _varWinRect);
        }

        private void ShowCharas(TextScenario scenario)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(28);
                GUILayout.Label("ID", GUILayout.Width(20));
                GUILayout.Label("Per", GUILayout.Width(25));
                GUILayout.Label("Init", GUILayout.Width(30));
                GUILayout.Label("Name", GUILayout.Width(150));
            }
            GUILayout.EndHorizontal();

            var controller = scenario.commandController;
            if (controller.Characters == null) return;
            foreach (var characterData in controller.Characters)
            {
                if (_searchVarStr != "" &&
                    characterData.Key.ToString().IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0 &&
                    characterData.Value.voiceNo.ToString().IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0 &&
                    characterData.Value.data.Name.IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GUILayout.BeginHorizontal();
                {
                    if (scenario.currentChara == characterData.Value) GUI.color = Color.green;
                    if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                        scenario.ChangeCurrentChara(characterData.Key);

                    GUILayout.Label(characterData.Key.ToString(), GUILayout.Width(20));
                    GUI.color = Color.white;

                    GUILayout.Label(characterData.Value.voiceNo.ToString(), GUILayout.Width(25));
                    GUILayout.Label(characterData.Value.initialized.ToString(), GUILayout.Width(30));
                    GUILayout.Label(characterData.Value.data.Name ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Inspect"))
                        SendToInspector("ADV Chara " + characterData.Key, characterData.Value);
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void SendToInspector(string name, object value)
        {
            var editorCore = RuntimeUnityEditor.Core.RuntimeUnityEditorCore.Instance;
            editorCore.Inspector.Push(new InstanceStackEntry(value, name), true);
            if (!editorCore.Show) editorCore.Show = true;
        }

        private void ShowHeroines(TextScenario scenario)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(53);
                GUILayout.Label("ID", GUILayout.Width(20));
                GUILayout.Label("Per", GUILayout.Width(25));
                GUILayout.Label("Name", GUILayout.Width(150));
            }
            GUILayout.EndHorizontal();

            if (scenario.heroineList == null) return;

            for (var index = 0; index < scenario.heroineList.Count; index++)
            {
                var charaIndex = -index - 2;
                var heroine = scenario.heroineList[index];

                if (heroine == null) continue;

                if (_searchVarStr != "" &&
                    charaIndex.ToString().IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0 &&
                    heroine.voiceNo.ToString().IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0 &&
                    heroine.parameter.fullname.IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GUILayout.BeginHorizontal();
                {
                    if (scenario.currentHeroine == heroine) GUI.color = Color.green;
                    if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                    {
                        var toSet = scenario.commandController.Characters.FirstOrDefault(x => x.Value.heroine == heroine);
                        if (toSet.Value != null)
                        {
                            scenario.ChangeCurrentChara(toSet.Key);
                            heroine.SetADVParam(scenario);
                        }
                        else
                            Logger.LogMessage("This heroine was not spawned yet");
                    }

                    if (GUILayout.Button("+", GUILayout.ExpandWidth(false)))
                    {
                        var charaData = scenario.commandController.GetChara(charaIndex);
                        if (charaData != null)
                        {
                            var charaId = scenario.commandController.Characters.Keys.Max() + 1;
                            scenario.commandController.AddChara(charaId, charaData.data, null);
                            Logger.LogMessage("Added character under index " + charaId);
                        }
                        else
                            Logger.LogMessage("Could not find CharaData for this Heroine. Was it added after the scene started?");
                    }

                    GUILayout.Label(charaIndex.ToString(), GUILayout.Width(20));
                    GUI.color = Color.white;

                    GUILayout.Label(heroine.voiceNo.ToString(), GUILayout.Width(25));
                    GUILayout.Label(heroine.parameter.fullname, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Inspect"))
                        SendToInspector("ADV Heroine " + charaIndex, heroine);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void ShowVars(TextScenario scenario)
        {
            foreach (var scenarioVar in scenario.Vars)
            {
                var currentValueStr = scenarioVar.Value.o?.ToString() ?? "";

                if (_searchVarStr != "" &&
                    scenarioVar.Key.IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0 &&
                    currentValueStr.IndexOf(_searchVarStr, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GUILayout.BeginHorizontal();
                {
                    var wasEditing = _varEditingKey == scenarioVar.Key;
                    GUILayout.Label(scenarioVar.Key, GUILayout.Width(160));

                    GUI.changed = false;
                    var value = wasEditing ? _varEditingValue : currentValueStr;
                    var newValue = GUILayout.TextField(value);
                    if (wasEditing || GUI.changed)
                    {
                        if (newValue == currentValueStr)
                        {
                            _varEditingKey = null;
                        }
                        else
                        {
                            _varEditingKey = scenarioVar.Key;
                            _varEditingValue = newValue;

                            if (GUILayout.Button("Set", GUILayout.ExpandWidth(false)))
                            {
                                try
                                {
                                    var converted = scenarioVar.Value.Convert(_varEditingValue);
                                    if (converted.GetType() != scenarioVar.Value.o.GetType())
                                        throw new Exception("Conversion resulted in a different type than original");

                                    scenario.Vars[scenarioVar.Key] = new ValData(converted);

                                    _varEditingKey = null;

                                    GUILayout.EndHorizontal();
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Logger.Log(LogLevel.Error | LogLevel.Message, "Failed to set the value: " + e.Message);
                                }
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        #endregion

        private static ManualLogSource Logger => AdvEditorPlugin.Logger;
    }
}