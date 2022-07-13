using System;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class CoinFlipModule: ModuleBase<SocketCommandContext>
{
    private static readonly Random random = new();
    
    [Command("Coinflip")]
    [Summary("Flips a coin")]
    [UsedImplicitly]
    public async Task Coinflip()
    {
        int rng = random.Next(0, 2);
        await ReplyAsync($"{(rng == 0?"Heads":"Tails")}");
        await ArchenemyLogger.Log($"Executed command Coinflip with result {(rng == 0 ? "Heads" : "Tails")}", "Discord");
            
    }
}