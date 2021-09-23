using System;
using System.IO;
using System.Net.Sockets;
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
        private SemaphoreSlim waitReadySemaphore = new SemaphoreSlim(0, 1);
        
        public DiscordBotMain()
        {
            _client = new DiscordSocketClient();
        }

        public async Task Connect()
        {

            await _client.LoginAsync(TokenType.Bot, GetToken());
            await _client.StartAsync();
            _client.Ready += semaphoreShit;
            await waitReadySemaphore.WaitAsync();

        }

        private string GetToken()
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

        private Task semaphoreShit()
        {
            waitReadySemaphore.Release();
            _client.Ready -= semaphoreShit;
            return Task.CompletedTask;
        }


    }
}