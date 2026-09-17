using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Umineko;

// User A gets user B's channel-access role iff A.QuoteIndex >= B.QuoteIndex (ties included).
// A user who hasn't posted a screenshot yet (QuoteIndex == null) counts as index 0 — the
// lowest possible position — so everyone else immediately has access to their channel, and
// they have access to no one else's until they actually post.
public static class UminekoRoleSync
{
    public static async Task SyncRoles(DiscordSocketClient client, SocketGuild guild)
    {
        List<UminekoProgressRecord> progress = await SQLInteracter.GetAllUminekoProgress(guild.Id);
        Dictionary<ulong, RestGuildUser?> users = new();

        foreach (UminekoProgressRecord a in progress)
        {
            foreach (UminekoProgressRecord b in progress)
            {
                if (a.UserId == b.UserId || b.RoleId == null)
                    continue;

                bool shouldHaveAccess = (a.QuoteIndex ?? 0) >= (b.QuoteIndex ?? 0);

                // ponytail: fetch over REST, not the gateway cache — the cache doesn't see our
                // own role changes and was causing missed revokes.
                if (!users.TryGetValue(a.UserId, out RestGuildUser? userA))
                    users[a.UserId] = userA = await client.Rest.GetGuildUserAsync(guild.Id, a.UserId);
                if (userA == null)
                    continue;

                bool hasAccess = userA.RoleIds.Contains(b.RoleId.Value);
                if (shouldHaveAccess == hasAccess)
                    continue;

                string roleName = guild.GetRole(b.RoleId.Value)?.Name ?? $"role {b.RoleId}";

                if (shouldHaveAccess)
                {
                    await userA.AddRoleAsync(b.RoleId.Value);
                    await ArchenemyLogger.Log(
                        $"Umineko role sync: granted {userA} (index {a.QuoteIndex}) '{roleName}' — gates {b.UserId}'s channel, {a.QuoteIndex} >= {b.QuoteIndex}",
                        "Umineko");
                }
                else
                {
                    await userA.RemoveRoleAsync(b.RoleId.Value);
                    await ArchenemyLogger.Log(
                        $"Umineko role sync: revoked {userA} (index {a.QuoteIndex}) '{roleName}' — gates {b.UserId}'s channel, {a.QuoteIndex} no longer >= {b.QuoteIndex}",
                        "Umineko");
                }
            }
        }
    }
}
