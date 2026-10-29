using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord;

public static class LLMChatService
{
    private const string API_URL = "https://openrouter.ai/api/v1/chat/completions";
    private const string KEY_INFO_URL = "https://openrouter.ai/api/v1/key";

    // Models tried in order until one answers. All accept text + image input, so !ask stays
    // multimodal. OpenRouter already fails over across upstream providers, but a model can still
    // be down or slow, so we keep a cheap chain plus a short per-model timeout to move on quickly.
    private static readonly string[] Models =
        ["z-ai/glm-5.3-flash", "qwen/qwen3.7-flash", "deepseek/deepseek-v4.1-flash"];

    private const int MAX_REQUESTS_PER_MINUTE = 15;
    private const int MAX_CONVERSATION_MESSAGES = 30;
    private const int DISCORD_MESSAGE_LIMIT = 2000;
    private const int PER_MODEL_TIMEOUT_SECONDS = 60;
    private const int CREDITS_TIMEOUT_SECONDS = 5;
    private const double EDIT_INTERVAL_SECONDS = 1.25;   // Discord allows ~5 message edits / 5s per channel
    private const int MAX_SOURCES_SHOWN = 4;
    private const string THINKING_PLACEHOLDER = "💭 Thinking…";

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
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")}");
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
                conversation.AddFirst(new { role = "assistant", content = StripFooters(current.Content) });
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

    // The models are multimodal: text-only messages use a plain string content, while messages
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

        // Post a placeholder straight away, then stream the answer into it by editing. Replying also
        // links this message into the reply chain, which is what lets users continue the conversation
        // by replying back to it.
        IUserMessage reply = await userMessage.ReplyAsync(THINKING_PLACEHOLDER);
        try
        {
            string? answer = await StreamAnswer(conversation, reply);
            await Finalize(reply, answer);
        }
        catch (Exception e)
        {
            await ArchenemyLogger.Log($"LLM request failed: {e}", "Discord");
            await SafeEdit(reply, "The LLM request failed, try again later.");
        }
    }

    // Puts the finished answer on screen: the first chunk replaces the placeholder, any overflow
    // past Discord's 2000-char limit follows as extra messages.
    private static async Task Finalize(IUserMessage reply, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            await SafeEdit(reply, "None of the LLMs answered, try again later.");
            return;
        }

        string? creditsFooter = await FetchCreditsFooter();
        if (creditsFooter != null)
            answer = $"{answer}\n{creditsFooter}";

        IReadOnlyList<string> chunks = SplitForDiscord(answer);
        await SafeEdit(reply, chunks[0]);
        for (int i = 1; i < chunks.Count; i++)
            await reply.Channel.SendMessageAsync(chunks[i], messageReference: new MessageReference(reply.Id));
    }

    // Reports the money left on the API key. A missing balance must never block the answer, so any
    // failure just drops the footer.
    private static async Task<string?> FetchCreditsFooter()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(CREDITS_TIMEOUT_SECONDS));
        try
        {
            using HttpResponseMessage response = await Client.GetAsync(KEY_INFO_URL, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

            // limit_remaining is only set when the key has a credit limit configured on OpenRouter
            JsonElement data = document.RootElement.GetProperty("data");
            if (!data.TryGetProperty("limit_remaining", out JsonElement remaining) || remaining.ValueKind != JsonValueKind.Number)
                return null;

            return $"-# 💰 ${remaining.GetDouble().ToString("0.00", CultureInfo.InvariantCulture)} left";
        }
        catch (Exception e)
        {
            await ArchenemyLogger.Log($"Fetching OpenRouter credits failed: {e.Message}", "Discord");
            return null;
        }
    }

    // Footers (sources, credits) are Discord subtext for humans; feeding them back as conversation
    // history would make the model imitate them in its own answers.
    private static string StripFooters(string content)
    {
        IEnumerable<string> lines = content.Split('\n').Where(line => !line.StartsWith("-# ", StringComparison.Ordinal));
        return string.Join('\n', lines).Trim();
    }

    // A failed edit (message deleted, transient rate limit) must not abort the stream, so swallow
    // and log it.
    private static async Task SafeEdit(IUserMessage message, string content)
    {
        try
        {
            await message.ModifyAsync(m => m.Content = content);
        }
        catch (Exception e)
        {
            await ArchenemyLogger.Log($"Discord edit failed: {e.Message}", "Discord");
        }
    }

    private static IReadOnlyList<string> SplitForDiscord(string text)
    {
        if (text.Length <= DISCORD_MESSAGE_LIMIT)
            return [text];

        List<string> chunks = [];
        for (int start = 0; start < text.Length; start += DISCORD_MESSAGE_LIMIT)
            chunks.Add(text.Substring(start, Math.Min(DISCORD_MESSAGE_LIMIT, text.Length - start)));

        return chunks;
    }

    private static async Task<string?> StreamAnswer(List<object> conversation, IUserMessage reply)
    {
        List<object> messages = [new { role = "system", content = SYSTEM_PROMPT }, .. conversation];

        foreach (string model in Models)
        {
            string? answer = await TryStreamModel(model, messages, reply);
            if (!string.IsNullOrWhiteSpace(answer))
                return answer;
        }

        return null;
    }

    private static async Task<string?> TryStreamModel(string model, List<object> messages, IUserMessage reply)
    {
        await ArchenemyLogger.Log($"Asking {model}", "Discord");
        await SafeEdit(reply, THINKING_PLACEHOLDER);   // reset in case a previous model streamed partial text

        // Cap each model so a hung or slow one fails over to the next instead of stalling the whole
        // command. Any failure (timeout, network error, bad JSON) returns null so the chain moves on
        // rather than throwing out of it.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(PER_MODEL_TIMEOUT_SECONDS));
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, API_URL)
            {
                Content = new StringContent(BuildRequestJson(model, messages), Encoding.UTF8, "application/json")
            };
            using HttpResponseMessage response =
                await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            return await ConsumeStream(response, reply, timeout.Token);
        }
        catch (Exception e)
        {
            await ArchenemyLogger.Log($"Model {model} failed: {e.Message}", "Discord");
            return null;
        }
    }

    // Reads the server-sent-event stream, editing the Discord message as it goes: a "thinking" or
    // "searching" status while the model reasons and runs web searches, then the answer itself once
    // its text starts arriving. Returns the finished answer with a compact source list appended.
    private static async Task<string?> ConsumeStream(HttpResponseMessage response, IUserMessage reply, CancellationToken token)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(token);
        using StreamReader reader = new(stream);

        StringBuilder content = new();
        List<string> sources = [];
        bool searching = false;
        string shown = THINKING_PLACEHOLDER;
        DateTime lastEdit = DateTime.MinValue;

        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            string payload = line[5..].Trim();
            if (payload is "[DONE]")
                break;

            AccumulateDelta(payload, content, sources, ref searching);

            // The answer text arrives only at the very end (after a long reasoning + search phase),
            // so show a live status until it does, then switch to the answer as it streams in.
            string view = content.Length > 0 ? content.ToString() : StatusLine(searching, sources.Count);
            if (view == shown || DateTime.UtcNow - lastEdit < TimeSpan.FromSeconds(EDIT_INTERVAL_SECONDS))
                continue;

            await SafeEdit(reply, Truncate(view, DISCORD_MESSAGE_LIMIT));
            shown = view;
            lastEdit = DateTime.UtcNow;
        }

        string answer = StripThink(content.ToString()).Trim();
        return answer.Length == 0 ? null : AppendSources(answer, sources);
    }

    // Applies one SSE chunk: appends any answer text and collects web-search citation URLs.
    private static void AccumulateDelta(string payload, StringBuilder content, List<string> sources, ref bool searching)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement choices = document.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0 || !choices[0].TryGetProperty("delta", out JsonElement delta))
                return;

            if (delta.TryGetProperty("content", out JsonElement text) && text.ValueKind == JsonValueKind.String)
                content.Append(text.GetString());

            if (!delta.TryGetProperty("annotations", out JsonElement annotations) || annotations.ValueKind != JsonValueKind.Array)
                return;

            searching = true;
            foreach (JsonElement annotation in annotations.EnumerateArray())
            {
                if (annotation.TryGetProperty("url_citation", out JsonElement citation)
                    && citation.TryGetProperty("url", out JsonElement url)
                    && url.GetString() is { } link && !sources.Contains(link))
                    sources.Add(link);
            }
        }
        catch (JsonException)
        {
            // Keep-alive comments and partial chunks aren't valid JSON; skip them.
        }
    }

    private static string StatusLine(bool searching, int sourceCount) =>
        searching
            ? $"🔍 Searching the web… ({sourceCount} source{(sourceCount == 1 ? "" : "s")} so far)"
            : THINKING_PLACEHOLDER;

    private static string AppendSources(string answer, List<string> sources)
    {
        if (sources.Count == 0)
            return answer;

        string links = string.Join(" · ", sources.Take(MAX_SOURCES_SHOWN).Select(FormatSourceLink));
        return $"{answer}\n-# 🔎 {links}";
    }

    // Shows the source as its host name, linked, with the URL in angle brackets so Discord doesn't
    // expand a preview embed for every citation.
    private static string FormatSourceLink(string url)
    {
        string host = Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            ? parsed.Host.Replace("www.", "")
            : url;
        return $"[{host}](<{url}>)";
    }

    private static string BuildRequestJson(string model, List<object> messages) =>
        JsonSerializer.Serialize(new
        {
            model,
            messages,
            max_tokens = 8192,
            temperature = 1.00,
            top_p = 0.95,
            stream = true,
            // Let the model decide when to search; Parallel's turbo tier is the cheapest at $0.001/search.
            tools = new object[]
            {
                new { type = "openrouter:web_search", parameters = new { engine = "parallel", mode = "turbo" } }
            }
        });

    // Reasoning models can prepend their thinking in a <think> block; only keep the answer after it.
    private static string StripThink(string content)
    {
        int thinkEnd = content.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        return thinkEnd >= 0 ? content[(thinkEnd + "</think>".Length)..] : content;
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
