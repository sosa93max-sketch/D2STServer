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

### Phase 1 — completed in this working tree

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

## Match close data flow

```text
local lobby
  -> listen server starts and reports 4506
  -> ConnectedPlayers (7034) mirrors game state/hero/leaver information
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
hero-standings handlers.

## Next order

1. Implement the history read handlers (`7408/7409`, `8063/8064` and the
   compatible friend/teammate requests) from `Matches` and `MatchPlayers`.
2. Implement hero standings/progress responses from `PlayerHeroStats`.
3. Expand the live `7034` path for kill totals, first blood, team score and
   building state where the current client build requests them.
4. Add EF migrations and replace the startup SQL bootstrap after the schema has
   stabilized.
5. Validate with two consecutive accounts and two real Windows clients through
   create -> join -> launch -> play -> 7004 -> reconnect/profile/history.
6. Only then widen the scope to spectators, invites, matchmaking and other GC
   surfaces.

## Important limitations

- Lobby objects and the server-to-lobby index are still in memory; completed
  match data is persistent but a running lobby is not.
- The profile card handler still exposes rank-focused fields. The account
  Shared Object now has real aggregate counters; full history/profile-card
  projection belongs to the next phase.
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

Phase 1 is implemented and verified at compile/startup level. The next session
should begin with the read side of the persisted match history, then expose the
hero aggregates through the existing generated GC contracts. Before calling the
vertical complete, reproduce it with two actual Dota 2 clients and preserve the
capture/replay evidence in the repository diagnostics output.
