using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands
{
    public class BoxChallengeProgressModule: ModuleBase<SocketCommandContext>
    {
        [Command("BoxChallengeProgress")]
        [Summary("Adds the given number to the user's current Draw A Box 250 box challenge progress. If no parameters are given, it displays the current progress")]
        [UsedImplicitly]
        public async Task UpdateBoxChallengeProgress(int drawnBoxes = 0)
        {
            int progress;
            EmbedBuilder embedBuilder;
            if (drawnBoxes != 0)
            {
                try
                {
                    checked
                    {
                        int a = await SQLInteracter.GetBoxChallengeProgress(Context.User) + drawnBoxes;

                    }
                }
                catch (OverflowException)
                {
                    embedBuilder = new()
                    {
                        Description = "Haha funny overflow attempt you're so funny dude haha"  
                    };
                    embedBuilder.WithAuthor(Context.User);
                    await ReplyAsync(embed:embedBuilder.Build());
                    await ArchenemyLogger.Log($"{Context.User} tried to overflow BoxChallengeProgress he is so funny", "Discord");
                    return;
                }

                progress = await SQLInteracter.UpdateBoxChallengeProgress(drawnBoxes, Context.User);
            }

            else
                progress = await SQLInteracter.GetBoxChallengeProgress(Context.User);

            embedBuilder = new()
            {
                Description = buildDescriptionString(progress)  
            };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed:embedBuilder.Build());
            await ArchenemyLogger.Log($"Executed command BoxChallengeProgress for user {Context.User} with result {progress}", "Discord");

        }
        
        private string buildDescriptionString(int progress)
        {
            return
                $"{(Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username} {(progress != 0 ? $"has drawn {progress} box{(progress == 1 ? string.Empty : "es")} ({((float)progress / 250):0.00%})" : "hasn't started the 250 box challenge!")}";
        }
    }
}