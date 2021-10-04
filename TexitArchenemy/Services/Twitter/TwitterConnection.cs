using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;
using Tweetinvi;
using Tweetinvi.Models.V2;
using Tweetinvi.Parameters.V2;
using Tweetinvi.Streaming.V2;

namespace TexitArchenemy.Services.Twitter
{
    public class TwitterConnection
    {
        private readonly TwitterClient client;
        private IFilteredStreamV2? stream;


        private TwitterConnection(string apiConsumerKey, string apiConsumerSecret, string bearerToken)
        {
            // Connect to twitter
            client = new TwitterClient(apiConsumerKey, apiConsumerSecret, bearerToken);
            SQLInteracter.OnAddTwitterRule += AddRule;

        }
        public TwitterConnection(TwitterAuth auth) : this(auth.apiKey, auth.apiSecret, auth.apiToken) { }
         

         public async Task StartStream(EventHandler<Tweetinvi.Events.V2.FilteredStreamTweetV2EventArgs> toHook, int secondsDelay = 0)
        {
            // Delete and readd rules

            FilteredStreamRuleV2[] currentRules =(await client.StreamsV2.GetRulesForFilteredStreamV2Async()).Rules;
            if(currentRules.Length > 0)
                await client.StreamsV2.DeleteRulesFromFilteredStreamAsync(currentRules);
            
            
            await client.StreamsV2.AddRulesToFilteredStreamAsync((await DeserializeRules()).Select(x => new FilteredStreamRuleConfig(x.value, x.tag.ToString())).ToArray());
            
            stream = client.StreamsV2.CreateFilteredStream();
            stream.TweetReceived += toHook;

            // Need to wait a bit after adding the rules - Twitter literally says "a minute" so let's take that at face value
            await Task.Delay(60 * 1000);
            await Task.Delay(secondsDelay * 1000);
            await ArchenemyLogger.Log("Starting stream", "Twitter");

            try {
                await stream.StartAsync(); // This only finishes on disconnection and it's by throwing
            }
            catch (IOException e)
            {
                await ArchenemyLogger.Log($"Stream was disconnected! Exception was {e} Waiting...", "Twitter");
            } 

        }

         private static async Task<HashSet<TwitterRule>> DeserializeRules()
        {
            return await SQLInteracter.GetTwitterRules();
        }

        private async Task AddRule(TwitterRule rule)
        {
            await client.StreamsV2.AddRulesToFilteredStreamAsync(new FilteredStreamRuleConfig(rule.value, rule.tag.ToString()));
        }

        public static string BuildFollowUseRule(string user, RetweetsMode retweets)
        {
            string ruleValue = retweets switch
            {
                RetweetsMode.OnlyRetweets => $"from:{user} is:retweet",
                RetweetsMode.RetweetsOff => $"from:{user} -is:retweet",
                RetweetsMode.RetweetsOn => $"from:{user}",
                _ => throw new ArgumentException($"Follow User was given RetweetsMode {retweets} which is not implemented")
            };

            return ruleValue;
        }

        public async Task Disconnect()
        {
            stream?.StopStream();
            await ArchenemyLogger.Log("Stream has been stopped through code", "Twitter");
        }

    }
}