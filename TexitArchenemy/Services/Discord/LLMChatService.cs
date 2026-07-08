using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord;

public static class LLMChatService
{
    private const string API_URL = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string MODEL = "minimaxai/minimax-m3";

    private const int MAX_REQUESTS_PER_MINUTE = 15;
    private const int MAX_CONVERSATION_MESSAGES = 30;
    private const int DISCORD_MESSAGE_LIMIT = 2000;

    private const string SYSTEM_PROMPT = """
                                         ## Core Principles
                                         - CRITICAL: You always keep you messages under 1000 characters. Your messages are sent to a messaging app that cannot handle messages over 2000 characters. Failure to comply with this causes critical failure. This directive is absolute and covers any situation, including medical or life advice ones. Not following this directive means the user NEVER SEES YOUR MESSAGE due to a crash, leaving them at risk.
                                         - You value being concise and accurate. Verbosity is always seen as a flaw.
                                         - Users can continue the conversation by replying to your messages, so you never prompt for follow ups or similar.
                                         - You triple check message length. Common pitfalls to this include not counting every character and ignoring things like styling. Any and all characters should be counted, including spaces, markdown formatting, and anything else that may be included in your response.
                                         ## Validation checklist
                                         - Is the entire string content of the reply <1000 characters?
                                         - Is the reply overly verbose? Have I added weasel words or otherwise beautified the message? Can it be more concise?
                                         """;

    private static readonly HttpClient Client = CreateClient();
    private static readonly Queue<DateTime> RecentRequestTimes = new();

    private static HttpClient CreateClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("NVIDIA_API_KEY")}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    // Handles a fresh !ask / !8ball command.
    public static async Task AnswerCommand(SocketCommandContext context, string question)
    {
        List<object> conversation = [BuildUserMessage(question, context.Message.Attachments)];
        await RespondTo(context.Message, conversation);
    }

    // Returns true if the message was a reply into an existing !ask conversation and was answered.
    public static async Task<bool> TryContinueConversation(SocketCommandContext context)
    {
        if (context.Message.Type != MessageType.Reply)
            return false;

        IMessage? repliedTo = context.Message.ReferencedMessage
                              ?? await GetRepliedToMessage(context.Message, context.Channel);
        if (repliedTo == null || repliedTo.Author.Id != context.Client.CurrentUser.Id)
            return false;

        List<object>? conversation = await BuildConversationFromReplyChain(context);
        if (conversation == null)
            return false;

        await RespondTo(context.Message, conversation);
        return true;
    }

    // Walks the reply chain upwards, oldest message first in the result:
    // user !ask -> bot answer -> user reply -> ... -> triggering user reply
    // Returns null if the chain doesn't lead back to an !ask command, meaning
    // this is a reply to some other bot message and not an LLM conversation.
    private static async Task<List<object>?> BuildConversationFromReplyChain(SocketCommandContext context)
    {
        LinkedList<object> conversation = new();
        IMessage? current = context.Message;

        for (int i = 0; i < MAX_CONVERSATION_MESSAGES && current != null; i++)
        {
            if (current.Author.Id == context.Client.CurrentUser.Id)
            {
                conversation.AddFirst(new { role = "assistant", content = current.Content });
            }
            else if (TryStripAskPrefix(current.Content, out string question))
            {
                // Found the root !ask command, the conversation is complete
                conversation.AddFirst(BuildUserMessage(question, current.Attachments));
                return conversation.ToList();
            }
            else
            {
                conversation.AddFirst(BuildUserMessage(current.Content, current.Attachments));
            }

            current = await GetRepliedToMessage(current, context.Channel);
        }

        return null;
    }

    private static async Task<IMessage?> GetRepliedToMessage(IMessage message, ISocketMessageChannel channel)
    {
        // Only follow actual replies; forwards and other reference types are not conversations
        if (message.Type != MessageType.Reply)
            return null;
        if (message.Reference is not { MessageId.IsSpecified: true } reference)
            return null;

        return await channel.GetMessageAsync(reference.MessageId.Value);
    }

    private static bool TryStripAskPrefix(string messageText, out string question)
    {
        string[] commandPrefixes = ["!ask", "!8ball"];
        foreach (string prefix in commandPrefixes)
        {
            if (messageText.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                question = string.Empty;
                return true;
            }

            if (messageText.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                question = messageText[prefix.Length..].Trim();
                return true;
            }
        }

        question = string.Empty;
        return false;
    }

    // MiniMax-M3 is multimodal: text-only messages use a plain string content, while messages
    // with attachments use a list of typed parts (text + image_url/video_url).
    private static object BuildUserMessage(string text, IEnumerable<IAttachment> attachments)
    {
        List<object> attachmentParts = [];
        foreach (IAttachment attachment in attachments)
        {
            string contentType = attachment.ContentType ?? string.Empty;
            if (contentType.StartsWith("image/"))
                attachmentParts.Add(new { type = "image_url", image_url = new { url = attachment.Url } });
            else if (contentType.StartsWith("video/"))
                attachmentParts.Add(new { type = "video_url", video_url = new { url = attachment.Url } });
        }

        if (attachmentParts.Count == 0)
            return new { role = "user", content = text };

        List<object> parts = [];
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add(new { type = "text", text });
        parts.AddRange(attachmentParts);

        return new { role = "user", content = parts };
    }

    private static async Task RespondTo(SocketUserMessage userMessage, List<object> conversation)
    {
        if (!TryReserveRateLimitSlot())
        {
            await ArchenemyLogger.Log("LLM request throttled (over 15/minute)", "Discord");
            await userMessage.ReplyAsync("Slow down, the LLM only takes 15 requests per minute. Try again in a bit.");
            return;
        }

        try
        {
            string? answer = await QueryModel(conversation);
            if (string.IsNullOrWhiteSpace(answer))
            {
                await userMessage.ReplyAsync("The LLM returned nothing, try again later.");
                return;
            }

            // Answering as a reply links this message into the reply chain, which is what
            // lets users continue the conversation by replying back to it
            await userMessage.ReplyAsync(Truncate(answer, DISCORD_MESSAGE_LIMIT));
        }
        catch (Exception e)
        {
            await ArchenemyLogger.Log($"LLM request failed: {e}", "Discord");
            await userMessage.ReplyAsync("The LLM request failed, try again later.");
        }
    }

    private static async Task<string?> QueryModel(List<object> conversation)
    {
        List<object> messages = [new { role = "system", content = SYSTEM_PROMPT }, .. conversation];

        var request = new
        {
            model = MODEL,
            messages,
            max_tokens = 8192,
            temperature = 1.00,
            top_p = 0.95,
            stream = false
        };

        string requestJson = JsonSerializer.Serialize(request);
        await ArchenemyLogger.Log($"Asking {MODEL}", "Discord");

        HttpResponseMessage response = await Client.PostAsync(API_URL, new StringContent(requestJson, Encoding.UTF8, "application/json"));
        string responseBody = await response.Content.ReadAsStringAsync();
        await ArchenemyLogger.Log($"LLM response: {responseBody}", "Discord");

        response.EnsureSuccessStatusCode();
        return ExtractAnswer(responseBody);
    }

    private static string? ExtractAnswer(string responseBody)
    {
        using JsonDocument document = JsonDocument.Parse(responseBody);

        // The API returns 200 with empty choices when it can't process an attachment
        JsonElement choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            return null;

        string? content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (content == null)
            return null;

        // Reasoning models can prepend their thinking in a <think> block; only keep the answer
        int thinkEnd = content.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
            content = content[(thinkEnd + "</think>".Length)..];

        return content.Trim();
    }

    private static bool TryReserveRateLimitSlot()
    {
        lock (RecentRequestTimes)
        {
            DateTime oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            while (RecentRequestTimes.Count > 0 && RecentRequestTimes.Peek() < oneMinuteAgo)
                RecentRequestTimes.Dequeue();

            if (RecentRequestTimes.Count >= MAX_REQUESTS_PER_MINUTE)
                return false;

            RecentRequestTimes.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }
}
