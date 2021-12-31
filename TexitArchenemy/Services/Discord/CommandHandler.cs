using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord;

public class CommandHandler
{
    // language=regexp
    private const string TWITTER_REGEX = 
        @"(?:^|\s)(?:https?):\/\/(?:www\.|mobile\.|m\.)?(?:fx)?(twitter)(?:\.com\/)(?:[\w\-\*]*?\/)?(?:status|statuses)\/(\d{18,20})(?:\s|$|\?|\/)";
        
    // language=regexp
    private const string PIXIV_REGEX = 
        @"(?:^|\s)(?:https?):\/\/(?:www\.)?(pixiv)(?:\.net\/)(?:[\w|\/|\.|\?|\&|\=]*?)(?:(?:artworks\/|illust_id=)(\d{6,9}))(?:\s|$|&)";
        
    // language=regexp
    private const string ARTSTATION_REGEX = 
        @"(?:^|\s)(?:https?):\/\/(?:[\w-]+\.)?(artstation)(?:\.com\/)(?:artwork|projects?)*\/([^?&\s]+)(?:\s|$|&|\?)";
        
    // language=regexp
    private const string DN_REGEX = 
        @"(?:^|\s)dn(?:\s|$)";
        
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly Random random = new();
    // Retrieve client and CommandService instance via ctor
    public CommandHandler(DiscordSocketClient client, CommandService commands)
    {
        _commands = commands;
        _client = client;
    }
    
    public async Task InstallCommandsAsync()
    {
        // Hook the MessageReceived event into our command handler
        _client.MessageReceived += HandleCommandAsync;

        // Here we discover all of the command modules in the entry 
        // assembly and load them. Starting from Discord.NET 2.0, a
        // service provider is required to be passed into the
        // module registration method to inject the 
        // required dependencies.
        //
        // If you do not use Dependency Injection, pass null.
        // See Dependency Injection guide for more information.
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    }

    private async Task HandleCommandAsync(SocketMessage messageParam)
    {
        // Don't process the command if it was a system message
        if (messageParam is not SocketUserMessage message) return;

        // Create a number to track where the prefix ends and the command begins
        int argPos = 0;

        // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            
        if (message.Author.IsBot && message.Author.Id != _client.CurrentUser.Id)
            return;
            
        // Create a WebSocket-based command context based on the message
        SocketCommandContext context = new(_client, message);
        if (!message.HasCharPrefix('!', ref argPos))
        {
            await CheckNonCommand(context);
        }
        else if(message.Author.Id != _client.CurrentUser.Id)
        {
            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(context, argPos, null);
        }
    }

    private async Task CheckNonCommand(SocketCommandContext context)
    {
        // await ArchenemyLogger.Log($"Handling message {context.Message} from channel {context.Channel}", "Discord");
        string? message = context.Message.Content?.ToLower();
        if(message == null)
            return;
        if (context.Message.Author.Id != _client.CurrentUser.Id)
        {
            if (message == "test")
            {
                await context.Channel.SendMessageAsync($"{context.Message.Author.Mention} How about you test these nuts");
                await ArchenemyLogger.Log
                (
                    $"Fucking got fool {context.Message.Author} in channel {context.Message.Channel} (ID {context.Message.Channel.Id}) with Test service",
                    "Discord"
                );
            }

            if (await MatchesRegex(message, DN_REGEX) != null)
            {
                int randomNumber = random.Next(50, 60);
                await ArchenemyLogger.Log($"Message contained dn! Rolled a {randomNumber} as random chance number", "Discord");
                if (randomNumber == 56 || context.User.Id is 140169451994611712 or 746754218022666340)
                {
                    string acronym = await GetDnAcronym();
                    await context.Channel.SendMessageAsync($"Does dn stand for {acronym} or");
                    await ArchenemyLogger.Log
                        ($"dn means {acronym}, actually (channel {context.Message.Channel} (ID {context.Message.Channel.Id})", "Discord");
                }
            }
        }

        if (await SQLInteracter.IsRepostChannel(context.Channel.Id))
            await EnsureNotRepost(message, context);
            
    }

        
        
    private static async Task<string> GetDnAcronym()
    {
        Task<string> adjectiveRequest = WordWebRequest("https://random-word-form.herokuapp.com/random/adjective/d");
        Task<string> nounRequest = WordWebRequest("https://random-word-form.herokuapp.com/random/noun/n");

        await Task.WhenAll(adjectiveRequest, nounRequest);
        return $"{await adjectiveRequest} {await nounRequest}";
    }

    private static async Task EnsureNotRepost(string? message, SocketCommandContext context)
    {
        await ArchenemyLogger.Log("A message was posted in a no repost channel! Checking...", "Discord");
        Match? match = await AttemptMatchArtLink(message);
        if (match == null)
        {
            await ArchenemyLogger.Log("The message wasn't an art link", "Discord");
            return;
        }

        await ArchenemyLogger.Log($"The message was matched to be an art link! Platform: {match.Groups[1].Value}, ID: {match.Groups[2].Value}. Checking for repost in channel {context.Channel} with ID {context.Channel.Id}", "Discord");
            
        (ulong messageId, ulong channelId)? isRepost = await SQLInteracter.CheckRepost(context.Message, match.Groups[2].Value, Enum.Parse<LinkTypes>(match.Groups[1].Value, true));
        if (!isRepost.HasValue)
        {
            await ArchenemyLogger.Log("Message wasn't a repost", "Discord");
            return;
        }

        await Task.WhenAll(context.Channel.SendMessageAsync($"{context.Message.Author.Username} repost arc", allowedMentions: new AllowedMentions() { MentionRepliedUser = false }, messageReference: new MessageReference(isRepost.Value.messageId, isRepost.Value.channelId))
                           ,ArchenemyLogger.Log("The message was a repost lmao gottem", "Discord"));
    }

    private static async Task<string> WordWebRequest(string url)
    {
        WebRequest request = WebRequest.Create(url);
        await using Stream webStream = (await request.GetResponseAsync()).GetResponseStream();
        using StreamReader reader = new(webStream);
        return (await reader.ReadToEndAsync()).Trim('[', '"', ']');
    }

        
    private static async Task<Match?> AttemptMatchArtLink(string? message)
    {
        return await MatchTwitter(message) ?? (await MatchPixiv(message) ?? await MatchArtstation(message));
            
    }

    private static async Task<Match?> MatchTwitter(string? message)
    {
        return await MatchesRegex(message, TWITTER_REGEX);
    }
    private static async Task<Match?> MatchPixiv(string? message)
    {
        return await MatchesRegex(message, PIXIV_REGEX);
    }
    private static async Task<Match?> MatchArtstation(string? message)
    {
        return await MatchesRegex(message, ARTSTATION_REGEX);
    }

    private static async Task<Match?> MatchesRegex(string? message, [RegexPattern] string pattern)
    {
        if (message == null)
            return null;
            
        try
        {
            Match match = Regex.Match(message, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            return match.Success? match:null;
        }
        catch (RegexMatchTimeoutException)
        {
            await ArchenemyLogger.Log($"The message {message} timed out on the Regex Match of the pattern {pattern}", "Discord");
            return null;
        }
    }
}