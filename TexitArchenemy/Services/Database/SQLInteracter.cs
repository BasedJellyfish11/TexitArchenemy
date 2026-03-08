using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;

// Open a new connection per query inside a using block, let Npgsql's built-in connection pool handle the lifetime.
// Npgsql pooling docs: https://www.npgsql.org/doc/connection-string-parameters.html#pooling

namespace TexitArchenemy.Services.Database;

public static class SQLInteracter
{
    private static readonly string CONNECTION_STRING =
        $"Host=localhost;Database=texit_archenemy;" +
        $"Username={Environment.GetEnvironmentVariable("PG_USER")};" +
        $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")};";

    private const int SQL_EXCEPTION = -999;


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
            new($"@{CheckRepostParams.channel_id}",          NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.message_id}",          NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.link_id}",             NpgsqlDbType.Varchar),
            new($"@{CheckRepostParams.link_type_description}",NpgsqlDbType.Varchar),
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

        var stringTuple = ((string)reader[RepostRepositoryColumns.message_id],
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
            new($"@{UpdateBoxChallengeProgressParams.user_id}",    NpgsqlDbType.Varchar),
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
            new($"@{UpdateBoxChallengeProgressParams.user_id}", NpgsqlDbType.Varchar),
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
            new($"@{IsRepostChannelParams.channel_id}", NpgsqlDbType.Varchar),
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
            new($"@{MarkAsRepostChannelParams.guild_id}",   NpgsqlDbType.Varchar)
        };
        parameters[0].Value = contextChannel.Id.ToString();
        parameters[1].Value = contextChannel.Guild.Id.ToString();

        await ExecuteVoidProcedure(ProcedureNames.mark_as_repost_channel, parameters);
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
            CommandType    = CommandType.Text,
            CommandTimeout = 300
        };

        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        return await cmd.ExecuteReaderAsync();
    }
    
    
    private static async Task<int> ExecuteVoidProcedure(
        string procedure_name, NpgsqlParameter[]? parameters = null)
    {
        await using NpgsqlConnection conn = new(CONNECTION_STRING);
        await conn.OpenAsync();

        string paramList = parameters is { Length: > 0 }
            ? string.Join(", ", parameters.Select(p => p.ParameterName))
            : string.Empty;

        NpgsqlCommand cmd = new($"CALL {procedure_name}({paramList})", conn)
        {
            CommandType    = CommandType.Text,
            CommandTimeout = 300
        };

        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            return 0;
        }
        catch (NpgsqlException)
        {
            return SQL_EXCEPTION;
        }
    }

    private static string NoRowsError(string function) =>
        $"The \"{function}\" function returned no rows. Have you populated the database?";

    #endregion
}