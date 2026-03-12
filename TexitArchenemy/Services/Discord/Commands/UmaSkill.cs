using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class UmaSkillModule : ModuleBase<SocketCommandContext>
{
    private const float SimilarityConfidentThreshold = 0.6f;
    private const float SimilarityFuzzyThreshold = 0.4f;

    [Command("umaskill")]
    [Summary("Looks up an Uma Musume skill by name.")]
    [UsedImplicitly]
    public async Task SearchSkill(params string[] words)
    {
        try
        {
            string query = string.Join(' ', words).Trim();
            if (string.IsNullOrEmpty(query))
            {
                await ReplyAsync("Usage: `!umaskill <name>` — e.g. `!umaskill On Your Left`");
                return;
            }

            await ArchenemyLogger.Log($"Skill search: \"{query}\"", "Uma");

            var results = await SQLInteracter.SearchUmaSkill(query, 5, SimilarityFuzzyThreshold);
            await ArchenemyLogger.Log($"Search returned {results.Count} results", "Uma");

            if (results.Count == 0)
            {
                Embed? embed = new EmbedBuilder()
                    .WithDescription(
                        $"No skill found matching **{query}**.\n" +
                        $"Try a closer match to the skill's exact name.")
                    .WithColor(Color.Red)
                    .Build();
                await ReplyAsync(embed: embed);
                return;
            }

            UmaSkillSearchResult topResult = results[0];
            await ArchenemyLogger.Log($"Top result: \"{topResult.NameEn}\" similarity={topResult.MatchSimilarity:0.00}",
                "Uma");

            if (topResult.MatchSimilarity >= SimilarityConfidentThreshold)
            {
                UmaSkillFullResult? detail = await SQLInteracter.GetUmaSkillFull(topResult.SkillId);
                if (detail == null)
                {
                    await ReplyAsync("Found a match but failed to load skill details.");
                    return;
                }

                var groups = await SQLInteracter.GetUmaSkillGroups(topResult.SkillId);

                List<UmaSkillGroupResult>? geneGroups = null;
                if (detail.GeneSkillId.HasValue)
                    geneGroups = await SQLInteracter.GetUmaSkillGroups(detail.GeneSkillId.Value);

                await ReplyAsync(embed: BuildSkillEmbed(detail, groups, geneGroups));
                await ArchenemyLogger.Log(
                    $"Served skill \"{detail.NameEn}\" (id {detail.SkillId}) for query \"{query}\"", "Uma");
                return;
            }

            await ReplyAsync(embed: BuildDisambiguationEmbed(query, results));
        }
        catch (Exception ex)
        {
            await ArchenemyLogger.Log($"SearchSkill error: {ex}", "Uma");
            await ReplyAsync("An error occurred looking up that skill.");
        }
    }

    // ── Embed builders ────────────────────────────────────────────────────────

    private static Embed BuildSkillEmbed(
        UmaSkillFullResult s,
        List<UmaSkillGroupResult> groups,
        List<UmaSkillGroupResult>? geneGroups)
    {
        EmbedBuilder? builder = new EmbedBuilder()
            .WithTitle(FormatSkillTitle(s.NameEn, s.Rarity))
            .WithColor(RarityColor(s.Rarity));

        if (!s.AvailableInEn)
            builder.WithDescription("⚠️ *This skill is not available in the Global version yet.*");

        bool descriptionPending = s.LastLlmUpdate == null || groups.Count == 0;

        if (descriptionPending)
        {
            builder.AddField("Description", "*Skill description is being generated — check back soon.*");
        }
        else if (groups.Count == 1)
        {
            // Single group — flat field layout
            UmaSkillGroupResult g = groups[0];
            if (g.ReadablePrecondition != null)
                builder.AddField("Requires", g.ReadablePrecondition);
            builder.AddField("Activates when", g.ReadableCondition ?? "*Unknown*");
            if (g.ReadableEffects != null)
                builder.AddField("Effect", g.ReadableEffects);
        }
        else
        {
            // Multiple groups — each as its own field
            for (int i = 0; i < groups.Count; i++)
            {
                UmaSkillGroupResult g = groups[i];
                StringBuilder sb = new();

                if (g.ReadablePrecondition != null)
                    sb.AppendLine($"**Requires:** {g.ReadablePrecondition}");

                sb.AppendLine($"**When:** {g.ReadableCondition ?? "*Unknown*"}");

                if (g.ReadableEffects != null)
                    sb.AppendLine($"**Effect:** {g.ReadableEffects}");

                builder.AddField($"Option {i + 1}", sb.ToString().TrimEnd());
            }
        }

        if (s.BaseTimeMs.HasValue)
            builder.AddField("Duration", $"{s.BaseTimeMs.Value / 10000.0:0.##}s", true);

        if (s.CharacterNames != null)
            builder.AddField("Characters", s.CharacterNames);

        // Gene version section
        if (s.GeneSkillId != null && geneGroups != null)
        {
            StringBuilder geneSb = new();
            geneSb.AppendLine($"**{s.GeneNameEn ?? "Inherited version"}**");

            if (geneGroups.Count == 1)
            {
                if (geneGroups[0].ReadableEffects != null)
                    geneSb.AppendLine(geneGroups[0].ReadableEffects);
            }
            else
            {
                for (int i = 0; i < geneGroups.Count; i++)
                    if (geneGroups[i].ReadableEffects != null)
                        geneSb.AppendLine($"Option {i + 1}: {geneGroups[i].ReadableEffects}");
            }

            if (s.GeneBaseTimeMs.HasValue)
                geneSb.AppendLine($"Duration: {s.GeneBaseTimeMs.Value / 10000.0:0.##}s");

            builder.AddField("Inherited version", geneSb.ToString().TrimEnd());
        }

        if (descriptionPending)
            builder.WithFooter("Skill descriptions are being generated — raw data only for now.");

        return builder.Build();
    }

    private static Embed BuildDisambiguationEmbed(
        string query, List<UmaSkillSearchResult> results)
    {
        StringBuilder sb = new();
        sb.AppendLine($"No confident match for **{query}**. Did you mean:");
        sb.AppendLine();
        foreach (UmaSkillSearchResult r in results.Take(3))
            sb.AppendLine($"• **{r.NameEn}** — `!umaskill {r.NameEn}`");

        return new EmbedBuilder()
            .WithTitle("Skill not found")
            .WithDescription(sb.ToString())
            .WithColor(Color.Orange)
            .Build();
    }

    // ── Formatting helpers ────────────────────────────────────────────────────

    private static string FormatSkillTitle(string name, int rarity)
    {
        string stars = new('★', rarity);
        return $"{stars} {name}";
    }

    private static Color RarityColor(int rarity)
    {
        return rarity switch
        {
            1 => new Color(0xAAAAAA), // grey   — common
            2 => new Color(0x4FC3F7), // blue   — uncommon
            3 => new Color(0xFFD700), // gold   — rare
            _ => new Color(0xFF69B4) // pink   — unique/event
        };
    }
}