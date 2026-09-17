using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;

namespace TexitArchenemy.Services.Discord.Commands;

public class UminekoCurrentModule : ModuleBase<SocketCommandContext>
{
    [Command("uminekocurrent")]
    [Summary("Shows your current Umineko quote and its index out of the total.")]
    [UsedImplicitly]
    public async Task UminekoCurrent()
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

        int totalQuotes = await SQLInteracter.GetUminekoQuoteCount();

        if (progress.QuoteIndex is not { } quoteIndex)
        {
            embedBuilder = new EmbedBuilder { Description = $"You haven't posted a progress screenshot yet. Total quotes: {totalQuotes}." };
            embedBuilder.WithAuthor(Context.User);
            await ReplyAsync(embed: embedBuilder.Build());
            return;
        }

        embedBuilder = new EmbedBuilder
        {
            Title = $"Quote #{quoteIndex} / {totalQuotes}",
            Description = progress.QuotePlaintext
        };
        embedBuilder.WithAuthor(Context.User);
        await ReplyAsync(embed: embedBuilder.Build());
    }
}
