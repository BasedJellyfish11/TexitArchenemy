using System;
using System.IO;
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
        private static async Task Main()
        {

            _botMain = new DiscordBotMain();
            await _botMain.Connect();
            TwitterConnection twitter = new TwitterConnection
                (JsonSerializer.Deserialize<TwitterAuth>(await File.ReadAllTextAsync("config/Twitter/auth.json")));
            Task twitterStream = twitter.StartStream(printTweet);
            
            Task.WaitAll(twitterStream);

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
        
    
    }
}