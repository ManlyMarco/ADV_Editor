using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ADV;

namespace KK_ADVeditor
{
    public sealed class AdvCommandInfo
    {
        public readonly Command Command;
        public readonly string CommandName;
        public readonly CommandBase CommandBase;
        public readonly string[] ArgsDefault;
        public readonly string[] ArgsLabel;
        public readonly bool IsCustom;
        public string Description;

        public AdvCommandInfo(Command command, CommandBase commandBase, bool isCustom)
        {
            Command = command;
            CommandName = command.ToString();
            if (!Enum.IsDefined(typeof(Command), command))
                CommandName = commandBase.GetType().Name;

            CommandBase = commandBase;
            ArgsLabel = commandBase.ArgsLabel ?? new string[0];
            ArgsDefault = commandBase.ArgsDefault;

            IsCustom = isCustom;

            UpdateDescription();
        }

        public void UpdateDescription()
        {
            AdvEditorPlugin.CommandDescriptions.TryGetValue(CommandName, out Description);
            if (Description == null) Description = "";
        }

        private static ReadOnlyCollection<AdvCommandInfo> _allCommands;
        private static Dictionary<Command, AdvCommandInfo> _allCommandLookup;
        public static ReadOnlyCollection<AdvCommandInfo> AllCommands => _allCommands ?? (_allCommands = GatherCommands().ToList<AdvCommandInfo>().AsReadOnly());
        public static AdvCommandInfo TryGetCommand(Command command)
        {
            if (_allCommandLookup == null)
                _allCommandLookup = AllCommands.ToDictionary(x => x.Command, x => x);

            _allCommandLookup.TryGetValue(command, out var result);
            return result;
        }

        private static IEnumerable<AdvCommandInfo> GatherCommands()
        {
            var largestValue = Enum.GetValues(typeof(Command)).Cast<int>().Max();

            for (var i = 1; ; i++)
            {
                var value = (Command)i;

                // Try CommandGet first range check later, in order to support custom commands
                var aboveLargest = i > largestValue;
#if KK
                var command = CommandList.CommandGet(value);
#elif KKS
                var command = CommandGenerator.Create(value);
#endif
                if (command != null)
                    yield return new AdvCommandInfo(value, command, aboveLargest);
                else if (aboveLargest)
                    break;
                else
                    AdvEditorPlugin.Logger.LogWarning("Unsupported ADV.Command: " + value);
            }
        }
    }
}