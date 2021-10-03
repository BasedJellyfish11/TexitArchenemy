using System;
using System.IO;
using System.Threading.Tasks;
using Discord;

namespace TexitArchenemy.Services.Logger
{
    public static class ArchenemyLogger
    {
        public static async Task Log(string message, string from, bool addDatetime = true)
        {
            Task fileWrite = File.AppendAllTextAsync("log.txt", $"{(addDatetime?DateTime.Now + " " :string.Empty)}{from} {message}");
            Console.WriteLine(message);
            await fileWrite;
        }

        public static async Task Log(LogMessage arg)
        {
            await Log(arg.ToString(), string.Empty, false);
        }
    }
}