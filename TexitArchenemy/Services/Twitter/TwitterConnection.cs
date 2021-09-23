using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models.V2;
using Tweetinvi.Parameters.V2;
using Tweetinvi.Streaming.V2;

namespace TexitArchenemy.Services.Twitter
{
    public class TwitterConnection
    {
        private List<TwitterRule> rules;
        private readonly TwitterClient client;


         public TwitterConnection(string apiConsumerKey, string apiConsumerSecret, string bearerToken)
        {
            // Connect to twitter
            client = new TwitterClient(apiConsumerKey, apiConsumerSecret, bearerToken);
            rules = DeserializeRules();

        }
        
         public TwitterConnection(TwitterAuth auth) : this(auth.apiKey, auth.apiSecret, auth.apiToken) { } 


             public async Task StartStream(EventHandler<Tweetinvi.Events.V2.FilteredStreamTweetV2EventArgs> toHook)
        {
            // Delete and readd rules
            
            var rules2 = DeserializeRules();
            
            FilteredStreamRuleV2[] currentRules =(await client.StreamsV2.GetRulesForFilteredStreamV2Async()).Rules;
            if(currentRules.Length > 0)
                await client.StreamsV2.DeleteRulesFromFilteredStreamAsync(currentRules);
            
            await client.StreamsV2.AddRulesToFilteredStreamAsync(rules.Select(x => new FilteredStreamRuleConfig(x.value, x.tag)).ToArray());
            
            IFilteredStreamV2 stream = client.StreamsV2.CreateFilteredStream();
            stream.TweetReceived += toHook;

            // Need to wait a bit after adding the rules - Twitter literally says "a minute" so let's take that at face value
            await Task.Delay(60 * 1000);
            
            Console.WriteLine("Starting stream");

            await stream.StartAsync(); // This never finishes btw so it's literally just blocking 

        }

        private List<TwitterRule> DeserializeRules()
        {
            try {
                return JsonSerializer.Deserialize<List<TwitterRule>>(File.ReadAllText("config/Twitter/rules.json"));
            }
            catch (FileNotFoundException)
            {
                return new List<TwitterRule>();
            }
        }
        
        private async Task<bool> SerializeRules()
        {
            string serialization = JsonSerializer.Serialize(rules);
            await File.WriteAllTextAsync("config/Twitter/rules.json", serialization);
            return true;
        }

        public async Task AddRule(string value, string tag)
        {
            TwitterRule rule = new TwitterRule(value, tag);
            
            if (rules.Contains(rule))
            {
                Console.WriteLine("This rule already exists, exiting");
                return;
            }

            Console.WriteLine($"Adding rule {rule.value} with tag {rule.tag}");
            rules.Add(rule);

            await Task.WhenAll( SerializeRules(),
                                client.StreamsV2.AddRulesToFilteredStreamAsync(new FilteredStreamRuleConfig(rule.value, rule.tag))
                                );
        }

        public string BuildFollowUseRule(string user, RetweetsMode retweets)
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

    }
}