using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class BoxWarmupModule: ModuleBase<SocketCommandContext>
{
    [Command("BoxWarmup")]
    [Summary("Gets a random DrawABox warmup exercise for the specified lesson or lower. If no parameters are given, lesson 7 is assumed")]
    [UsedImplicitly]
    public async Task BoxWarmup(int level = 7)
    {
        List<string?> warmups = await SQLInteracter.GetBoxWarmup(level);
        EmbedBuilder embedBuilder = new()
        {
            Description = warmups[new Random().Next(0, warmups.Count)] 
        };

        embedBuilder.WithAuthor(Context.User);
        await ReplyAsync(embed: embedBuilder.Build());
        await ArchenemyLogger.Log($"Executed command BoxWarmup for user {Context.User} with result {embedBuilder.Description}", "Discord");
            

    }
        
}