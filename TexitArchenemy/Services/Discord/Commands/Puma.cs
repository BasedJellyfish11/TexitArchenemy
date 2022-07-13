using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;

namespace TexitArchenemy.Services.Discord.Commands;

public class PumaModule: ModuleBase<SocketCommandContext>
{
    private static readonly Random random = new();
    
    [Command("Puma")]
    [Summary("RRRRRRRRRAAAAAAAAAAAGH")]
    [UsedImplicitly]
    public async Task Puma()
    {
        List<string> files = Directory.EnumerateFiles("Venom/").ToList();
        
        await Context.Channel.SendFileAsync(files[random.Next(0, files.Count)]);
            
    }
}