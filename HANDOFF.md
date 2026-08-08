# D2STServer handoff

Last updated: 2026-08-08

## Current objective

D2STServer is a private Dota 2 Game Coordinator for the client build currently
targeted by this repository (ClientVersion 6783). The active product scope is
narrow and deliberate:

> practice lobby -> local listen-server match -> authoritative match result ->
> persistent history -> profile and hero statistics visible in Dota 2.

Matchmaking, public lobbies, guilds, workshop, coaching and other unrelated GC
surfaces are out of scope until this vertical is reliable with two real Dota 2
clients on Windows.

## Source and architecture

All three project lines descend from the original MIT project
`Hackerprod/-SKYNET-Steam-Emulator`:

- `src/D2ST.Steam` and the external `steam_api` repository are the Steam API shim.
- `D2STServer` is the C#/.NET redesign of the original SKYNET server.
- `new_launcher` and the legacy launcher are the launcher implementations.

The server is split into `D2ST.Core`, `D2ST.Protocol`, `D2ST.Persistence`,
`D2ST.Steam`, `D2ST.GameCoordinator` and `D2ST.Api`. The GC handlers stay
independent from EF Core; persistence is exposed through interfaces owned by
the GameCoordinator and implemented by the API host.

## Brief summary of previous work

The retained baseline already includes login and sessions, the GC handshake,
Shared Object cache subscription/update flows, account/econ/inventory data,
parties, chat, friends/presence/avatar support, rank/Elo storage and a practice
lobby flow. The lobby can create, join, leave, configure teams, launch a local
listen server and publish the server connection through the existing
4007/4508/4511/4506/7034 protocol flow. The server also contains the capture and
diagnostics plumbing used to derive handlers from real client traffic.

Before this handoff reset, lobby state was volatile, the rank store was the only
match-related persistence, and `7004 GameMatchSignOut` only applied Elo and
returned the lobby to postgame. The old handoff contained a much larger roadmap;
this file intentionally keeps only the context needed to continue the current
vertical.

## Decision and implementation status

### Phase 1 — completed

The first vertical now consumes the game server's real `CMsgGameMatchSignOut`
payload instead of discarding it.

Implemented:

- `MatchRecord` and `MatchPlayerRecord` contracts in `D2ST.Core`.
- SQLite/EF entities for `Matches`, `MatchPlayers`, `PlayerProfileStats` and
  `PlayerHeroStats`.
- Composite keys and indexes in `D2stDbContext`.
- A transaction-backed `MatchStore` in the API host.
- Match-id idempotency: repeated `7004` packets do not create another match,
  increment aggregates again or apply Elo twice within the same installation.
- Full core scoreboard capture: result, duration, date, teams, heroes, K/D/A,
  last hits, denies, GPM, XPM, gold, level, damage, healing, net worth,
  leaver status, party/lane metadata and item purchase arrays.
- Match-level result data: first-blood time, team scores, tower/barracks status,
  server metadata, surrender/flags and other fields available in the generated
  protobuf.
- Overall profile aggregates and per-hero aggregates updated in the same
  database transaction as the match and player rows.
- `CSODOTAGameAccountClient` projection now exposes persisted wins, losses,
  casual games played and leaver count at welcome time and after match close.
- Lobby completion now publishes `POSTGAME`, the post-game game state, the
  first-blood flag and Radiant/Dire match outcome through all lobby cache
  buckets.
- Ranking is calculated only from recorded Radiant/Dire participants, not from
  spectators or the player pool.
- A persistence fallback keeps the reusable GameCoordinator assembly usable
  without the API database; D2ST.Api registers the SQLite implementation.
- Existing databases receive the four new tables through the current bootstrap
  until the planned EF migration stage replaces manual schema evolution.

### Evidence for Phase 1

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings and
  0 errors.
- API startup against a temporary SQLite database: passed; EF created the new
  tables and the compatibility bootstrap completed without an exception.
- `git diff --check`: passed.
- Two-client Windows validation and a real captured `7004` replay are still
  pending; compilation and startup are not a substitute for that validation.

### Phase 2 — completed in this working tree

The persisted rows are now readable by the GC handlers that the client uses to
populate match history and teammate summaries:

- `7408 -> 7409` decodes the requested account, hero filter, page size,
  `start_at_match_id` cursor and practice/custom/event flags. It returns the
  newest local-lobby matches first and derives `start_time` from the authoritative
  end time and duration.
- `8063 -> 8064` decodes requested match ids and returns compact real match
  summaries: result, duration, mode, Radiant/Dire score and each stored player's
  account, hero, level, K/D/A, team boundary slot and items.
- `8124 -> 8125` returns players that shared a team with the requesting account,
  including common games, wins, most recent match and a deterministic
  K/D/A-difference performance average.
- `IMatchStore` and `MatchStore` now expose bounded read queries (100 rows per
  request) without coupling the GameCoordinator to EF Core.
- The generated protobuf files were not modified; all new behavior uses the
  existing generated request/response contracts.

The history reader treats rows written by the current server as practice-lobby
matches. An omitted `include_practice_matches` field includes them; an explicit
false excludes them, matching the protocol flag. Custom and event filters are
accepted for forward compatibility but have no rows until those match sources
are implemented.

### Evidence for Phase 2

- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings and
  0 errors after the read-side changes.
- API startup against a new temporary SQLite database: passed; DI resolved the
  read-capable `IMatchStore` and the schema bootstrap completed.
- `git diff --check`: passed.

### Phase 3 — completed in this working tree

The three existing hero-data contracts now read the aggregates created when a
local-lobby match closes:

- `7274 -> 7275` returns one row per hero known for the connected account. It
  includes wins, losses and averages for kills, deaths, assists, GPM, XPM, last
  hits and denies.
- `7521 -> 7522` resolves the requested account (or the connected account),
  reports the least-played recorded hero as the current progression hero, its
  games and the number of heroes already played by that account.
- `7606 -> 7607` returns a deterministic ascending list of positive hero ids
  that exist in persisted `PlayerHeroStats` rows.
- The new reads stay behind `IMatchStore`; the generated protobuf files and the
  database schema were not modified.

The response deliberately leaves win streaks, best-match peaks and challenge
lap timing unset because those values are not currently persisted. This keeps
the client response grounded in real local-lobby data instead of deriving
unsupported claims.

### Evidence for Phase 3

- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings and
  0 errors after the hero read-side changes.
- API startup against a new temporary SQLite database: passed; all four
  persistence tables and the compatibility bootstrap completed, and the API
  reached its listening state without an exception.
- `git diff --check`: passed.

### Phase 4 — completed in this working tree

The live `CMsgConnectedPlayers` path (`7034`) now carries the game server's
transient scoreboard state through to the Dota clients:

- `first_blood_happened=true` is sticky on `CSODOTALobby`, so the lobby Shared
  Object retains first-blood state for clients that reconnect during the same
  lobby.
- Packets containing connected/disconnected players, game state, first blood,
  `radiant_kills`, `dire_kills`, `radiant_lead`, building state or player draft
  are forwarded unchanged to every lobby member except the game-server caller.
- The original protobuf body is preserved, so the client receives the real
  kill totals, Radiant lead and building bitmask with their original optional
  field presence. Heartbeats without visible state are not broadcast.
- No values are calculated or persisted from `7034`; `7004 GameMatchSignOut`
  remains the authoritative source for final match statistics and history.

This is the correct split for the current protocol: `CSODOTALobby` exposes a
first-blood field, while kills, lead and building state are fields of the live
`7034` message rather than lobby Shared Object fields.

### Evidence for Phase 4

- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings and
  0 errors after the live `7034` changes.
- API startup against a new temporary SQLite database: passed; dependency
  injection resolved `IGcMessageQueue` for `ConnectedPlayersHandler`, the
  compatibility bootstrap completed and the API reached its listening state.
- `git diff --check`: passed.

## Match close data flow

```text
local lobby
  -> listen server starts and reports 4506
  -> ConnectedPlayers (7034) mirrors game state/hero/leaver and forwards live
     first-blood/kills/lead/building updates to lobby clients
  -> game server sends GameMatchSignOut (7004)
  -> GameMatchSignOutHandler normalizes CMsgGameMatchSignOut
  -> MatchStore transaction writes match, players, overall and hero aggregates
  -> RankStore applies the result once
  -> account Shared Object receives wins/losses/games/leavers
  -> lobby Shared Object becomes POSTGAME and clients receive 8081
```

`7004` is the authoritative source for final scoreboard data. `7034` remains
the live lobby/game-state source and is not a replacement for final statistics.

## Persistence contract

`Matches` is keyed by `MatchId` and stores lobby/mode/timing/result/team/server
metadata. `MatchPlayers` is keyed by `(MatchId, AccountId)` and stores one final
row per playing account. The JSON item columns preserve the repeated item
arrays without inventing a second protocol model.

`PlayerProfileStats` is a fast projection containing games, wins, losses, K/D/A,
last hits, denies, damage, healing, gold, GPM/XPM, play time and leavers.
`PlayerHeroStats` carries the same useful counters grouped by account and hero.
The per-match rows remain the source of truth for future match-history and
hero-standings handlers. The current history readers query those per-match rows
directly, so they do not reconstruct history from lossy profile counters.

## Next order

1. Expose richer profile-card/recent-match projections from the persisted rows.
2. Add EF migrations and replace the startup SQL bootstrap after the schema has
   stabilized.
3. Validate with two consecutive accounts and two real Windows clients through
   create -> join -> launch -> play -> 7004 -> reconnect/profile/history.
4. Only then widen the scope to spectators, invites, matchmaking and other GC
   surfaces.

## Important limitations

- Lobby objects and the server-to-lobby index are still in memory; completed
  match data is persistent but a running lobby is not.
- The profile card handler still exposes rank-focused fields. The account
  Shared Object now has real aggregate counters; the richer profile-card
  projection belongs to the next phase.
- Match-history rows expose the fields available in the current contracts. Rank
  change/previous rank is not yet stored per match, and the compact player slot
  currently preserves only the Radiant/Dire boundary (0/128) because the final
  sign-out payload has no lobby slot field.
- The history endpoint honors an explicit `include_practice_matches=false`, so a
  client request that excludes practice games will intentionally receive no
  local-lobby rows until another match category exists.
- Hero standings and order contain only hero ids already recorded for the
  account/installation; they are not a complete public Dota hero catalog until
  one is imported from the targeted client build.
- Hero best values, streaks and all-hero challenge timing remain unset because
  the current match-close projection stores totals, not those event histories.
- Live kills, Radiant lead and building state are delivered by `7034` while the
  client is connected; the current lobby Shared Object schema has no fields for
  those values, so a reconnect depends on the next visible `7034` update.
- The fallback server lookup for simultaneous local launches remains a known
  risk until the game-server/lobby association is made explicit for every
  launch.
- No test project should be added unless explicitly requested. Use the solution
  build, startup smoke check and real client captures.

## Working conventions

- Update this `HANDOFF.md` when a phase changes, before committing.
- Work directly on `main` for this repository, using explicit file lists with
  git; never use `git reset --hard`, `git clean -fd` or broad destructive cleanup.
- Keep `TreatWarningsAsErrors` green and do not claim a client build is
  compatible without a real client verification.
- The current database bootstrap is transitional. Do not silently change old
  columns; add an explicit compatibility path or a migration.

## Current handoff

Phases 1, 2, 3 and 4 are implemented and verified at compile/startup level. The
next session should expose richer profile-card data, then stabilize the schema
and validate the complete flow with two actual Windows clients. Preserve the
capture/replay evidence in the repository diagnostics output before calling the
vertical complete.
