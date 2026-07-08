using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class Ask : ModuleBase<SocketCommandContext>
{
    [Command("Ask")]
    [Alias("8ball")]
    [Summary("Asks an LLM. Supports image and video attachments. Reply to the bot's answer to continue the conversation")]
    [UsedImplicitly]
    public async Task QueryLLM([Remainder] string message = "")
    {
        await ArchenemyLogger.Log($"Received Ask command with message {message}", "Discord");
        await LLMChatService.AnswerCommand(Context, message);
        await ArchenemyLogger.Log($"Ended Ask command for message {message}", "Discord");
    }
}
