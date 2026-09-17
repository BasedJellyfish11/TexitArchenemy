using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class UminekoRegisterModule : ModuleBase<SocketCommandContext>
{
    [Command("uminekoregister")]
    [Summary("Registers the current channel as your Umineko progress channel and creates the role that grants access to it.")]
    [UsedImplicitly]
    public async Task UminekoRegister()
    {
        EmbedBuilder embedBuilder;
        
        if (Context.Channel is not SocketGuildChannel channel || Context.User is not SocketGuildUser guildUser)
        {
            embedBuilder = new EmbedBuilder { Description = "This command has to be used in a server channel." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        if (await SQLInteracter.GetUminekoProgress(Context.User, Context.Guild.Id) != null)
        {
            embedBuilder = new EmbedBuilder { Description = "You're already registered for Umineko progress tracking." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        IRole role = await Context.Guild.CreateRoleAsync($"umineko-{Context.User.Username}", isMentionable: true);
        await channel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
        await channel.AddPermissionOverwriteAsync(role, new OverwritePermissions(viewChannel: PermValue.Allow));
        await guildUser.AddRoleAsync(role);

        await SQLInteracter.RegisterUminekoUser(Context.User, Context.Guild.Id, channel.Id, role.Id);

        embedBuilder = new EmbedBuilder
        {
            Description = $"Registered! This channel is now private, only {role.Mention} can see it. Post your progress screenshots here to start updating it."
        };
        embedBuilder.WithAuthor(Context.User);
        await ReplyAsync(embed: embedBuilder.Build());
        await ArchenemyLogger.Log($"{Context.User} registered channel {channel} (ID {channel.Id}) as private for Umineko progress tracking with role {role.Id}", "Discord");
    }
}
