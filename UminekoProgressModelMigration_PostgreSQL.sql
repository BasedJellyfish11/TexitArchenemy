-- ============================================================
-- Umineko progress-model migration
-- ============================================================
--
-- Prod currently has the OLD Umineko design (per-channel shared progress,
-- gated by discord_channels.umineko_channel) from
-- "Sync schema file with the Umineko shared-channel tables/functions
-- deployed on prod" (upstream 046a040). This branch replaces that with a
-- per-user, role-gated design instead.
--
-- The only object both designs share is umineko_quotes_cache (identical
-- shape in both) — it is left untouched. Everything else Umineko-related
-- is dropped and recreated to match DatabaseCreation_PostgreSQL.sql in this
-- branch.
--
-- This is NOT part of DatabaseCreation_PostgreSQL.sql (that file is for a
-- from-scratch DB, which never had the old design) — run this once, by
-- hand, against the live database.
--
-- Run as the same role that owns these objects, e.g.:
--   psql -d texit_archenemy -f UminekoProgressModelMigration_PostgreSQL.sql

BEGIN;

-- ---- Drop old design ----

DROP TRIGGER IF EXISTS umineko_cleanup ON discord_channels;
DROP FUNCTION IF EXISTS fn_umineko_cleanup();

DROP PROCEDURE IF EXISTS mark_umineko_channel(VARCHAR(20), VARCHAR(20));
DROP PROCEDURE IF EXISTS unmark_umineko_channel(VARCHAR(20));
DROP PROCEDURE IF EXISTS upsert_umineko_progress(VARCHAR(20), VARCHAR(20), INT);
DROP PROCEDURE IF EXISTS delete_umineko_progress(VARCHAR(20), VARCHAR(20));
-- One-off loader for umineko_quotes_cache. Not referenced by the app in either
-- design; dropped along with the rest of the old design since it's redundant
-- with the migration/table both branches already share.
DROP PROCEDURE IF EXISTS bulk_upsert_umineko_quotes(INT[], TEXT[]);

DROP FUNCTION IF EXISTS is_umineko_channel(VARCHAR(20));
DROP FUNCTION IF EXISTS get_umineko_channel_progress(VARCHAR(20));
DROP FUNCTION IF EXISTS get_umineko_progress(VARCHAR(20), VARCHAR(20));
DROP FUNCTION IF EXISTS get_umineko_quote_cache_count();
DROP FUNCTION IF EXISTS search_umineko_quote(TEXT, INT, REAL);

DROP TABLE IF EXISTS umineko_progress;

-- remove_useless_channels() referenced umineko_channel — restore it to the
-- pre-shared-channel form now that the column is going away.
CREATE OR REPLACE PROCEDURE remove_useless_channels()
LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM discord_channels
    WHERE pixiv_expand  = FALSE
      AND repost_check  = FALSE
      AND channel_id NOT IN (SELECT rcr.channel_id FROM rule_channel_relation rcr);
END;
$$;

ALTER TABLE discord_channels DROP COLUMN IF EXISTS umineko_channel;

-- ---- Create new design ----

CREATE TABLE IF NOT EXISTS user_umineko_progress
(
    user_id        VARCHAR(20) NOT NULL,
    guild_id       VARCHAR(20) NOT NULL,
    channel_id     VARCHAR(20) NOT NULL,
    role_id        VARCHAR(20),
    quote_index    INT,
    quote_plaintext TEXT,
    PRIMARY KEY (user_id, guild_id)
);

CREATE OR REPLACE FUNCTION get_umineko_progress(p_user_id VARCHAR(20), p_guild_id VARCHAR(20))
RETURNS TABLE(
    user_id         VARCHAR(20),
    guild_id        VARCHAR(20),
    channel_id      VARCHAR(20),
    role_id         VARCHAR(20),
    quote_index     INT,
    quote_plaintext TEXT
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
        SELECT u.user_id, u.guild_id, u.channel_id, u.role_id, u.quote_index, u.quote_plaintext
        FROM user_umineko_progress u
        WHERE u.user_id = p_user_id AND u.guild_id = p_guild_id;
END;
$$;

CREATE OR REPLACE FUNCTION get_all_umineko_progress(p_guild_id VARCHAR(20))
RETURNS TABLE(
    user_id         VARCHAR(20),
    guild_id        VARCHAR(20),
    channel_id      VARCHAR(20),
    role_id         VARCHAR(20),
    quote_index     INT,
    quote_plaintext TEXT
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
        SELECT u.user_id, u.guild_id, u.channel_id, u.role_id, u.quote_index, u.quote_plaintext
        FROM user_umineko_progress u
        WHERE u.guild_id = p_guild_id;
END;
$$;

CREATE OR REPLACE PROCEDURE register_umineko_user(
    p_user_id    VARCHAR(20),
    p_guild_id   VARCHAR(20),
    p_channel_id VARCHAR(20),
    p_role_id    VARCHAR(20)
)
LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO user_umineko_progress (user_id, guild_id, channel_id, role_id)
    VALUES (p_user_id, p_guild_id, p_channel_id, p_role_id);
END;
$$;

CREATE OR REPLACE PROCEDURE unregister_umineko_user(p_user_id VARCHAR(20), p_guild_id VARCHAR(20))
LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM user_umineko_progress
    WHERE user_id = p_user_id AND guild_id = p_guild_id;
END;
$$;

CREATE OR REPLACE PROCEDURE update_umineko_progress(
    p_user_id         VARCHAR(20),
    p_guild_id        VARCHAR(20),
    p_quote_index     INT,
    p_quote_plaintext TEXT
)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE user_umineko_progress
    SET quote_index = p_quote_index,
        quote_plaintext = p_quote_plaintext
    WHERE user_id = p_user_id AND guild_id = p_guild_id;
END;
$$;

-- Fuzzy-matches noisy OCR text against the cached script, restricted to quotes
-- past the reader's current position (p_min_index, nullable for a first-ever
-- match) so OCR noise can't walk someone's progress backwards. On an exact tie
-- in similarity (e.g. a short, repeated line), the later occurrence is
-- preferred — readers move forward, so it's the more likely real position.
CREATE OR REPLACE FUNCTION search_umineko_quote(p_query TEXT, p_min_index INT, p_threshold REAL)
RETURNS TABLE(quote_index INT, quote_text TEXT, match_similarity REAL)
LANGUAGE plpgsql AS $$
BEGIN
RETURN QUERY
SELECT
    q.quote_index,
    q.quote_text,
    similarity(q.quote_text, p_query) AS sim
FROM umineko_quotes_cache q
WHERE similarity(q.quote_text, p_query) >= p_threshold
  AND (p_min_index IS NULL OR q.quote_index > p_min_index)
ORDER BY sim DESC, q.quote_index DESC
LIMIT 1;
END;
$$;

CREATE OR REPLACE FUNCTION get_umineko_quote_count()
RETURNS TABLE(quote_count INT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY SELECT COUNT(*)::INT FROM umineko_quotes_cache;
END;
$$;

COMMIT;
