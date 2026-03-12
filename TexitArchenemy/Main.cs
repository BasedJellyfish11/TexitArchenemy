using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Discord;
using TexitArchenemy.Services.Logger;
using TexitArchenemy.Services.Uma;

namespace TexitArchenemy;

public static class TexitArchenemy
{
    private static DiscordBotMain? _botMain;
    private static UmaSkillFetcher? _umaFetcher;
    private static int _retryBackoffDelay;
    private static readonly Dictionary<int, HashSet<ulong>> _cachedRuleChannelRelation = new();
    private static async Task Main()
    {
        await ArchenemyLogger.Log("Main started", "Uma");

        AutoResetEvent autoEvent = new AutoResetEvent(false);
        
        _botMain = new DiscordBotMain();
        _umaFetcher = new UmaSkillFetcher();
        _umaFetcher.Start();
        
        Console.CancelKeyPress += End;
        AppDomain.CurrentDomain.ProcessExit += End;
            
        Task discordConnectTask = _botMain.Connect(await SQLInteracter.GetDiscordToken());

        await discordConnectTask;

        autoEvent.WaitOne();
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
        await (_umaFetcher?.StopAsync() ?? Task.CompletedTask);
        await (_botMain?.Disconnect()??Task.CompletedTask);
    }

}