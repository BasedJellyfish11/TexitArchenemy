using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TexitArchenemy.Services.DB;
using TexitArchenemy.Services.Discord;
using TexitArchenemy.Services.Twitter;
using Tweetinvi.Events.V2;
using Tweetinvi.Models.V2;

namespace TexitArchenemy
{
    public static class Program
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
            Task twitterStream = _twitter.StartStream(printTweet);
            
            
            await Task.WhenAll(twitterStream);
            
            await End();


        }



        private static async void printTweet(object? e, FilteredStreamTweetV2EventArgs args)
        {
            TweetV2 tweet = args.Tweet;
            if (tweet == null)
            {
                Console.WriteLine($"A non tweet (probably an event) was received!. The JSON reads as follows: {Environment.NewLine} {args.Json}");
                return;
            }

            HashSet<ulong> channelsToSend = new();
            foreach (FilteredStreamMatchingRuleV2? rule in args.MatchingRules)
            {
                await SQLInteracter.GetTwitterRuleChannels(int.Parse(rule.Tag), channelsToSend);
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