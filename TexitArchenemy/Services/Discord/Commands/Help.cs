using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class HelpModule: ModuleBase<SocketCommandContext>
    {
        public CommandService _commandService { get; set; } = null!;

        [Command("Help")]
        [Summary("Displays all commands")]
        [UsedImplicitly]
        public async Task Help()
        {
            List<CommandInfo> commands = _commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new();

            foreach (CommandInfo command in commands)
            {
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\n";

                string? parameters = null;
                if (command.Parameters is { Count: > 0 })
                {
                    parameters = $"{Environment.NewLine}**Parameters:**{Environment.NewLine}";
                    foreach (ParameterInfo? parameterInfo in command.Parameters)
                    {
                        parameters += $"{parameterInfo.Name}: {parameterInfo.Type} {Environment.NewLine}";
                    }
                }

                embedBuilder.AddField(command.Name, $"{embedFieldText} {parameters??$"{Environment.NewLine} **No parameters**"}");
            }

            await ReplyAsync("Here's a list of commands and their description and parameters: ", false, embedBuilder.Build());
        }
    }
}