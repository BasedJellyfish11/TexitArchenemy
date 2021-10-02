using System;
using System.IO;
using System.Threading.Tasks;

namespace TexitArchenemy.Services.Logger
{
    public static class ArchenemyLogger
    {
        public static async Task Log(string message)
        {
            Task fileWrite = File.AppendAllTextAsync("log.txt", message);
            Console.WriteLine(message);
            await fileWrite;
        }
    }
}