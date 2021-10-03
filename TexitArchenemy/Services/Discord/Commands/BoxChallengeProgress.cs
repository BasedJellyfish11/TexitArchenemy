using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TexitArchenemy.Services.DB;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class BoxChallengeProgressModule: ModuleBase<SocketCommandContext>
    {
        [Command("BoxChallengeProgress")]
        [Summary("Adds the given number to the user's current Draw A Box 250 box challenge progress")]
        public async Task UpdateBoxChallengeProgress(int drawnBoxes)
        {
            int progress = await SQLInteracter.UpdateBoxChallengeProgress(drawnBoxes, Context.User);

            EmbedBuilder embedBuilder = new()
            {
                Description = buildDescriptionString(progress)  
            };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed:embedBuilder.Build());

        }
        
        [Command("BoxChallengeProgress")]
        [Summary("Gets the user's current Draw A Box 250 box challenge progress")]
        public async Task GetBoxChallengeProgress()
        {
            int progress = await SQLInteracter.GetBoxChallengeProgress(Context.User);

            EmbedBuilder embedBuilder = new()
            {
                Description = buildDescriptionString(progress) 
            };

            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed:embedBuilder.Build());

        }

        private string buildDescriptionString(int progress)
        {
            return
                $"{(Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username} {(progress != 0 ? $"has drawn {progress} box{(progress == 1 ? string.Empty : "es")} ({((float)progress / 250):0.00%})" : "hasn't started the 250 box challenge!")}";
        }
    }
}