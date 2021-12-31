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
    public class RepostPoliceModule: ModuleBase<SocketCommandContext>
    {
        [Command("RepostPolice")]
        [Summary("Calls out any art reposts on this channel from now on. Currently supports twitter, pixiv and artstation")]
        [RequireUserPermission(ChannelPermission.ManageChannels | ChannelPermission.ManageMessages, Group = "RepostPolicePermissions")]
        [RequireUserPermission(GuildPermission.Administrator | GuildPermission.ManageChannels, Group = "RepostPolicePermissions")]
        [UsedImplicitly]
        public async Task RepostPolice()
        {
            EmbedBuilder embedBuilder;
            if (Context.Channel is not SocketGuildChannel channel)
            {
                embedBuilder = new EmbedBuilder
                {
                    Description = "Sorry, only channels in servers can be marked to be checked for reposts"
                };

                embedBuilder.WithAuthor(Context.User);
                await ReplyAsync(embed: embedBuilder.Build());
                await ArchenemyLogger.Log($"{Context.User} tried to execute RepostPolice in non-guild channel {Context.Channel} (ID {Context.Channel.Id}) and was met with an error.", "Discord");
                return;
            }

            await SQLInteracter.MarkAsRepostChannel(channel);
            embedBuilder = new EmbedBuilder
            {
                Description = "I will start calling out reposts in this channel from now on"
            };

            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            await ArchenemyLogger.Log($"{Context.User} has marked channel {Context.Channel} (ID {Context.Channel.Id}) as a no repost channel.", "Discord");
        }
    }
}