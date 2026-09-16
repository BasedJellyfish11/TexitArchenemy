using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TexitArchenemy.Services.Database;

namespace TexitArchenemy.Services.Umineko;

// User A gets user B's channel-access role iff A.QuoteIndex >= B.QuoteIndex (ties included).
// Neither side counts until they've posted at least one screenshot (QuoteIndex != null).
public static class UminekoRoleSync
{
    public static async Task SyncRoles(SocketGuild guild)
    {
        List<UminekoProgressRecord> progress = await SQLInteracter.GetAllUminekoProgress(guild.Id);
        Dictionary<ulong, IGuildUser?> users = new();

        foreach (UminekoProgressRecord a in progress)
        {
            foreach (UminekoProgressRecord b in progress)
            {
                if (a.UserId == b.UserId || b.RoleId == null)
                    continue;

                bool shouldHaveAccess = a.QuoteIndex.HasValue && b.QuoteIndex.HasValue && a.QuoteIndex >= b.QuoteIndex;

                if (!users.TryGetValue(a.UserId, out IGuildUser? userA))
                    users[a.UserId] = userA = (IGuildUser?)guild.GetUser(a.UserId) ?? await ((IGuild)guild).GetUserAsync(a.UserId);
                if (userA == null)
                    continue;

                bool hasAccess = userA.RoleIds.Contains(b.RoleId.Value);
                if (shouldHaveAccess && !hasAccess)
                    await userA.AddRoleAsync(b.RoleId.Value);
                else if (!shouldHaveAccess && hasAccess)
                    await userA.RemoveRoleAsync(b.RoleId.Value);
            }
        }
    }
}
