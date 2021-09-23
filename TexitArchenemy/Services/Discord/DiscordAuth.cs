using System.Text.Json.Serialization;

namespace TexitArchenemy.Services.Discord
{
    public struct DiscordAuth
    {
        [JsonInclude]
        public string token;
    }
}