using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;

namespace TexitArchenemy.Services.Discord.Commands;

public class UminekoHelpModule : ModuleBase<SocketCommandContext>
{
    [Command("uminekohelp")]
    [Summary("Lists every command that's part of the Umineko progress tracker.")]
    [UsedImplicitly]
    public async Task UminekoHelp()
    {
        EmbedBuilder embedBuilder = new()
        {
            Title = "Umineko progress tracker commands",
            Description =
                "**!uminekoregister** — registers the current channel as your progress channel and creates your access role.\n" +
                "**!uminekounregister** — removes your registration and deletes your access role.\n" +
                "**!uminekoupdate <quote text>** — manually matches a pasted quote, for when OCR misreads a screenshot.\n" +
                "**!uminekocurrent** — shows your current quote and its index out of the total.\n" +
                "**!uminekohelp** — shows this list.\n\n" +
                "Posting a screenshot of your in-game progress in your registered channel also updates it automatically via OCR."
        };
        embedBuilder.WithAuthor(Context.User);
        await ReplyAsync(embed: embedBuilder.Build());
    }
}
