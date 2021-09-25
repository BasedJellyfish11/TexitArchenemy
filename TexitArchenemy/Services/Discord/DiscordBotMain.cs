using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Text.Json;

namespace TexitArchenemy.Services.Discord
{
    public class DiscordBotMain
    {

        private readonly DiscordSocketClient _client;
        private readonly SemaphoreSlim _waitReadySemaphore = new(0, 1);
        
        public DiscordBotMain()
        {
            _client = new DiscordSocketClient();
        }

        public async Task Connect()
        {

            await _client.LoginAsync(TokenType.Bot, GetToken());
            await _client.StartAsync();
            _client.Ready += semaphoreShit;
            await _waitReadySemaphore.WaitAsync();
            await _client.SetActivityAsync(new Game("Texit", ActivityType.Watching));

        }

        private static string GetToken()
        {
            return JsonSerializer.Deserialize<DiscordAuth>(File.ReadAllText("config/Discord/auth.json")).token;
        }

        public async Task SendMessage(string message, ulong channel)
        {
            if (_client.GetChannel(channel) is not SocketTextChannel textChannel)
            {
                Console.WriteLine("Couldn't get the text channel!");
                return;
            }
                
            await textChannel.SendMessageAsync(message);
        }

        public async Task Disconnect()
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
            _client.Dispose();
            semaphoreShit().Dispose();
        }
        
        
        
        private Task semaphoreShit()
        {
            _waitReadySemaphore.Release();
            _client.Ready -= semaphoreShit;
            return Task.CompletedTask;
        }




    }
}