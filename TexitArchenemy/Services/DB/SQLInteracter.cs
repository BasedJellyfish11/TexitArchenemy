using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Discord.WebSocket;
using TexitArchenemy.Services.Twitter;

// It appears Connections in .NET are handled the opposite of how I was taught in uni: You open a new one every query, preferably inside a using, and .NET is so smart that it pools it
// That's what I get from here anyway https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-connection-pooling and here https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection?view=dotnet-plat-ext-5.0#remarks

namespace TexitArchenemy.Services.DB
{
    public static class SQLInteracter
    {
        private const string CONNECTION_STRING = @"Data Source=.\SQLEXPRESS;Database=TEXIT_ARCHENEMY;Integrated Security=True;";

        public delegate Task OnAddTwitterRuleHandler(TwitterRule addedRule);
        public static OnAddTwitterRuleHandler? OnAddTwitterRule;
        
        
        public static async Task<string> GetDiscordToken()
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            await using SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.get_discord_creds, connection);
            if(!reader.HasRows)
                throw new InvalidOperationException(NoRowsError(ProcedureNames.get_discord_creds));

            reader.Read();
            return reader[DiscordAuthColumns.token].ToString()!;
        }
        
        public static async Task<TwitterAuth> GetTwitterToken()
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            await using SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.get_twitter_creds, connection);
            
            if(!reader.HasRows)
                throw new InvalidOperationException(NoRowsError(ProcedureNames.get_twitter_creds));

            reader.Read();
            return new TwitterAuth
            {
                apiKey = reader[TwitterAuthColumns.api_key].ToString()!, 
                apiSecret = reader[TwitterAuthColumns.api_secret].ToString()!, 
                apiToken = reader[TwitterAuthColumns.api_token].ToString()!
            };
        }

        public static async Task<TwitterRule?> AddTwitterRule(string rule_value, SocketGuildChannel channel)
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            
            SqlParameter[] parameters =
            {
                new($"@{AddTwitterRuleParams.channel_id}", SqlDbType.VarChar),
                new($"@{AddTwitterRuleParams.guild_id}", SqlDbType.VarChar),
                new($"@{AddTwitterRuleParams.rule_value}", SqlDbType.VarChar),

            };
            parameters[0].Value = channel.Id.ToString();
            parameters[1].Value = channel.Guild.ToString();
            parameters[2].Value = rule_value;

            SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.add_twitter_rule,connection, parameters);
            
            reader.Read();
            if ((int) reader[TwitterRulesColumns.tag] != 1)
                return null;
            
            await (OnAddTwitterRule?.Invoke( new TwitterRule {tag = (int) reader[TwitterRulesColumns.tag], value = rule_value}) ?? Task.CompletedTask);

            return new TwitterRule {tag = (int) reader[TwitterRulesColumns.tag], value = rule_value};

        }

        public static async Task<(ulong messageId, ulong channelId)?> CheckRepost(SocketMessage message, string linkId, LinkTypes linkType )
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            SqlParameter[] parameters =
            {
                new($"@{CheckRepostParams.channel_id}", SqlDbType.VarChar),
                new($"@{CheckRepostParams.message_id}", SqlDbType.VarChar),
                new($"@{CheckRepostParams.link_id}", SqlDbType.VarChar),
                new($"@{CheckRepostParams.link_type_description}", SqlDbType.VarChar),
            };
            
            parameters[0].Value = message.Channel.Id.ToString();
            parameters[1].Value = message.Id.ToString();
            parameters[2].Value = linkId;
            parameters[3].Value = linkType.ToString();
            
            await using SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.check_repost, connection);
            
            if(!reader.HasRows)
                throw new InvalidOperationException(NoRowsError(ProcedureNames.check_repost));

            reader.Read();

            ValueTuple<string, string> stringTuple = ((string) reader[RepostRepositoryColumns.message_id], (string) reader[RepostRepositoryColumns.channel_id]);

            if (stringTuple.Item1 == "-1" || stringTuple.Item2 == "-1")
                return null;
            return (ulong.Parse(stringTuple.Item1), ulong.Parse(stringTuple.Item2));

        }
        
        public static async Task<bool> DeleteTwitterRule(int tag, SocketGuildChannel channel)
        {
            
            SqlParameter[] parameters =
            {
                new($"@{DeleteTwitterRuleParams.tag}", SqlDbType.Int),
                new($"@{DeleteTwitterRuleParams.channel_id}", SqlDbType.VarChar)

            };
            parameters[0].Value = tag;
            parameters[1].Value = channel.Id.ToString();
            
            return await ExecuteReturnValueProcedure(ProcedureNames.delete_twitter_rule, parameters) != -1;

        }
        
        public static async Task<HashSet<ulong>> GetTwitterRuleChannels(int tag)
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            SqlParameter[] parameters =
            {
                new($"@{GetTwitterRuleChannelsParams.tag}", SqlDbType.Int),

            };
            parameters[0].Value = tag;
            await using SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.get_twitter_rule_channels, connection, parameters);
            
            if(!reader.HasRows)
                throw new InvalidOperationException(NoRowsError(ProcedureNames.get_twitter_rule_channels));

            HashSet<ulong> channelsForRule = new();
            while (reader.Read())
            {
                channelsForRule.Add(ulong.Parse((string) reader[RuleChannelRelationColumns.channel_id]));
            }

            return channelsForRule;
        }
        
        public static async Task<HashSet<TwitterRule>> GetTwitterRules()
        {
            await using SqlConnection connection = new(CONNECTION_STRING);
            await using SqlDataReader reader = await ExecuteReturnQueryProcedure(ProcedureNames.get_twitter_rules, connection);
            if(!reader.HasRows)
                throw new InvalidOperationException(NoRowsError(ProcedureNames.get_twitter_rules));

            HashSet<TwitterRule> toReturn = new();
            while (reader.Read())
            {
                TwitterRule rule = new TwitterRule
                {
                    tag = (int) reader[TwitterRulesColumns.tag],
                    value = (string) reader[TwitterRulesColumns.rule_value]
                };
                toReturn.Add(rule);
            }

            return toReturn;
        }
        
        
        
        #region helper functions
        private static async Task<SqlDataReader> ExecuteReturnQueryProcedure(string procedure_name, SqlConnection conn, SqlParameter[]? parameters = null)
        {
            await conn.OpenAsync();
            SqlCommand sqlComm = new(procedure_name, conn) {CommandType = CommandType.StoredProcedure};
            if (parameters != null)
                sqlComm.Parameters.AddRange(parameters);
            
            SqlDataReader reader = await sqlComm.ExecuteReaderAsync();
            return reader;
        }


        private static async Task<int> ExecuteReturnValueProcedure(string procedure_name, SqlParameter[]? parameters = null)
        {
            await using SqlConnection conn = new(CONNECTION_STRING);
            await conn.OpenAsync();
            SqlCommand sqlComm = new(procedure_name, conn) {CommandType = CommandType.StoredProcedure};
            if (parameters != null)
                sqlComm.Parameters.AddRange(parameters);
            await sqlComm.ExecuteNonQueryAsync();
            return (int) sqlComm.Parameters["@RETURN_VALUE"].Value;
        }

        private static string NoRowsError(string procedure)
        {
            return $"The \"{procedure}\" stored procedure returned no rows. Have you populated the database?";
        }

        #endregion
    }
}