﻿using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class CoinFlipModule: ModuleBase<SocketCommandContext>
    {
        [Command("Coinflip")]
        [Summary("Flips a coin")]
        public async Task Coinflip()
        {
            int rng = new Random().Next(0, 2);
            await ReplyAsync($"{(rng == 0?"Heads":"Tails")}");
        }
    }
}