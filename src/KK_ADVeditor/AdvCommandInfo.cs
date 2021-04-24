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
        public string Description;

        public AdvCommandInfo(Command command, CommandBase commandBase)
        {
            Command = command;
            CommandName = command.ToString();
            if (!Enum.IsDefined(typeof(Command), command))
                CommandName = commandBase.GetType().Name;

            CommandBase = commandBase;
            ArgsLabel = commandBase.ArgsLabel ?? new string[0];
            ArgsDefault = commandBase.ArgsDefault;

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
            int largestValue = Enum.GetValues(typeof(Command)).Cast<Command>().Select(value => (int)value).Max();

            for (int i = 1; ; i++)
            {
                var value = (Command)i;

                var command = CommandList.CommandGet(value);
                if (command != null)
                    yield return new AdvCommandInfo(value, command);
                else if (i > largestValue)
                    break;
                else
                    AdvEditorPlugin.Logger.LogWarning("Unsupported ADV.Command: " + value);
            }
        }

    }
}