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
            var values = Enum.GetValues(typeof(Command));
            foreach (Command value in values)
            {
                if (value == Command.None) continue;

                var command = CommandList.CommandGet(value);
                if (command != null)
                    yield return new AdvCommandInfo(value, command);
                else
                    AdvEditorPlugin.Logger.LogWarning("Unsupported ADV.Command: " + value);
            }
        }

    }
}