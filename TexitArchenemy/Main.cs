using System;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using TexitArchenemy.Services.Discord;
using TexitArchenemy.Services.Twitter;
using Tweetinvi.Events.V2;
using Tweetinvi.Models.V2;

namespace TexitArchenemy
{
    public static class Program
    {
        private static DiscordBotMain _botMain;
        private static TwitterConnection _twitter;
        private static async Task Main()
        {
            
            Console.CancelKeyPress += End;
            AppDomain.CurrentDomain.ProcessExit += End;
            
            _botMain = new DiscordBotMain();
            await _botMain.Connect();
            _twitter = new TwitterConnection
                (JsonSerializer.Deserialize<TwitterAuth>(await File.ReadAllTextAsync("config/Twitter/auth.json")));
            Task twitterStream = _twitter.StartStream(printTweet);
            
            
            await Task.WhenAll(twitterStream);
            await End();

        }



        private static async void printTweet(object e, FilteredStreamTweetV2EventArgs args)
        {
            TweetV2 tweet = args.Tweet;
            if (tweet == null)
                throw new InvalidDataException(args.Json);

            if (tweet.ReferencedTweets != null && tweet.ReferencedTweets[0].Type == "retweeted")
                await _botMain.SendMessage($"https://twitter.com/twitter/status/{tweet.ReferencedTweets[0].Id}", 800584635423785041);
                // Console.WriteLine($"https://twitter.com/twitter/status/{tweet.ReferencedTweets[0].Id}");
            else
            {
                await _botMain.SendMessage($"https://twitter.com/twitter/status/{tweet.Id}", 800584635423785041);
                Console.WriteLine($"https://twitter.com/twitter/status/{tweet.Id}");
                Console.WriteLine(args.Tweet.Text);
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
            if(_twitter!= null)
                _twitter.Disconnect();
            if(_botMain != null)
                await _botMain?.Disconnect();
        }

    }
}