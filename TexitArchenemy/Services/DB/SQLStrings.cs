namespace TexitArchenemy.Services.DB
{
    // Pretty much nothing in this file will follow C# naming conventions due to it matching SQL names.
    // This is also just a glorified string dictionary ngl
    public static class ProcedureNames
    {
        public const string add_twitter_rule = nameof(add_twitter_rule);
        public const string check_repost = nameof(check_repost);
        public const string delete_twitter_rule = nameof(delete_twitter_rule);
        public const string get_discord_creds = nameof(get_discord_creds);
        public const string get_twitter_creds = nameof(get_twitter_creds);
        public const string get_twitter_rules = nameof(get_twitter_rules);
        public const string get_twitter_rule_channels = nameof(get_twitter_rule_channels);
        public const string get_box_warmup = nameof(get_box_warmup);
        public const string update_box_challenge_progress = nameof(update_box_challenge_progress);
        public const string get_box_challenge_progress = nameof(get_box_challenge_progress);
    }


    #region TableColumns
    
    public static class DiscordAuthColumns
    {
        public const string token = nameof(token);
    }    
    
    public static class BoxWarmupColumns
    {
        public const string warmup = nameof(warmup);
        public const string level = nameof(level);
    }    
    
    public static class BoxChallengeColumns
    {
        public const string user_id = nameof(user_id);
        public const string boxes_drawn = nameof(boxes_drawn);
    }   
    
    public static class TwitterAuthColumns
    {
        public const string api_key = nameof(api_key);
        public const string api_secret = nameof(api_secret);
        public const string api_token = nameof(api_token);
    }
    public static class TwitterRulesColumns
    {
        public const string tag = nameof(tag);
        public const string rule_value = nameof(rule_value);
    }    
    
    public static class DiscordChannelsColumns
    {
        public const string channel_id = nameof(channel_id);
        public const string guild_id = nameof(guild_id);
        public const string channel_type_id = nameof(channel_type_id);
        public const string repost_check = nameof(repost_check);
        public const string pixiv_expand = nameof(pixiv_expand);
    }
    public static class RuleChannelRelationColumns
    {
        public const string tag = nameof(tag);
        public const string channel_id = nameof(channel_id);
    }
    
    public static class RepostRepositoryColumns
    {
        public const string message_id = nameof(message_id);
        public const string channel_id = nameof(channel_id);
        public const string link_id = nameof(link_id);
        public const string link_type_id = nameof(link_type_id);
    }

    public enum LinkTypes
    {
        Twitter,
        Pixiv,
        Artstation
    }
    
    #endregion
    // Procedure params

    #region ProcedureParameters

    public static class CheckRepostParams
    {
        public const string channel_id = nameof(channel_id);
        public const string message_id = nameof(message_id);
        public const string link_id = nameof(link_id);
        public const string link_type_description = nameof(link_type_description);
    }
    
    public static class AddTwitterRuleParams
    {
        public const string channel_id = nameof(channel_id);
        public const string guild_id = nameof(guild_id);
        public const string rule_value = nameof(rule_value);
    }
    
    public static class DeleteTwitterRuleParams
    {
        public const string tag = nameof(tag);
        public const string channel_id = nameof(channel_id);
    }
    
    public static class GetTwitterRuleParams
    {
    }
    
    public static class GetTwitterRuleChannelsParams
    {
        public const string tag = nameof(tag);
    }
    
    public static class GetTwitterCredsParams
    {
    }
    
    public static class GetDiscordCredsParams
    {
    }
    
    public static class GetBoxWarmupParams
    {
        public const string lesson = nameof(lesson);
    }
    
    public static class UpdateBoxChallengeProgressParams
    {
        public const string boxes_drawn = nameof(boxes_drawn);
        public const string user_id = nameof(user_id);
    }
    public static class GetBoxChallengeProgressParams
    {
        public const string user_id = nameof(user_id);
    }
    #endregion
    

    
}