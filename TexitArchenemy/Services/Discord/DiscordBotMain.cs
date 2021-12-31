using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord
{
    public class DiscordBotMain
    {

        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _commandHandler;
        private readonly SemaphoreSlim _waitReadySemaphore = new(0, 1);
        
        public DiscordBotMain()
        {
            DiscordSocketConfig config = new()
            {
                GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds,
            };
            _client = new DiscordSocketClient(config);
            _client.Log += ArchenemyLogger.Log;
            
            CommandService commandService = new(new CommandServiceConfig(){CaseSensitiveCommands = false, SeparatorChar = ';'});
            _commandHandler = new CommandHandler(_client, commandService);

        }

        public async Task Connect(string token)
        {
            Task installCommands = _commandHandler.InstallCommandsAsync();
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            _client.Ready += semaphoreShit;
            await Task.WhenAll(_waitReadySemaphore.WaitAsync(), installCommands);
            await _client.SetActivityAsync(new Game("Texit", ActivityType.Watching));

        }
        
        public async Task SendMessage(string message, ulong channel)
        {
            if (_client.GetChannel(channel) is not SocketTextChannel textChannel)
            {
                await ArchenemyLogger.Log("Couldn't get the text channel!", "Discord");
                return;
            }
                
            await textChannel.SendMessageAsync(message);
        }

        public async Task Disconnect()
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
            _client.Dispose();
            _waitReadySemaphore.Dispose();
        }
        
        private Task semaphoreShit()
        {
            _client.Ready -= semaphoreShit;
            _waitReadySemaphore.Release();
            return Task.CompletedTask;
        }




    }
}