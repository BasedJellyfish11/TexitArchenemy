using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;

namespace TexitArchenemy.Services.Database;

public static class SQLInteracter
{
    private static readonly string CONNECTION_STRING =
        $"Host=localhost;Database=texit_archenemy;" +
        $"Username={Environment.GetEnvironmentVariable("PG_USER")};" +
        $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")};";

    public static async Task<string> GetDiscordToken()
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);
        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.get_discord_creds, connection);

        if (!reader.HasRows)
            throw new InvalidOperationException(NoRowsError(ProcedureNames.get_discord_creds));

        await reader.ReadAsync();
        return reader[DiscordAuthColumns.token].ToString()!;
    }

    public static async Task<(ulong messageId, ulong channelId)?> CheckRepost(
        SocketMessage message, string linkId, LinkTypes linkType)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{CheckRepostParams.channel_id}", NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.message_id}", NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.link_id}", NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.link_type_description}", NpgsqlDbType.Varchar)
        };

        parameters[0].Value = message.Channel.Id.ToString();
        parameters[1].Value = message.Id.ToString();
        parameters[2].Value = linkId;
        parameters[3].Value = linkType.ToString();

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.check_repost, connection, parameters);

        if (!reader.HasRows)
            throw new InvalidOperationException(NoRowsError(ProcedureNames.check_repost));

        await reader.ReadAsync();

        (string, string) stringTuple = ((string)reader[RepostRepositoryColumns.message_id],
            (string)reader[RepostRepositoryColumns.channel_id]);

        if (stringTuple.Item1 == "-1" || stringTuple.Item2 == "-1")
            return null;

        return (ulong.Parse(stringTuple.Item1), ulong.Parse(stringTuple.Item2));
    }

    public static async Task<List<string?>> GetBoxWarmup(int level)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{GetBoxWarmupParams.lesson}", NpgsqlDbType.Integer)
        };
        parameters[0].Value = level;

        NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.get_box_warmup, connection, parameters);

        List<string?> warmups = new();
        while (await reader.ReadAsync())
            warmups.Add(reader[BoxWarmupColumns.warmup].ToString());

        return warmups;
    }

    public static async Task<int> UpdateBoxChallengeProgress(int boxesDrawn, SocketUser user)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{UpdateBoxChallengeProgressParams.user_id}", NpgsqlDbType.Varchar),
            new($"@{UpdateBoxChallengeProgressParams.boxes_drawn}", NpgsqlDbType.Integer)
        };
        parameters[0].Value = user.Id.ToString();
        parameters[1].Value = boxesDrawn;

        NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.update_box_challenge_progress, connection, parameters);

        await reader.ReadAsync();
        return (int)reader[BoxChallengeColumns.boxes_drawn];
    }

    public static async Task<int> GetBoxChallengeProgress(SocketUser user)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{UpdateBoxChallengeProgressParams.user_id}", NpgsqlDbType.Varchar)
        };
        parameters[0].Value = user.Id.ToString();

        NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.get_box_challenge_progress, connection, parameters);

        await reader.ReadAsync();
        return (int)reader[BoxChallengeColumns.boxes_drawn];
    }

    public static async Task<bool> IsRepostChannel(ulong channelID)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{IsRepostChannelParams.channel_id}", NpgsqlDbType.Varchar)
        };
        parameters[0].Value = channelID.ToString();

        NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.is_repost_channel, connection, parameters);

        await reader.ReadAsync();
        return (bool)reader[DiscordChannelsColumns.repost_check];
    }

    public static async Task MarkAsRepostChannel(SocketGuildChannel contextChannel)
    {
        NpgsqlParameter[] parameters =
        {
            new($"@{MarkAsRepostChannelParams.channel_id}", NpgsqlDbType.Varchar),
            new($"@{MarkAsRepostChannelParams.guild_id}", NpgsqlDbType.Varchar)
        };
        parameters[0].Value = contextChannel.Id.ToString();
        parameters[1].Value = contextChannel.Guild.Id.ToString();

        await ExecuteVoidProcedure(ProcedureNames.mark_as_repost_channel, parameters);
    }

    // ---- Umineko progress ----

    public static async Task<UminekoProgressRecord?> GetUminekoProgress(SocketUser user, ulong guildId)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{GetUminekoProgressParams.user_id}", NpgsqlDbType.Varchar) { Value = user.Id.ToString() },
            new($"@{GetUminekoProgressParams.guild_id}", NpgsqlDbType.Varchar) { Value = guildId.ToString() }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.get_umineko_progress, connection, parameters);

        if (!await reader.ReadAsync()) return null;
        return ReadUminekoProgressRecord(reader);
    }

    public static async Task<List<UminekoProgressRecord>> GetAllUminekoProgress(ulong guildId)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new($"@{GetUminekoProgressParams.guild_id}", NpgsqlDbType.Varchar) { Value = guildId.ToString() }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction(ProcedureNames.get_all_umineko_progress, connection, parameters);

        var results = new List<UminekoProgressRecord>();
        while (await reader.ReadAsync())
            results.Add(ReadUminekoProgressRecord(reader));
        return results;
    }

    public static async Task RegisterUminekoUser(SocketUser user, ulong guildId, ulong channelId, ulong roleId)
    {
        NpgsqlParameter[] parameters =
        {
            new($"@{RegisterUminekoUserParams.user_id}", NpgsqlDbType.Varchar) { Value = user.Id.ToString() },
            new($"@{RegisterUminekoUserParams.guild_id}", NpgsqlDbType.Varchar) { Value = guildId.ToString() },
            new($"@{RegisterUminekoUserParams.channel_id}", NpgsqlDbType.Varchar) { Value = channelId.ToString() },
            new($"@{RegisterUminekoUserParams.role_id}", NpgsqlDbType.Varchar) { Value = roleId.ToString() }
        };
        await ExecuteVoidProcedure(ProcedureNames.register_umineko_user, parameters);
    }

    public static async Task UnregisterUminekoUser(SocketUser user, ulong guildId)
    {
        NpgsqlParameter[] parameters =
        {
            new($"@{UnregisterUminekoUserParams.user_id}", NpgsqlDbType.Varchar) { Value = user.Id.ToString() },
            new($"@{UnregisterUminekoUserParams.guild_id}", NpgsqlDbType.Varchar) { Value = guildId.ToString() }
        };
        await ExecuteVoidProcedure(ProcedureNames.unregister_umineko_user, parameters);
    }

    private static UminekoProgressRecord ReadUminekoProgressRecord(NpgsqlDataReader reader)
    {
        return new UminekoProgressRecord(
            ulong.Parse((string)reader[UminekoProgressColumns.user_id]),
            ulong.Parse((string)reader[UminekoProgressColumns.guild_id]),
            ulong.Parse((string)reader[UminekoProgressColumns.channel_id]),
            reader[UminekoProgressColumns.role_id] is string roleId ? ulong.Parse(roleId) : null,
            reader[UminekoProgressColumns.quote_index] as int?,
            reader[UminekoProgressColumns.quote_episode] as short?,
            reader[UminekoProgressColumns.quote_plaintext] as string);
    }

    // ---- Manifest hashes ----

    public static async Task<string?> GetUmaManifestHash(string key)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_key", NpgsqlDbType.Varchar) { Value = key }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_manifest_hash", connection, parameters);

        if (!await reader.ReadAsync()) return null;
        return reader["hash_token"] as string;
    }

    public static async Task UpsertUmaManifestHash(string key, string hash)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_key", NpgsqlDbType.Varchar) { Value = key },
            new("@p_hash", NpgsqlDbType.Varchar) { Value = hash }
        };
        await ExecuteVoidProcedure("upsert_manifest_hash", parameters);
    }

    // ---- Skills ----

    public static async Task UpsertUmaSkill(UmaSkillRecord r)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = r.SkillId },
            new("@p_parent_skill_id", NpgsqlDbType.Integer) { Value = (object?)r.ParentSkillId ?? DBNull.Value },
            new("@p_name_en", NpgsqlDbType.Varchar) { Value = r.NameEn },
            new("@p_name_jp", NpgsqlDbType.Varchar) { Value = (object?)r.NameJp ?? DBNull.Value },
            new("@p_rarity", NpgsqlDbType.Integer) { Value = r.Rarity },
            new("@p_icon_id", NpgsqlDbType.Integer) { Value = (object?)r.IconId ?? DBNull.Value },
            new("@p_available_in_en", NpgsqlDbType.Boolean) { Value = r.AvailableInEn },
            new("@p_condition_groups_json", NpgsqlDbType.Jsonb) { Value = r.ConditionGroupsJson },
            new("@p_base_time_ms", NpgsqlDbType.Integer) { Value = (object?)r.BaseTimeMs ?? DBNull.Value }
        };

        await ExecuteVoidProcedure("upsert_uma_skill", parameters);
    }

    public static async Task UpsertUmaSkillCharacters(int skillId, int[] charIds)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId },
            new("@p_char_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = charIds }
        };
        await ExecuteVoidProcedure("upsert_uma_skill_characters", parameters);
    }

    public static async Task<List<UmaSkillSearchResult>> SearchUmaSkill(
        string query, int limit = 5, float threshold = 0.4f)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_query", NpgsqlDbType.Text) { Value = query },
            new("@p_limit", NpgsqlDbType.Integer) { Value = limit },
            new("@p_threshold", NpgsqlDbType.Real) { Value = threshold }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("search_uma_skill", connection, parameters);

        var results = new List<UmaSkillSearchResult>();
        while (await reader.ReadAsync())
            results.Add(new UmaSkillSearchResult(
                (int)reader["skill_id"],
                reader["parent_skill_id"] as int?,
                reader["name_en"].ToString()!,
                (int)reader["rarity"],
                (bool)reader["available_in_en"],
                (float)reader["match_similarity"]
            ));
        return results;
    }

    public static async Task<UmaSkillFullResult?> GetUmaSkillFull(int skillId)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_uma_skill_full", connection, parameters);

        if (!await reader.ReadAsync()) return null;

        return new UmaSkillFullResult(
            (int)reader["skill_id"],
            reader["name_en"].ToString()!,
            (int)reader["rarity"],
            (bool)reader["available_in_en"],
            reader["base_time_ms"] as int?,
            reader["last_llm_update"] as DateTime?,
            reader["gene_skill_id"] as int?,
            reader["gene_name_en"] as string,
            reader["gene_base_time_ms"] as int?,
            reader["character_names"] as string
        );
    }

    public static async Task<List<UmaSkillGroupResult>> GetUmaSkillGroups(int skillId)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_uma_skill_groups", connection, parameters);

        var results = new List<UmaSkillGroupResult>();
        while (await reader.ReadAsync())
            results.Add(new UmaSkillGroupResult(
                (int)reader["group_index"],
                reader["readable_precondition"] as string,
                reader["readable_condition"] as string,
                reader["readable_effects"] as string
            ));
        return results;
    }

    public static async Task<List<(int SkillId, string ConditionGroupsJson)>> GetUmaSkillsMissingDescriptions()
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_uma_skills_missing_descriptions", connection);

        var results = new List<(int, string)>();
        while (await reader.ReadAsync())
            results.Add(((int)reader["skill_id"], reader["condition_groups_json"].ToString()!));
        return results;
    }

    public static async Task<int?> GetUmaSkillParentId(int skillId)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_uma_skill_parent_id", connection, parameters);

        if (!await reader.ReadAsync()) return null;
        return reader["parent_skill_id"] as int?;
    }

    // ---- Skill readable groups ----

    public static async Task UpsertUmaSkillReadableGroup(
        int skillId, int groupIndex, string? precondition, string? condition, string? effects)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId },
            new("@p_group_index", NpgsqlDbType.Integer) { Value = groupIndex },
            new("@p_readable_precondition", NpgsqlDbType.Text) { Value = (object?)precondition ?? DBNull.Value },
            new("@p_readable_condition", NpgsqlDbType.Text) { Value = (object?)condition ?? DBNull.Value },
            new("@p_readable_effects", NpgsqlDbType.Text) { Value = (object?)effects ?? DBNull.Value }
        };
        await ExecuteVoidProcedure("upsert_uma_skill_readable_group", parameters);
    }

    public static async Task DeleteUmaSkillReadableGroups(int skillId)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId }
        };
        await ExecuteVoidProcedure("delete_uma_skill_readable_groups", parameters);
    }

    public static async Task UpdateSkillConditionsHash(int skillId)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_skill_id", NpgsqlDbType.Integer) { Value = skillId }
        };
        await ExecuteVoidProcedure("update_skill_conditions_hash", parameters);
    }

    // ---- Condition types ----

    public static async Task ReseedUmaConditionTypes(ConditionTypeRecord[] entries)
    {
        await ExecuteVoidProcedure("truncate_uma_condition_types");

        foreach (ConditionTypeRecord e in entries)
        {
            NpgsqlParameter[] parameters =
            {
                new("@p_name", NpgsqlDbType.Varchar) { Value = e.Name },
                new("@p_description", NpgsqlDbType.Text) { Value = e.Description },
                new("@p_example", NpgsqlDbType.Varchar) { Value = (object?)e.Example ?? DBNull.Value },
                new("@p_example_meaning", NpgsqlDbType.Text) { Value = (object?)e.ExampleMeaning ?? DBNull.Value }
            };
            await ExecuteVoidProcedure("insert_uma_condition_type", parameters);
        }
    }

    public static async Task<List<(string name, string desc, string? example, string? meaning)>>
        GetUmaConditionDescriptions(string[] conditionNames)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_names", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = conditionNames }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_condition_descriptions", connection, parameters);

        var results = new List<(string, string, string?, string?)>();
        while (await reader.ReadAsync())
            results.Add((
                reader["condition_name"].ToString()!,
                reader["description"].ToString()!,
                reader["example"] as string,
                reader["example_meaning"] as string
            ));
        return results;
    }

    // ---- Effect types ----

    public static async Task<Dictionary<int, string>> GetUmaEffectTypeNames(int[] typeIds)
    {
        await using NpgsqlConnection connection = new(CONNECTION_STRING);

        NpgsqlParameter[] parameters =
        {
            new("@p_type_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = typeIds }
        };

        await using NpgsqlDataReader reader =
            await ExecuteReturnQueryFunction("get_uma_effect_type_names", connection, parameters);

        var map = new Dictionary<int, string>();
        while (await reader.ReadAsync())
            map[(int)reader["effect_type_id"]] = reader["display_name"].ToString()!;

        return map;
    }

    // ---- Cards ----

    public static async Task UpsertUmaCard(UmaCardRecord r)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_card_id", NpgsqlDbType.Integer) { Value = r.CardId },
            new("@p_char_id", NpgsqlDbType.Integer) { Value = r.CharId },
            new("@p_name_en", NpgsqlDbType.Varchar) { Value = r.NameEn },
            new("@p_version", NpgsqlDbType.Varchar) { Value = (object?)r.Version ?? DBNull.Value },
            new("@p_title_en_gl", NpgsqlDbType.Varchar) { Value = (object?)r.TitleEnGl ?? DBNull.Value }
        };
        await ExecuteVoidProcedure("upsert_uma_card", parameters);
    }

    // ---- Characters ----

    public static async Task UpsertUmaCharacter(UmaCharacterRecord r)
    {
        NpgsqlParameter[] parameters =
        {
            new("@p_char_id", NpgsqlDbType.Integer) { Value = r.CharId },
            new("@p_name_en", NpgsqlDbType.Varchar) { Value = r.NameEn },
            new("@p_name_jp", NpgsqlDbType.Varchar) { Value = (object?)r.NameJp ?? DBNull.Value },
            new("@p_name_tw", NpgsqlDbType.Varchar) { Value = (object?)r.NameTw ?? DBNull.Value },
            new("@p_url_name", NpgsqlDbType.Varchar) { Value = (object?)r.UrlName ?? DBNull.Value },
            new("@p_height", NpgsqlDbType.Integer) { Value = (object?)r.Height ?? DBNull.Value },
            new("@p_birth_day", NpgsqlDbType.Integer) { Value = (object?)r.BirthDay ?? DBNull.Value },
            new("@p_birth_month", NpgsqlDbType.Integer) { Value = (object?)r.BirthMonth ?? DBNull.Value },
            new("@p_birth_year", NpgsqlDbType.Integer) { Value = (object?)r.BirthYear ?? DBNull.Value },
            new("@p_sex", NpgsqlDbType.Integer) { Value = (object?)r.Sex ?? DBNull.Value },
            new("@p_bust", NpgsqlDbType.Integer) { Value = (object?)r.Bust ?? DBNull.Value },
            new("@p_waist", NpgsqlDbType.Integer) { Value = (object?)r.Waist ?? DBNull.Value },
            new("@p_hip", NpgsqlDbType.Integer) { Value = (object?)r.Hip ?? DBNull.Value },
            new("@p_va_en", NpgsqlDbType.Varchar) { Value = (object?)r.VaEn ?? DBNull.Value },
            new("@p_va_ja", NpgsqlDbType.Varchar) { Value = (object?)r.VaJa ?? DBNull.Value },
            new("@p_va_link", NpgsqlDbType.Varchar) { Value = (object?)r.VaLink ?? DBNull.Value },
            new("@p_playable", NpgsqlDbType.Boolean) { Value = (object?)r.Playable ?? DBNull.Value },
            new("@p_playable_en", NpgsqlDbType.Boolean) { Value = (object?)r.PlayableEn ?? DBNull.Value },
            new("@p_active_en", NpgsqlDbType.Boolean) { Value = r.ActiveEn },
            new("@p_rl_active", NpgsqlDbType.Varchar) { Value = (object?)r.RlActive ?? DBNull.Value },
            new("@p_rl_country", NpgsqlDbType.Varchar) { Value = (object?)r.RlCountry ?? DBNull.Value },
            new("@p_rl_death", NpgsqlDbType.Varchar) { Value = (object?)r.RlDeath ?? DBNull.Value },
            new("@p_rl_earnings", NpgsqlDbType.Bigint) { Value = (object?)r.RlEarnings ?? DBNull.Value },
            new("@p_rl_earnings_other", NpgsqlDbType.Jsonb) { Value = (object?)r.RlEarningsOther ?? DBNull.Value },
            new("@p_rl_races", NpgsqlDbType.Integer) { Value = (object?)r.RlRaces ?? DBNull.Value },
            new("@p_rl_record", NpgsqlDbType.Varchar) { Value = (object?)r.RlRecord ?? DBNull.Value },
            new("@p_rl_wins", NpgsqlDbType.Integer) { Value = (object?)r.RlWins ?? DBNull.Value }
        };
        await ExecuteVoidProcedure("upsert_uma_character", parameters);
    }

    #region helper functions

    private static async Task<NpgsqlDataReader> ExecuteReturnQueryFunction(
        string function_name, NpgsqlConnection conn, NpgsqlParameter[]? parameters = null)
    {
        await conn.OpenAsync();

        string paramList = parameters is { Length: > 0 }
            ? string.Join(", ", parameters.Select(p => p.ParameterName))
            : string.Empty;

        NpgsqlCommand cmd = new($"SELECT * FROM {function_name}({paramList})", conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 300
        };

        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        return await cmd.ExecuteReaderAsync();
    }

    private static async Task ExecuteVoidProcedure(
        string procedure_name, NpgsqlParameter[]? parameters = null)
    {
        await using NpgsqlConnection conn = new(CONNECTION_STRING);
        await conn.OpenAsync();

        string paramList = parameters is { Length: > 0 }
            ? string.Join(", ", parameters.Select(p => p.ParameterName))
            : string.Empty;

        NpgsqlCommand cmd = new($"CALL {procedure_name}({paramList})", conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 300
        };

        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        await cmd.ExecuteNonQueryAsync();
    }

    private static string NoRowsError(string function)
    {
        return $"The \"{function}\" function returned no rows. Have you populated the database?";
    }

    #endregion
}