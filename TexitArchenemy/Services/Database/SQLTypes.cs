using System;
using System.Collections.Generic;

namespace TexitArchenemy.Services.Database;

public record UmaSkillRecord(
    int     SkillId,
    int?    ParentSkillId,
    string  NameEn,
    string  NameJp,
    int     Rarity,
    int?    IconId,
    bool    AvailableInEn,
    string  ConditionGroupsJson,
    int?    BaseTimeMs
);

public record UmaCharacterRecord(
    int     CharId,
    string  NameEn,
    string? NameJp,
    string? NameTw,
    string? UrlName,
    int?    Height,
    int?    BirthDay,
    int?    BirthMonth,
    int?    BirthYear,
    int?    Sex,
    int?    Bust,
    int?    Waist,
    int?    Hip,
    string? VaEn,
    string? VaJa,
    string? VaLink,
    bool?   Playable,
    bool?   PlayableEn,
    bool    ActiveEn,
    string? RlActive,
    string? RlCountry,
    string? RlDeath,
    long?   RlEarnings,
    string? RlEarningsOther,
    int?    RlRaces,
    string? RlRecord,
    int?    RlWins);

public record UmaCardRecord(
    int     CardId,
    int     CharId,
    string  NameEn,
    string? Version,
    string? TitleEnGl
);

public record UmaSkillSearchResult(
    int     SkillId,
    int?    ParentSkillId,
    string  NameEn,
    int     Rarity,
    bool    AvailableInEn,
    float   MatchSimilarity);

public record UmaSkillFullResult(
    int         SkillId,
    string      NameEn,
    int         Rarity,
    bool        AvailableInEn,
    int?        BaseTimeMs,
    DateTime?   LastLlmUpdate,
    int?        GeneSkillId,
    string?     GeneNameEn,
    int?        GeneBaseTimeMs,
    string?     CharacterNames);

public record UmaSkillGroupResult(
    int     GroupIndex,
    string? ReadablePrecondition,
    string? ReadableCondition,
    string? ReadableEffects);

public record ConditionTypeRecord(
    string  Name,
    string  Description,
    string? Example,
    string? ExampleMeaning);

public record ConditionTypeEntry(
    string Name, string Desc, string Example, string ExampleMeaning);

public record UminekoProgressRecord(
    ulong   UserId,
    ulong   GuildId,
    ulong   ChannelId,
    ulong?  RoleId,
    int?    QuoteIndex,
    short?  QuoteEpisode,
    string? QuotePlaintext);