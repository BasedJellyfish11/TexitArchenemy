# Umineko progress tracker

## Goal

Discord feature: gate a reading-group's spoiler channels by how far each
member has read in the visual novel *Umineko no Naku Koro ni*. Members post a
screenshot of their in-game progress (or paste a quote manually); OCR + local
fuzzy quote matching turn that into a position in the script; a role-sync
grants/revokes access to each other's channels based on relative progress, so
nobody gets spoiled past where they've actually read.

Access rule: user A gets user B's channel-access role iff
`A.quote_index >= B.quote_index` (ties included, per guild). Progress is
tracked as a single global `quote_index` (comparable across episodes — no
composite episode+index sort needed) plus `quote_plaintext` for display.
There is no `quote_episode` column — the quote dump this was seeded from
didn't capture which episode each line belongs to, so episode display was
dropped rather than left half-populated.

## Flow

1. `!uminekoregister` (`Commands/UminekoRegister.cs`) — run in the channel to
   gate. Creates a `umineko-<username>` role, grants it `ViewChannel` on that
   channel, assigns it to the caller, and inserts a `user_umineko_progress`
   row with `quote_index = NULL`. `UminekoRoleSync` treats a `NULL` index as
   0 (the lowest possible position), so everyone else gets access to the new
   channel right away, while the new member gets access to no one else's
   until they actually post a screenshot.
2. Member posts a screenshot in their registered channel.
   `CommandHandler.CheckUminekoProgressImage` (called from `CheckNonCommand`
   on every message) checks the message has image attachments and that the
   author is registered *and posting in their own registered channel*, then:
   - OCRs each attachment via `Ocr/TesseractOcrService`.
   - Looks up the OCR text via `SQLInteracter.SearchUminekoQuote`, a local
     Postgres fuzzy match (`search_umineko_quote`, pg_trgm) against
     `umineko_quotes_cache`, restricted to `quote_index > progress.QuoteIndex`
     (or no floor if the user has never matched yet). This replaced an earlier
     external REST API (`quotes.auaurora.moe`) that wanted exact-ish matches
     and handled noisy OCR text poorly.
   - Writes the match via `SQLInteracter.UpdateUminekoProgress`.
   - Calls `UminekoRoleSync.SyncRoles(guild)` to recompute every pairwise
     grant/revoke in that guild.
   The match-and-apply half of this (search → write progress → sync roles →
   reply embed) is `CommandHandler.ApplyUminekoQuoteMatch`, shared with
   `!uminekoupdate` below.
3. `!uminekoupdate <text>` (`Commands/UminekoUpdate.cs`) — manual fallback for
   when OCR misreads a screenshot: paste the line yourself and it runs through
   the same `ApplyUminekoQuoteMatch` path (same `quote_index >
   current index` floor, so it can't be used to fabricate backward progress,
   but a wrong-but-later-index paste can still misplace someone — no
   confirmation step).
4. `!uminekohelp` (`Commands/UminekoHelp.cs`) — static embed listing these
   commands. Hardcoded rather than reflected off `CommandService`/`[Summary]`
   at runtime (no DI wiring exists for modules to reach `CommandService`);
   keep it in sync by hand when commands change.
5. `!uminekounregister` (`Commands/UminekoUnregister.cs`) reverts the channel
   to public (removes the `@everyone` deny overwrite added at register),
   deletes the role, and deletes the progress row.

## Key files

- `UminekoRoleSync.cs` — the pairwise access algorithm. O(n²) over registered
  users per sync; fine at reading-group scale. Fetches each user via REST
  (`DiscordSocketRestClient.GetGuildUserAsync`), not the gateway cache — the
  cache doesn't see the bot's own role changes (no `GuildMembers` intent),
  which was causing missed revokes.
- `Commands/UminekoUpdate.cs`, `Commands/UminekoHelp.cs` — the manual-entry
  and command-list commands described above.
- `../Ocr/TesseractOcrService.cs` — on-device OCR (chosen over a cloud OCR
  API). Linux native-lib loading needed a manual self-healing symlink shim
  (`TesseractNativeLibrarySetup.cs`) because the NuGet package only ships
  Windows binary names.
- `../Database/SQLInteracter.cs` / `SQLStrings.cs` — DB plumbing for
  `user_umineko_progress` (composite PK `user_id, guild_id` — a Discord user
  ID is global, so this is required for the same person to be in reading
  groups on multiple servers).

## Schema notes (`DatabaseCreation_PostgreSQL.sql`)

- `user_umineko_progress` stores current position only (one row per
  user+guild), not a history log.
- Deliberately **not** FK'd to `discord_channels` — that table belongs to the
  repost-checker feature and has a cleanup trigger that prunes untagged
  channels; coupling Umineko's channel to it risked losing the row.
- `umineko_quotes_cache` + `search_umineko_quote()` (pg_trgm fuzzy match) are
  the live lookup path — the table needs to be populated with the full
  numbered script (a one-off data load, done outside this repo) before
  matches will work.

## Not built yet

- A permission check on `!uminekoregister` (currently anyone can create a
  channel-gating role for themselves).
- The debug "OCR debug" embed that echoes raw OCR text on every screenshot in
  `CommandHandler.cs` is intentional scaffolding for tuning match quality —
  remove once the API match rate is trusted.
