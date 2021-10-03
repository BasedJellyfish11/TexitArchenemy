﻿using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class ChooseModule: ModuleBase<SocketCommandContext>
    {
        [Command("Choose")]
        [Summary("Makes a choice among the given parameters")]
        public async Task Choose(params string[] options)
        {
            await ReplyAsync(options[new Random().Next(0, options.Length)]);

        }
    }
}