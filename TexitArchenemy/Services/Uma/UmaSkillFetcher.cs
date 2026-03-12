using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TexitArchenemy.Services.Database;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Uma;

public class UmaSkillFetcher
{
    private const int LlmCallDelayMs = 3_000;
    private const int MaxBackoffMs = 30 * 60 * 1000;
    private const string LlmModel = "openai/gpt-oss-120b";
    private const string ManifestUrl = "https://gametora.com/data/manifests/umamusume.json";
    private const string DataBaseUrl = "https://gametora.com/data/umamusume/";

private const string LlmSystemPrompt = """
    You are translating a single Uma Musume horse racing skill condition group into human-readable English.
    Respond ONLY with a JSON object — no preamble, no markdown, no code fences.

    Required format:
    { "precondition": "<one sentence, ≤500 chars>", "condition": "<one sentence, ≤500 chars>", "effects": "<one sentence, ≤300 chars>" }

    Rules:
    - precondition: a state that was established EARLIER in the race (e.g. something that happened before the current moment). Omit this field entirely if there is no precondition.
    - condition: what is true RIGHT NOW at the exact moment this group fires.
    - effects: describe what this group does. Divide raw values by 10000 (e.g. 4500 → +0.45).
    - Be concise. No fluff.
    - If a condition variable has no definition provided, include it literally rather than skipping it.
    - If order and order_rate translate to the same place, mention it only once.
    - Never output "none", "n/a", "-", or empty strings. Omit optional fields instead.

    Style guide:
    - Capitalize the first word of every sentence
    - Never use the word "horse". "uma" or "girl" are preferred.
    
    Terminology translations (use these when a matching variable is present):
    Phases: phase==0=Early Race, phase==1=Mid Race, phase==2=Late Race, phase==3=Final Spurt. phase>=2=Late Race or later.
    Order: order==1=1st place, order==2=2nd place, order<=3=top 3, order>=5=5th place or worse. Lower order number = better position.
    Order rate (9 runners total, lower % = better position): order_rate<=20=top 2, order_rate<=50=top 5, order_rate>=50=5th or worse, order_rate>=65=6th or worse, order_rate>=75=7th or worse, order_rate>=90=8th or worse. Always express as a place number, never as a percentage.
    Running styles: running_style==1=Front Runner, ==2=Pace Chaser, ==3=Late Surger, ==4=End Closer.
    Distance type: distance_type==1=Sprint, ==2=Mile, ==3=Medium, ==4=Long.
    IMPORTANT: distance_rate is NOT a race phase. distance_rate>=50&distance_rate<=60 means 'between 50% and 60% of the race distance completed' — do NOT translate this as Mid Race or any phase name.
    """;

    private const string LlmEffectsOnlySystemPrompt = """
                                                      You are translating the effects of a single Uma Musume horse racing skill condition group into human-readable English.
                                                      Respond ONLY with a JSON object — no preamble, no markdown, no code fences.

                                                      Required format:
                                                      { "effects": "<one sentence, ≤300 chars>" }

                                                      Rules:
                                                      - Describe only the effects. Divide raw values by 10000 (e.g. 4500 → +0.45).
                                                      - Be concise. No fluff.
                                                      """;

    private const string KeySkills = "skills";
    private const string KeySkillConditions = "static/skill_conditions";
    private const string KeyCharacters = "characters";
    private const string KeyCards = "character-cards";
    private static readonly TimeSpan FetchInterval = TimeSpan.FromHours(8);
    private readonly HttpClient _groq = new();

    private readonly HttpClient _http = new();
    private CancellationTokenSource? _cts;
    private Task? _runLoop;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _runLoop = RunLoop(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_runLoop != null)
            await _runLoop.ConfigureAwait(false);
    }

    private async Task RunLoop(CancellationToken ct)
    {
        await ArchenemyLogger.Log("UmaSkillFetcher RunLoop started", "Uma");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FetchCycle(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await ArchenemyLogger.Log($"UmaSkillFetcher error: {ex}", "Uma");
            }

            await Task.Delay(FetchInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task FetchCycle(CancellationToken ct)
    {
        await ArchenemyLogger.Log("Starting Uma skill fetch cycle", "Uma");

        var manifest = await FetchJson<Dictionary<string, string>>(ManifestUrl, ct);
        if (manifest == null) return;

        if (await HashChanged(KeySkillConditions, manifest, ct))
        {
            await ArchenemyLogger.Log("Skill conditions changed — reseeding condition types", "Uma");
            await FetchAndSeedConditionTypes(manifest[KeySkillConditions], ct);
            await UpsertManifestHash(KeySkillConditions, manifest[KeySkillConditions]);
        }

        if (await HashChanged(KeyCharacters, manifest, ct))
        {
            await ArchenemyLogger.Log("Characters changed — upserting characters", "Uma");
            await FetchAndUpsertCharacters(manifest[KeyCharacters], ct);
            await UpsertManifestHash(KeyCharacters, manifest[KeyCharacters]);
        }

        if (await HashChanged(KeyCards, manifest, ct))
        {
            await ArchenemyLogger.Log("Cards changed — upserting cards", "Uma");
            await FetchAndUpsertCards(manifest[KeyCards], ct);
            await UpsertManifestHash(KeyCards, manifest[KeyCards]);
        }

        if (await HashChanged(KeySkills, manifest, ct))
        {
            await ArchenemyLogger.Log("Skills changed — processing skills", "Uma");
            await FetchAndUpsertSkills(manifest[KeySkills], ct);
            await UpsertManifestHash(KeySkills, manifest[KeySkills]);
        }
        else
        {
            await ArchenemyLogger.Log("No skill changes detected", "Uma");
        }

        var needsLlm = await SQLInteracter.GetUmaSkillsMissingDescriptions();
        if (needsLlm.Count > 0)
            await GenerateLlmDescriptions(needsLlm, ct);
        else
            await ArchenemyLogger.Log("All skill descriptions up to date", "Uma");
    }

    private async Task<bool> HashChanged(
        string key, Dictionary<string, string> manifest, CancellationToken ct)
    {
        if (!manifest.TryGetValue(key, out string? newHash)) return false;
        string? stored = await SQLInteracter.GetUmaManifestHash(key);
        return stored != newHash;
    }

    private static Task UpsertManifestHash(string key, string hash)
    {
        return SQLInteracter.UpsertUmaManifestHash(key, hash);
    }

    private async Task FetchAndSeedConditionTypes(string hash, CancellationToken ct)
    {
        string url = BuildDataUrl(KeySkillConditions, hash);
        var entries = await FetchJson<ConditionTypeEntry[]>(url, ct);
        if (entries == null) return;

        await SQLInteracter.ReseedUmaConditionTypes(entries
            .Select(e => new ConditionTypeRecord(e.Name, e.Desc, e.Example, e.ExampleMeaning))
            .ToArray());
    }

    private async Task FetchAndUpsertCharacters(string hash, CancellationToken ct)
    {
        string url = BuildDataUrl(KeyCharacters, hash);
        using JsonDocument doc = await FetchRawJson(url, ct);
        if (doc == null) return;

        await ArchenemyLogger.Log("Starting character enumeration", "Uma");
        foreach (JsonElement el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("playable", out JsonElement playable) || !playable.GetBoolean())
                continue;

            try
            {
                await SQLInteracter.UpsertUmaCharacter(ParseCharacterElement(el));
            }
            catch (Exception ex)
            {
                int? charId = el.TryGetProperty("char_id", out JsonElement id) ? id.GetInt32() : null;
                await ArchenemyLogger.Log(
                    $"Failed to parse character {charId?.ToString() ?? "unknown"}: {ex.Message}", "Uma");
            }
        }
    }

    private async Task FetchAndUpsertCards(string hash, CancellationToken ct)
    {
        string url = BuildDataUrl(KeyCards, hash);
        using JsonDocument doc = await FetchRawJson(url, ct);
        if (doc == null) return;

        await ArchenemyLogger.Log("Starting card enumeration", "Uma");
        foreach (JsonElement el in doc.RootElement.EnumerateArray())
            try
            {
                int cardId = el.GetProperty("card_id").GetInt32();
                int charId = el.GetProperty("char_id").GetInt32();
                string nameEn = el.TryGetProperty("name_en", out JsonElement ne) ? ne.GetString() ?? "" : "";
                string? version = el.TryGetProperty("version", out JsonElement ver) ? ver.GetString() : null;
                string? titleEnGl = el.TryGetProperty("title_en_gl", out JsonElement tit) ? tit.GetString() : null;

                if (string.IsNullOrWhiteSpace(titleEnGl)) titleEnGl = null;

                await SQLInteracter.UpsertUmaCard(new UmaCardRecord(cardId, charId, nameEn, version, titleEnGl));
            }
            catch (Exception ex)
            {
                int? cardId = el.TryGetProperty("card_id", out JsonElement id) ? id.GetInt32() : null;
                await ArchenemyLogger.Log(
                    $"Failed to parse card {cardId?.ToString() ?? "unknown"}: {ex.Message}", "Uma");
            }
    }

    private async Task FetchAndUpsertSkills(string hash, CancellationToken ct)
    {
        string url = BuildDataUrl(KeySkills, hash);
        using JsonDocument doc = await FetchRawJson(url, ct);
        if (doc == null) return;

        foreach (JsonElement skill in doc.RootElement.EnumerateArray())
        {
            var records = ProcessSkillElement(skill, null);
            foreach (UmaSkillRecord record in records)
            {
                await SQLInteracter.UpsertUmaSkill(record);

                if (record.ParentSkillId == null && skill.TryGetProperty("char", out JsonElement chars))
                {
                    int[] charIds = chars.EnumerateArray()
                        .Select(c => c.GetInt32())
                        .ToArray();
                    await SQLInteracter.UpsertUmaSkillCharacters(record.SkillId, charIds);
                }
            }
        }
    }

    private static List<UmaSkillRecord> ProcessSkillElement(JsonElement el, int? parentId)
    {
        var results = new List<UmaSkillRecord>();

        int skillId = el.GetProperty("id").GetInt32();
        string nameEn = el.TryGetProperty("name_en", out JsonElement ne) ? ne.GetString() ?? "" : "";
        string nameJp = el.TryGetProperty("jpname", out JsonElement nj) ? nj.GetString() ?? "" : "";
        int rarity = el.TryGetProperty("rarity", out JsonElement r) ? r.GetInt32() : 1;
        int? iconId = el.TryGetProperty("iconid", out JsonElement ic) ? ic.GetInt32() : null;

        JsonElement locEn = default;
        bool availableInEn = el.TryGetProperty("loc", out JsonElement loc)
                             && loc.TryGetProperty("en", out locEn);

        JsonElement conditionGroupsEl = default;
        if (availableInEn && locEn.TryGetProperty("condition_groups", out JsonElement enCg))
            conditionGroupsEl = enCg;
        else if (el.TryGetProperty("condition_groups", out JsonElement rootCg))
            conditionGroupsEl = rootCg;

        string conditionsJson = conditionGroupsEl.ValueKind != JsonValueKind.Undefined
            ? conditionGroupsEl.GetRawText()
            : "[]";

        int? baseTimeMs = null;
        if (conditionGroupsEl.ValueKind == JsonValueKind.Array)
            foreach (JsonElement group in conditionGroupsEl.EnumerateArray())
                if (group.TryGetProperty("base_time", out JsonElement bt))
                {
                    baseTimeMs = bt.GetInt32();
                    break;
                }

        results.Add(new UmaSkillRecord(
            skillId,
            parentId,
            nameEn,
            nameJp,
            rarity,
            iconId,
            availableInEn,
            conditionsJson,
            baseTimeMs
        ));

        if (el.TryGetProperty("gene_version", out JsonElement gene))
            results.AddRange(ProcessSkillElement(gene, skillId));

        return results;
    }

    // ── LLM description generation ────────────────────────────────────────────

    private async Task GenerateLlmDescriptions(
        List<(int SkillId, string ConditionsJson)> queue, CancellationToken ct)
    {
        await ArchenemyLogger.Log($"Generating LLM descriptions for {queue.Count} skills", "Uma");
        EnsureGroqClientConfigured();

        foreach ((int skillId, string conditionsJson) in queue)
        {
            if (ct.IsCancellationRequested) break;

            int backoffMs = LlmCallDelayMs;
            while (true)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    bool success = await GenerateSingleSkillDescription(skillId, conditionsJson, ct);
                    if (success) break;
                }
                catch (Exception ex)
                {
                    await ArchenemyLogger.Log(
                        $"LLM generation failed for skill {skillId}: {ex.Message}", "Uma");
                    break;
                }

                await ArchenemyLogger.Log(
                    $"Skill {skillId} — 429, retrying in {backoffMs / 1000}s", "Uma");
                await Task.Delay(backoffMs, ct);
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }

        await ArchenemyLogger.Log("LLM generation cycle complete", "Uma");
    }

    /// <summary>
    ///     Generates readable descriptions for all condition groups of a skill.
    ///     Returns false only on 429 (caller retries). On success, writes hash.
    /// </summary>
    private async Task<bool> GenerateSingleSkillDescription(
        int skillId, string conditionsJson, CancellationToken ct)
    {
        await ArchenemyLogger.Log($"Processing skill {skillId}", "Uma");

        // Parse condition groups from JSON
        var rawGroups = ParseConditionGroupsJson(conditionsJson);
        if (rawGroups.Count == 0)
        {
            await ArchenemyLogger.Log($"Skill {skillId} has no condition groups — skipping", "Uma");
            await SQLInteracter.UpdateSkillConditionsHash(skillId);
            return true;
        }

        // Fetch parent groups if this is a gene version
        int? parentId = await SQLInteracter.GetUmaSkillParentId(skillId);
        List<UmaSkillGroupResult>? parentGroups = null;
        if (parentId.HasValue)
        {
            parentGroups = await SQLInteracter.GetUmaSkillGroups(parentId.Value);
            if (parentGroups.Count == 0)
            {
                // Parent not yet processed — skip, will be retried next cycle
                await ArchenemyLogger.Log(
                    $"Skill {skillId} parent {parentId} not yet processed — deferring", "Uma");
                return true;
            }
        }

        // Delete existing groups before re-generating
        await SQLInteracter.DeleteUmaSkillReadableGroups(skillId);

        // Generate per group
        for (int i = 0; i < rawGroups.Count; i++)
        {
            JsonElement group = rawGroups[i];

            string? readablePrecondition;
            string? readableCondition;
            string? readableEffects;

            if (parentGroups != null)
            {
                // Gene version — inherit condition/precondition from parent at same index
                UmaSkillGroupResult parentGroup = i < parentGroups.Count ? parentGroups[i] : parentGroups[^1];
                readablePrecondition = parentGroup.ReadablePrecondition;
                readableCondition = parentGroup.ReadableCondition;

                // Only call LLM for effects
                string effectsPrompt = BuildEffectsOnlyPrompt(group);
                string? llmResponse = await CallGroq(LlmEffectsOnlySystemPrompt, effectsPrompt, ct);
                if (llmResponse == null) return false; // 429
                await ArchenemyLogger.Log($"Skill {skillId} group {i} effects response: {llmResponse}", "Uma");

                readableEffects = ParseEffectsFromResponse(llmResponse);
            }
            else
            {
                // Full generation
                string[] conditionNames = ExtractConditionNamesFromGroup(group);
                var conditionDocs = await SQLInteracter.GetUmaConditionDescriptions(conditionNames);
                int[] effectTypeIds = ExtractEffectTypeIdsFromGroup(group);
                var effectTypeNames = await SQLInteracter.GetUmaEffectTypeNames(effectTypeIds);

                string prompt = BuildFullGroupPrompt(group, conditionDocs, effectTypeNames);
                string? llmResponse = await CallGroq(LlmSystemPrompt, prompt, ct);
                if (llmResponse == null) return false; // 429
                await ArchenemyLogger.Log($"Skill {skillId} group {i} raw response: {llmResponse}", "Uma");

                (readablePrecondition, readableCondition, readableEffects) =
                    ParseFullGroupResponse(llmResponse);
            }

            await SQLInteracter.UpsertUmaSkillReadableGroup(
                skillId, i, readablePrecondition, readableCondition, readableEffects);

            await ArchenemyLogger.Log($"Skill {skillId} group {i} written", "Uma");

            // Delay between LLM calls (rate limit applies per call, not per skill)
            if (i < rawGroups.Count - 1)
                await Task.Delay(LlmCallDelayMs, ct);
        }

        // All groups succeeded — write hash
        await SQLInteracter.UpdateSkillConditionsHash(skillId);
        await ArchenemyLogger.Log($"Skill {skillId} complete ({rawGroups.Count} group(s))", "Uma");

        // Delay before next skill
        await Task.Delay(LlmCallDelayMs, ct);
        return true;
    }

    // ── Prompt building ───────────────────────────────────────────────────────

    private static string BuildFullGroupPrompt(
        JsonElement group,
        IReadOnlyList<(string name, string desc, string? example, string? meaning)> conditionDocs,
        IReadOnlyDictionary<int, string> effectTypeNames)
    {
        StringBuilder sb = new();

        sb.AppendLine("Condition group (JSON):");
        sb.AppendLine(group.GetRawText());
        sb.AppendLine();

        if (conditionDocs.Count > 0)
        {
            sb.AppendLine("Condition variable definitions:");
            foreach ((string name, string desc, string? example, string? meaning) in conditionDocs)
            {
                sb.Append($"- {name}: {desc}");
                if (example != null && meaning != null)
                    sb.Append($" (e.g. {example} = {meaning})");
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        if (effectTypeNames.Count > 0)
        {
            sb.AppendLine("Effect type meanings:");
            foreach ((int typeId, string typeName) in effectTypeNames)
                sb.AppendLine($"- type {typeId}: {typeName}");
        }

        return sb.ToString();
    }

    private static string BuildEffectsOnlyPrompt(JsonElement group)
    {
        StringBuilder sb = new();
        sb.AppendLine("Condition group (JSON) — describe only the effects:");
        sb.AppendLine(group.GetRawText());
        return sb.ToString();
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    private static (string? precondition, string? condition, string? effects)
        ParseFullGroupResponse(string llmResponse)
    {
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(llmResponse);
            JsonElement root = parsed.RootElement;

            string? pre = root.TryGetProperty("precondition", out JsonElement preEl)
                ? NullIfEmpty(preEl.GetString())
                : null;
            string? cond = root.TryGetProperty("condition", out JsonElement condEl)
                ? NullIfEmpty(condEl.GetString())
                : null;
            string? effects = root.TryGetProperty("effects", out JsonElement effEl)
                ? NullIfEmpty(effEl.GetString())
                : null;

            return (pre, cond, effects);
        }
        catch
        {
            return (null, llmResponse, null);
        }
    }

    private static string? ParseEffectsFromResponse(string llmResponse)
    {
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(llmResponse);
            return parsed.RootElement.TryGetProperty("effects", out JsonElement effEl)
                ? NullIfEmpty(effEl.GetString())
                : null;
        }
        catch
        {
            return llmResponse;
        }
    }

    private static string? NullIfEmpty(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string t = s.Trim().ToLower();
        if (t is "none" or "n/a" or "-") return null;
        return s.Trim();
    }

    // ── Condition group helpers ───────────────────────────────────────────────

    private static List<JsonElement> ParseConditionGroupsJson(string conditionsJson)
    {
        var groups = new List<JsonElement>();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(conditionsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (JsonElement el in doc.RootElement.EnumerateArray())
                    groups.Add(el.Clone());
        }
        catch
        {
            /* malformed JSON — return empty */
        }

        return groups;
    }

    private static string[] ExtractConditionNamesFromGroup(JsonElement group)
    {
        string condition = group.TryGetProperty("condition", out JsonElement c) ? c.GetString() ?? "" : "";
        string precon = group.TryGetProperty("precondition", out JsonElement p) ? p.GetString() ?? "" : "";
        string combined = condition + "&" + precon;

        return Regex.Matches(combined, @"([a-z][a-z0-9_]*)(?:[<>=!])", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();
    }

    private static int[] ExtractEffectTypeIdsFromGroup(JsonElement group)
    {
        if (!group.TryGetProperty("effects", out JsonElement effects)) return Array.Empty<int>();

        return effects.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out _))
            .Select(e => e.GetProperty("type").GetInt32())
            .Distinct()
            .ToArray();
    }

    // ── Groq API ──────────────────────────────────────────────────────────────

    private void EnsureGroqClientConfigured()
    {
        if (!_groq.DefaultRequestHeaders.Contains("Authorization"))
            _groq.DefaultRequestHeaders.Add(
                "Authorization",
                $"Bearer {Environment.GetEnvironmentVariable("GROQ_TOKEN")}");
    }

    private async Task<string?> CallGroq(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        EnsureGroqClientConfigured();

        var body = new
        {
            model = LlmModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        string json = JsonSerializer.Serialize(body);
        StringContent content = new(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _groq.PostAsync(
            "https://api.groq.com/openai/v1/chat/completions", content, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            int retryAfter = 60;
            if (response.Headers.TryGetValues("Retry-After", out var values)
                && int.TryParse(values.FirstOrDefault(), out int parsed))
                retryAfter = parsed;
            await ArchenemyLogger.Log($"Groq 429 — Retry-After: {retryAfter}s", "Uma");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ArchenemyLogger.Log(
                $"Groq returned {(int)response.StatusCode}", "Uma");
            return null;
        }

        string body2 = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(body2);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private async Task<T?> FetchJson<T>(string url, CancellationToken ct)
    {
        try
        {
            string raw = await _http.GetStringAsync(url, ct);
            return JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }
        catch (Exception ex)
        {
            await ArchenemyLogger.Log($"HTTP fetch failed for {url}: {ex.Message}", "Uma");
            return default;
        }
    }

    private async Task<JsonDocument> FetchRawJson(string url, CancellationToken ct)
    {
        string raw = await _http.GetStringAsync(url, ct);
        return JsonDocument.Parse(raw);
    }

    private static UmaCharacterRecord ParseCharacterElement(JsonElement el)
    {
        int? GetInt(string key)
        {
            return el.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : null;
        }

        string? GetStr(string key)
        {
            return el.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }

        int? bust = null, waist = null, hip = null;
        if (el.TryGetProperty("three_sizes", out JsonElement sizes))
        {
            bust = sizes.TryGetProperty("b", out JsonElement b) ? b.GetInt32() : null;
            waist = sizes.TryGetProperty("w", out JsonElement w) ? w.GetInt32() : null;
            hip = sizes.TryGetProperty("h", out JsonElement h) ? h.GetInt32() : null;
        }

        string? rlActive = null, rlCountry = null, rlDeath = null, rlRecord = null;
        long? rlEarnings = null;
        string? rlEarningsOther = null;
        int? rlRaces = null, rlWins = null;

        if (el.TryGetProperty("rl", out JsonElement rl))
        {
            rlActive = rl.TryGetProperty("active", out JsonElement a) ? a.GetString() : null;
            rlCountry = rl.TryGetProperty("country", out JsonElement co) ? co.GetString() : null;
            rlDeath = rl.TryGetProperty("death", out JsonElement d) ? d.GetString() : null;
            rlRecord = rl.TryGetProperty("record", out JsonElement re) ? re.GetString() : null;
            rlEarnings = rl.TryGetProperty("earnings", out JsonElement e) && e.ValueKind == JsonValueKind.Number
                ? e.GetInt64()
                : null;
            rlEarningsOther = rl.TryGetProperty("earnings_other", out JsonElement eo)
                ? eo.GetRawText()
                : null;
            rlRaces = rl.TryGetProperty("races", out JsonElement ra) && ra.ValueKind == JsonValueKind.Number
                ? ra.GetInt32()
                : null;
            rlWins = rl.TryGetProperty("wins", out JsonElement wi) && wi.ValueKind == JsonValueKind.Number
                ? wi.GetInt32()
                : null;
        }

        return new UmaCharacterRecord(
            el.GetProperty("char_id").GetInt32(),
            el.TryGetProperty("en_name", out JsonElement ne) ? ne.GetString() ?? "" : "",
            GetStr("jp_name"),
            GetStr("name_tw"),
            GetStr("url_name"),
            GetInt("height"),
            GetInt("birth_day"),
            GetInt("birth_month"),
            GetInt("birth_year"),
            GetInt("sex"),
            bust,
            waist,
            hip,
            GetStr("va_en"),
            GetStr("va_ja"),
            GetStr("va_link"),
            el.TryGetProperty("playable", out JsonElement pl) ? pl.GetBoolean() : null,
            el.TryGetProperty("playable_en", out JsonElement ple) ? ple.GetBoolean() : null,
            el.TryGetProperty("active_en", out JsonElement ae) && ae.GetBoolean(),
            rlActive,
            rlCountry,
            rlDeath,
            rlEarnings,
            rlEarningsOther,
            rlRaces,
            rlRecord,
            rlWins
        );
    }

    private static string BuildDataUrl(string manifestKey, string hash)
    {
        return $"{DataBaseUrl}{manifestKey}.{hash}.json";
    }
}