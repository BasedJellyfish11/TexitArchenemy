using System;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class ChooseModule: ModuleBase<SocketCommandContext>
{
    [Command("Choose")]
    [Summary("Makes a choice among the given parameters.")]
    [UsedImplicitly]
    public async Task Choose(params string[] options)
    {
        string choice = options[new Random().Next(0, options.Length)];
        await ReplyAsync(choice);
        await ArchenemyLogger.Log($"Executed command Choose with result {choice}", "Discord");

    }
}