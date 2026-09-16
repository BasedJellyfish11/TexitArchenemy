using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class UminekoUnregisterModule : ModuleBase<SocketCommandContext>
{
    [Command("uminekounregister")]
    [Summary("Removes your Umineko progress tracking registration and deletes your access role.")]
    [UsedImplicitly]
    public async Task UminekoUnregister()
    {
        EmbedBuilder embedBuilder;

        if (Context.Channel is not SocketGuildChannel || Context.User is not SocketGuildUser)
        {
            embedBuilder = new EmbedBuilder { Description = "This command has to be used in a server channel." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        UminekoProgressRecord? progress = await SQLInteracter.GetUminekoProgress(Context.User, Context.Guild.Id);
        if (progress == null)
        {
            embedBuilder = new EmbedBuilder { Description = "You're not registered for Umineko progress tracking." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        if (progress.RoleId is { } roleId)
        {
            IRole? role = Context.Guild.GetRole(roleId);
            if (role != null)
                await role.DeleteAsync();
        }

        await SQLInteracter.UnregisterUminekoUser(Context.User, Context.Guild.Id);

        embedBuilder = new EmbedBuilder
        {
            Description = "Unregistered! Your Umineko progress role and tracking entry have been removed."
        };
        embedBuilder.WithAuthor(Context.User);
        await ReplyAsync(embed: embedBuilder.Build());
        await ArchenemyLogger.Log($"{Context.User} unregistered from Umineko progress tracking in guild {Context.Guild.Id}", "Discord");
    }
}
