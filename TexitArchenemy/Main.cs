using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TexitArchenemy.Services.Twitter;
using Tweetinvi.Events.V2;
using Tweetinvi.Models.V2;

namespace TexitArchenemy
{
    public static class Program
    {
        private static async Task Main()
        {
            TwitterConnection twitter = new TwitterConnection
                (JsonSerializer.Deserialize<TwitterAuth>(await File.ReadAllTextAsync(@"D:\Documents\GitHub\TexitArchenemy\TexitArchenemy\Services\Twitter\Auth.json")));
            Task twitterStream = twitter.StartStream(printTweet);
            
            Task.WaitAll(twitterStream);

        }

        private static void printTweet(object e, FilteredStreamTweetV2EventArgs args)
        {
            TweetV2 tweet = args.Tweet;
            if (tweet == null)
                throw new InvalidDataException(args.Json);

            if (tweet.ReferencedTweets != null && tweet.ReferencedTweets[0].Type == "retweeted")
                Console.WriteLine($"https://twitter.com/i/web/status/{tweet.ReferencedTweets[0].Id}");
            else
            {
                Console.WriteLine($"https://twitter.com/i/web/status/{tweet.Id}");
                Console.WriteLine(args.Tweet.Text);
            }


            
        }
        
    
    }
}