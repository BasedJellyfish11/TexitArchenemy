using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Net;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Discord;
using TexitArchenemy.Services.Logger;
using TexitArchenemy.Services.Twitter;
using Tweetinvi.Events.V2;
using Tweetinvi.Models.V2;

namespace TexitArchenemy;

public static class TexitArchenemy
{
    private static DiscordBotMain? _botMain;
    private static TwitterConnection? _twitter;
    private static int _retryBackoffDelay;
    private static readonly Dictionary<int, HashSet<ulong>> _cachedRuleChannelRelation = new();
    private static async Task Main()
    {
        _botMain = new DiscordBotMain();
        _twitter = new TwitterConnection(await SQLInteracter.GetTwitterToken());

            
        Console.CancelKeyPress += End;
        AppDomain.CurrentDomain.ProcessExit += End;
            
        Task discordConnectTask = _botMain.Connect(await SQLInteracter.GetDiscordToken());
        
        foreach (TwitterRule rule in await SQLInteracter.GetTwitterRules())
        {
            _cachedRuleChannelRelation.Add(rule.tag, await SQLInteracter.GetTwitterRuleChannels(rule.tag));
        }

        await discordConnectTask;
        
        _retryBackoffDelay = 0;
        while (true)
        {
            try 
            { 
                await _twitter.StartStream(PostTweet, _retryBackoffDelay); // This will block
            }
            catch (Exception e)
            {
                await ArchenemyLogger.Log(e.Message, "Twitter");
                await _twitter.Disconnect();
                _retryBackoffDelay = _retryBackoffDelay == 0? _retryBackoffDelay + 60:  _retryBackoffDelay *2;
            }
        }
            
            
        // ReSharper disable once FunctionNeverReturns
    }
        

    private static async void PostTweet(object? e, FilteredStreamTweetV2EventArgs args)
    {
        TweetV2 tweet = args.Tweet;
        if (tweet == null)
        {
            await ArchenemyLogger.Log($"A non tweet (probably an event) was received!. The JSON reads as follows: {Environment.NewLine} {args.Json}", "Twitter");
            await (_twitter?.Disconnect() ?? Task.CompletedTask);
            return;
        }
        
        _retryBackoffDelay = 0;
        HashSet<ulong> channelsToSend = new();
        foreach (FilteredStreamMatchingRuleV2? rule in args.MatchingRules)
        {
            channelsToSend.UnionWith(_cachedRuleChannelRelation[int.Parse(rule.Tag)]);
        }

        string tweetID = tweet.ReferencedTweets?[0]?.Type == "retweeted" ? tweet.ReferencedTweets[0].Id : tweet.Id;
        string tweetOriginalOwner = tweet.ReferencedTweets?[0]?.Type == "retweeted" ? args.Includes.Users[1].Username : args.Includes.Users[0].Username;
            
        foreach (ulong channelID in channelsToSend)
        {
            if (await SQLInteracter.IsRepostChannel(channelID) && !await SQLInteracter.PreemptiveRepostCheck(channelID, tweetID, LinkTypes.Twitter)) 
                continue;
                
            bool sent = false;
            while (!sent)
            {
                try
                {
                    await _botMain!.SendMessage($"https://twitter.com/{tweetOriginalOwner}/status/{tweetID}", channelID);
                    sent = true;
                }
                catch (Exception exception) when (exception is HttpRequestException or HttpException or TimeoutException)
                {
                    sent = false;
                    await Task.Delay
                    (
                        _retryBackoffDelay = Math.Min(5 * 1000 * 60, 
                            _retryBackoffDelay == 0 ? _retryBackoffDelay + 60 : _retryBackoffDelay * 2)
                    );
                }
            }

        }
            
    }
        
        
        
        
    private static async void End(object? sender, EventArgs e)
    {
        await End();
    }
    private static async void End(object? sender, ConsoleCancelEventArgs consoleCancelEventArgs)
    {
        await End();
    }

    private static async Task End()
    {
        _twitter?.Disconnect();
        await (_botMain?.Disconnect()??Task.CompletedTask);
    }

}