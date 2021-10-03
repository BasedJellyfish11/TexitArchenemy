using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using TexitArchenemy.Services.DB;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class BoxWarmupModule: ModuleBase<SocketCommandContext>
    {
        [Command("BoxWarmup")]
        [Summary("Gets a random DrawABox warmup exercise for the specified level or lower")]
        public async Task BoxWarmup(int level)
        {
            List<string?> warmups = await SQLInteracter.GetBoxWarmup(level);
            await ReplyAsync(warmups[new Random().Next(0, warmups.Count)]);

        }
    }
}