namespace TexitArchenemy.Services.Database;

public static class ProcedureNames
{
    public const string check_repost                         = nameof(check_repost);
    public const string get_discord_creds                    = nameof(get_discord_creds);
    public const string get_box_warmup                       = nameof(get_box_warmup);
    public const string update_box_challenge_progress        = nameof(update_box_challenge_progress);
    public const string get_box_challenge_progress           = nameof(get_box_challenge_progress);
    public const string is_repost_channel                    = nameof(is_repost_channel);
    public const string mark_as_repost_channel               = nameof(mark_as_repost_channel);
    public const string get_manifest_hash                    = nameof(get_manifest_hash);
    public const string get_uma_effect_type_names            = nameof(get_uma_effect_type_names);
    public const string get_condition_descriptions           = nameof(get_condition_descriptions);
    public const string search_uma_skill                     = nameof(search_uma_skill);
    public const string get_uma_skill_full                   = nameof(get_uma_skill_full);
    public const string get_uma_skill_groups                 = nameof(get_uma_skill_groups);
    public const string upsert_manifest_hash                 = nameof(upsert_manifest_hash);
    public const string upsert_uma_skill                     = nameof(upsert_uma_skill);
    public const string upsert_uma_card                      = nameof(upsert_uma_card);
    public const string upsert_uma_character                 = nameof(upsert_uma_character);
    public const string upsert_uma_skill_characters          = nameof(upsert_uma_skill_characters);
    public const string upsert_uma_skill_readable_group      = nameof(upsert_uma_skill_readable_group);
    public const string delete_uma_skill_readable_groups     = nameof(delete_uma_skill_readable_groups);
    public const string update_skill_conditions_hash         = nameof(update_skill_conditions_hash);
    public const string truncate_uma_condition_types         = nameof(truncate_uma_condition_types);
    public const string insert_uma_condition_type            = nameof(insert_uma_condition_type);
    public const string get_uma_skill_parent_id              = nameof(get_uma_skill_parent_id);
    public const string get_uma_skills_missing_descriptions  = nameof(get_uma_skills_missing_descriptions);
}

#region TableColumns

public static class DiscordAuthColumns
{
    public const string token = nameof(token);
}

public static class BoxWarmupColumns
{
    public const string warmup = nameof(warmup);
    public const string level  = nameof(level);
}

public static class BoxChallengeColumns
{
    public const string user_id    = nameof(user_id);
    public const string boxes_drawn = nameof(boxes_drawn);
}

public static class DiscordChannelsColumns
{
    public const string channel_id      = nameof(channel_id);
    public const string guild_id        = nameof(guild_id);
    public const string channel_type_id = nameof(channel_type_id);
    public const string repost_check    = nameof(repost_check);
    public const string pixiv_expand    = nameof(pixiv_expand);
}

public static class RuleChannelRelationColumns
{
    public const string tag        = nameof(tag);
    public const string channel_id = nameof(channel_id);
}

public static class RepostRepositoryColumns
{
    public const string message_id    = nameof(message_id);
    public const string channel_id    = nameof(channel_id);
    public const string link_id       = nameof(link_id);
    public const string link_type_id  = nameof(link_type_id);
}

public enum LinkTypes
{
    Twitter,
    Pixiv,
    Artstation
}

#endregion

#region ProcedureParameters

public static class CheckRepostParams
{
    public const string channel_id           = nameof(channel_id);
    public const string message_id           = nameof(message_id);
    public const string link_id              = nameof(link_id);
    public const string link_type_description = nameof(link_type_description);
}

public static class GetDiscordCredsParams { }

public static class GetBoxWarmupParams
{
    public const string lesson = nameof(lesson);
}

public static class UpdateBoxChallengeProgressParams
{
    public const string boxes_drawn = nameof(boxes_drawn);
    public const string user_id     = nameof(user_id);
}

public static class GetBoxChallengeProgressParams
{
    public const string user_id = nameof(user_id);
}

public static class IsRepostChannelParams
{
    public const string channel_id = nameof(channel_id);
}

public static class MarkAsRepostChannelParams
{
    public const string channel_id = nameof(channel_id);
    public const string guild_id   = nameof(guild_id);
}

#endregion