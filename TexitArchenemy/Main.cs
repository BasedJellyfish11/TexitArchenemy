using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TexitArchenemy.Services.DB;
using TexitArchenemy.Services.Discord;
using TexitArchenemy.Services.Logger;
using TexitArchenemy.Services.Twitter;
using Tweetinvi.Events.V2;
using Tweetinvi.Models.V2;

namespace TexitArchenemy
{
    public static class TexitArchenemy
    {
        private static DiscordBotMain? _botMain;
        private static TwitterConnection? _twitter;
        private static async Task Main()
        {
            _botMain = new DiscordBotMain();
            _twitter = new TwitterConnection(await SQLInteracter.GetTwitterToken());
            
            Console.CancelKeyPress += End;
            AppDomain.CurrentDomain.ProcessExit += End;
            
            await _botMain.Connect(await SQLInteracter.GetDiscordToken());
            Task twitterStream = _twitter.StartStream(PostTweet);
            
            
            await Task.WhenAll(twitterStream);
            
            await End();
            
        }
        

        private static async void PostTweet(object? e, FilteredStreamTweetV2EventArgs args)
        {
            TweetV2 tweet = args.Tweet;
            if (tweet == null)
            {
                await ArchenemyLogger.Log($"A non tweet (probably an event) was received!. The JSON reads as follows: {Environment.NewLine} {args.Json}", "Twitter");
                return;
            }

            HashSet<ulong> channelsToSend = new();
            foreach (FilteredStreamMatchingRuleV2? rule in args.MatchingRules)
            {
                channelsToSend.UnionWith(await SQLInteracter.GetTwitterRuleChannels(int.Parse(rule.Tag)));
            }

            foreach (ulong channelID in channelsToSend)
            {            
                if (tweet.ReferencedTweets[0]?.Type == "retweeted")
                    await _botMain!.SendMessage($"https://twitter.com/twitter/status/{tweet.ReferencedTweets[0].Id}", channelID);
                else
                    await _botMain!.SendMessage($"https://twitter.com/twitter/status/{tweet.Id}", channelID);
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
}