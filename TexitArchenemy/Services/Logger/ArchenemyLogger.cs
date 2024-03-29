﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;

namespace TexitArchenemy.Services.Logger;

public static class ArchenemyLogger
{
    private static readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    public static async Task Log(string message, string from, bool addDatetime = true)
    {
        await _semaphoreSlim.WaitAsync();
        Directory.CreateDirectory("Logger");
        Task fileWrite = File.AppendAllTextAsync(@"Logger\log.txt", $"{(addDatetime?" "+ DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss")  + " " :string.Empty)}{from} {message} {Environment.NewLine}");
        Console.WriteLine($"{(addDatetime?" " +DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss") + " " :string.Empty)}{from}\t{message}");
        await fileWrite;
        _semaphoreSlim.Release();
    }

    public static async Task Log(LogMessage arg)
    {
        await Log(arg.Message, arg.Source);
    }
}