using System.Text.Json.Serialization;

namespace TexitArchenemy.Services.Twitter
{
    public struct TwitterAuth
    {
        [JsonInclude]
        public string apiKey;
        [JsonInclude]
        public string apiSecret;
        [JsonInclude]
        public string apiToken;
    }
}