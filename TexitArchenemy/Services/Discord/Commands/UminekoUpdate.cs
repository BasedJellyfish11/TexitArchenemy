using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;

namespace TexitArchenemy.Services.Discord.Commands;

public class UminekoUpdateModule : ModuleBase<SocketCommandContext>
{
    [Command("uminekoupdate")]
    [Summary("Manually matches a pasted quote against the script, for when OCR misreads a screenshot.")]
    [UsedImplicitly]
    public async Task UminekoUpdate([Remainder] string quoteText)
    {
        EmbedBuilder embedBuilder;

        if (Context.Guild == null)
        {
            embedBuilder = new EmbedBuilder { Description = "This command has to be used in a server channel." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        UminekoProgressRecord? progress = await SQLInteracter.GetUminekoProgress(Context.User, Context.Guild.Id);
        if (progress == null)
        {
            embedBuilder = new EmbedBuilder { Description = "You're not registered for Umineko progress tracking. Run !uminekoregister first." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        bool matched = await CommandHandler.ApplyUminekoQuoteMatch(Context, progress, quoteText);
        if (!matched)
        {
            embedBuilder = new EmbedBuilder { Description = "No quote past your current position matched that text." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
        }
    }
}
