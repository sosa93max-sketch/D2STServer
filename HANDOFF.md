# D2STServer handoff

Last updated: 2026-08-09

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
- Existing databases received the four new tables through the old bootstrap;
  the migration transition in Phase 6 now preserves those rows and records the
  initial EF baseline.

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

### Phase 5 — completed in this working tree

The profile page no longer depends on an empty card or an omitted conduct
payload:

- The profile-card projection now exposes `LifetimeGames`, the persisted rank
  projection and real stat slots for wins and games played when no custom
  layout has been saved.
- `ProfileCards` stores the authenticated account's selected slot layout as
  JSON. `7538 ClientToGCSetProfileCardSlots` validates the slot types, persists
  the edit and returns `7539 GCToClientProfileCardUpdated` with the rebuilt
  card. The edit is scoped to the account in the authenticated GC context.
- Saved stat, trophy, item, hero, emoticon and team slots are projected back
  through the existing `CMsgDOTAProfileCard` nested messages. Hero slots use
  the persisted per-hero wins/losses when that hero exists in local history.
- The previous implementation had no handler for `7538`, so the router
  silently returned no response when the client tried to edit the card. That
  was the direct server-side reason the edit could not complete.
- The account Shared Object now explicitly carries a local conduct state, and
  `8095 -> 8096` returns a complete conduct scorecard. The score is the local
  deployment policy (10,000, good, no reports/sanctions); match and abandon
  counts are read from persisted local matches. This prevents an omitted/zero
  behavior score from being interpreted as a restricted profile without
  pretending to have Valve's external conduct history.
- The game-server `7450 -> 7451` player-resource response now supplies both
  local scores as 10,000, the maximum communication/behavior feature levels,
  and `low_priority=false`. This keeps the pre-match feature gates consistent
  with the welcome and conduct-scorecard paths.
- The same local conduct fields are sent both at welcome time and after a
  recorded `7004`, so a reconnect and a post-match cache update use the same
  semantics.

### Evidence for Phase 5

- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- API startup against a temporary SQLite database: passed; the new
  `ProfileCards` table was created, the profile store was resolved and the API
  reached its listening state without an exception.
- `git diff --check`: passed before publication.
- The profile/conduct behavior and communication UI still need a real
  two-account Windows client run to confirm the exact build-6783 response; the
  local policy is intentionally documented as local, not as a real Valve
  conduct history.

### Phase 6 — completed in this working tree

The database schema is now migration-managed for new installations and
already-migrated databases:

- Added the EF Core `InitialSchema` migration
  `20260808144219_InitialSchema` and its model snapshot. It covers the account,
  social, rank, match, profile aggregate, hero aggregate, workshop, storage
  and `ProfileCards` tables currently represented by `D2stDbContext`.
- Normal startup now calls `Database.Migrate()`. A fresh SQLite file is created
  entirely through EF migrations and receives `__EFMigrationsHistory`.
- A database from the pre-migrations SQL bootstrap is detected by its existing
  `Accounts` table and missing migration history. The old idempotent table
  repair runs only on that first transition, adds known legacy columns such as
  `Avatar` and `IsCalibrated`, creates any missing current tables, preserves all
  rows and stamps the initial migration as applied.
- The legacy bridge is deliberately kept separate from the normal path so a
  later migration can evolve the schema without re-running the historical SQL
  on every startup.
- The runtime projects do not carry the EF Core design-time/Roslyn dependency.
  The checked-in migration classes and `Database.Migrate()` are sufficient for
  normal startup. Future migration generation must temporarily enable
  `Microsoft.EntityFrameworkCore.Design` (or use a separate tooling project)
  and then keep the generated migration checked in without shipping the design
  package in the server output.

### Evidence for Phase 6

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- Fresh SQLite smoke: `Database.Migrate()` reached the listening state and
  created the migration-managed schema.
- Legacy SQLite smoke: a pre-migrations `Accounts` row survived, `Avatar` was
  added, all required match/profile tables existed and history contained only
  `20260808144219_InitialSchema`.
- The local conduct policy remains unchanged at score 10,000 as approved; this
  phase only changes schema management.

### Runtime dependency cleanup — completed in this working tree

`Microsoft.EntityFrameworkCore.Design` was removed from `D2ST.Api` and
`D2ST.Persistence`. It was the only project-level path that brought
`Microsoft.CodeAnalysis`/Roslyn into the server output; no application source
used those APIs. The checked-in `InitialSchema` migration remains active, so
new and existing databases continue to use `Database.Migrate()` at startup.

This cleanup intentionally trades automatic in-repository `dotnet ef` commands
for a smaller runtime dependency set. When a new migration is needed, enable
the design package temporarily or run the tooling from a separate project,
generate the migration, verify it, and remove the design dependency again
before publishing the server.

### Evidence for runtime dependency cleanup

- `dotnet restore D2STServer.sln`: passed after removing both references.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- Release output no longer contains `Microsoft.CodeAnalysis*.dll` files.
- Fresh SQLite startup smoke: migrations applied and the API reached its
  listening state without the design-time assemblies.

### Bot matches — completed in this working tree

The local practice-lobby flow can now be used with one human client and Dota's
built-in bots:

- `FillWithBots` no longer requires a second human member before launch. A
  single host can enter `SERVERSETUP` when the lobby setting is enabled; the
  Dota listen server remains responsible for creating the bots.
- The bot difficulty sent through `CMsgPracticeLobbySetDetails` is retained on
  the lobby. A per-side difficulty sent through `CMsgPracticeLobbySetTeamSlot`
  is also retained for Radiant or Dire.
- When `FillWithBots` is active, `7004` player rows are accepted only for
  human members of that lobby. Bot rows cannot create fake accounts, profile
  aggregates, hero aggregates or match-history identities.
- The match's real duration, result, team scores and other match-level fields
  remain persistable. The human player's real scoreboard remains available.
- Bot matches update the human profile/statistics projection but deliberately do
  not apply Elo, because the opponent set is not human. The local conduct
  policy remains fixed at score 10,000.
- No bot AI was added to D2STServer; this phase relies on the game client's
  listen server to populate and control Dota bots.

### Evidence for bot-match support

- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- API startup against a temporary SQLite database: passed; migrations and DI
  completed and the API reached its listening state.
- `git diff --check`: passed before publication.
- A real Windows client match against bots is still pending. Until that run,
  the server-side launch and filtering behavior is verified, but it is not yet
  confirmed that build 6783's listen server accepts the current bot lobby
  projection and emits a complete `7004`.

### Phase 7 — completed in this working tree

The local conduct compatibility path now supplies both behavior and
communication values wherever the client can use them to gate features:

- `LocalConductState` owns the local defaults: behavior `10,000` and
  communication `10,000`.
- `7450 -> 7451` (`ServerToGCRequestBatchPlayerResources`) is now handled for
  every requested account. The response includes both scores, the maximum
  communication/behavior feature levels and `low_priority=false`, together
  with the persisted local wins/losses projection.
- The existing account Shared Object projection and `8095 -> 8096` conduct
  scorecard remain explicitly good and unsanctioned. The protocol's conduct
  scorecard has no separate communication-score field, so the direct
  communication score is supplied through the `7451` player-resource response.
- The change is intentionally a local compatibility policy; it does not claim
  to reproduce Valve's report, commend or moderation history.

### Evidence for Phase 7

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- API startup against a temporary SQLite database: passed; migrations applied,
  dependency injection resolved the GameCoordinator handlers and the API
  reached its listening state.
- `git diff --check`: passed.
- Real two-account Windows validation of the build-6783 UI remains pending;
  until that run, the server-side response is verified but client rendering and
  feature unlocks are not claimed as complete.

### Phase 8 — completed in this working tree

This phase addresses two reports from the client: adding a friend opens the
custom profile UI without an apparent request, and accounts created through the
admin web surface still appear with red conduct.

Friend requests use the REST path, not a GC mutation: `POST /api/friends/request`
resolves the target and `FriendService` persists the pending relationship for
both accounts. A two-account API smoke check returned HTTP 200 and showed the
pending initiator/recipient relationships through `GET /api/friends`. The
English profile panel is a client-side overlay deliberately opened by the
client after it queues the request; it is not the server's confirmation screen.

The server now honors the client's `UseActiveWebUser` option. A successful
password/web login is marked with its source IP, and a shim handshake from the
same IP can bind to that active web account instead of creating or selecting
the machine's fallback identity. Web sessions are excluded from game presence
and client-session removal. If no active web session exists, the previous
fallback identity behavior is retained for compatibility.

The account Shared Object is reconciled on every GC welcome instead of being
seeded only once. This replaces stale in-memory snapshots created by an older
build with the current account projection. The account projection continues to
use behavior and communication scores of `10_000`; the `7450 -> 7451` response
continues to provide both direct player-resource scores and the maximum feature
levels.

### Evidence for Phase 8

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- `git diff --check`: passed.
- Against a release API using temporary SQLite, an admin-created account was
  logged in through the web endpoint, then a shim request with
  `UseActiveWebUser=true` from the same IP resolved to that account rather than
  its fallback id.
- The same mapped account sent a friend request successfully and the pending
  relationship was visible to both sides.
- A decoded GC welcome for the admin-created account reported behavior
  `10000`, sequence `1`, `old=false` and `low_priority_until=0`.
- Real Windows-client validation remains pending. The inspected external
  `steam_api` client currently calls `EnsureSession()` non-blocking before the
  friend POST; on a brand-new client session, the first click can therefore
  receive `401` before the token exists while the overlay still opens. That
  client-side race is outside this repository and was not changed here.

### Phase 9 — completed in this working tree

The profile showcase flow now supports the editable profile and mini-profile
views used by the client:

- `8888 -> 8889` authenticates the edit against the caller's GC account,
  validates the showcase type and item count, normalizes the local moderation
  state to `Ok`, stores the exact protobuf showcase payload and returns it as
  `validated_showcase`.
- `8886 -> 8887` honors the requested `AccountId`, so any client can load the
  saved profile or mini-profile of another account. `Profile` and
  `DefaultProfile` share one saved profile; `MiniProfile` and
  `DefaultMiniProfile` share one saved mini-profile.
- A new `Showcases` table is keyed by `(AccountId, ShowcaseType)` and stores
  the format version, encoded payload and update time. This keeps item
  positions, scale, background and all generated showcase item variants
  intact across reconnects and server restarts.
- The existing editable profile-card path (`7538 -> 7539`) remains account
  scoped and durable through `ProfileCards`; this phase makes the separate
  showcase/mini-profile path durable and publicly readable as well.

“Visible to everyone” is implemented through the public read request: when a
client opens an account, it asks for that account's `AccountId` and receives
the persisted data. The build has no separate unsolicited showcase-updated
message, so clients that already have a profile open refresh it on their next
read rather than receiving a live broadcast.

### Evidence for Phase 9

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- A fresh SQLite database applied both `InitialSchema` and
  `20260808170000_AddShowcases`; an existing database applied only the new
  migration without losing its prior rows.
- Two independent authenticated GC sessions were used: the owner saved a
  mini-profile item `101` and a profile item `202`; the viewer loaded both by
  the owner's `AccountId` and received the item ids and saved position data.
- After stopping and restarting the API, the viewer loaded both records again
  without rewriting them: `RESTART_MINI_PUBLIC_LOAD=ok` and
  `RESTART_PROFILE_PUBLIC_LOAD=ok`.
- `git diff --check`: passed.
- Real Windows build-6783 rendering and editing remain pending; the server
  path is verified with protocol-level two-account smoke coverage.

### Phase 10 — completed in the external client shim

The Windows client regressed during startup after the Workshop cache was added:
the Dota console stopped at `Creating SteamUGC`, Dota remained in the
background and no UI appeared. The server's empty Workshop response and the
generated `subscriptions.json` were symptoms, not a malformed server payload.

- `SteamUGC` previously called `WorkshopManager.Initialize()` from its
  constructor. That constructor runs while `SteamEmulator.Initialize()` is
  still creating native interfaces, so it could synchronously inspect the
  `%LocalAppData%\\D2Max\\Workshop` cache on Dota's fragile startup path.
- The external `sosa93max-sketch/steam_api` repository now defers that call.
  Workshop state still initializes through `WorkshopManager.EnsureIdentity()`
  when a UGC operation actually needs it, while session snapshots continue to
  be applied by `APIClient` in the background.
- The valid empty snapshot
  `{"SteamId":76561197960265730,"AppId":570,"Subscriptions":[]}` does not
  need to be deleted. It records that the account has no server subscriptions.
- Published client commit:
  `0aee43c` (`Defer Workshop cache loading during SteamUGC startup`) on
  `sosa93max-sketch/steam_api:main`.

### Evidence for Phase 10

- The startup path was reproduced from source: `SteamUGC` construction was the
  last logged step before the missing `SteamUGC created` message.
- The patch removes the synchronous Workshop initialization from that
  constructor and leaves all existing lazy `EnsureIdentity()` call sites
  intact.
- `git diff --check`: passed in the client shim and the commit was pushed to
  its `main` branch.
- `dotnet restore steam_api.csproj`: passed. A full build was attempted but
  this checkout does not include the required `DllExport.bat` bootstrap, so
  Windows DLL compilation and real Dota foreground/UI validation remain
  pending.

### Phase 11 — completed in this working tree

The mini-profile conduct gate and lightweight profile-card request are now
covered by the server-side GC path:

- Client GC exchanges use the authenticated session account when
  `GameServer=false`. Only game-server exchanges may select the Steam id sent
  in the request, which preserves `UseActiveWebUser` instead of replacing it
  with the shim's machine/fallback id.
- The `7451` player-resource response publishes raw `comm_score` and
  `behavior_score` as `10000`. The optional `comm_level` and `behavior_level`
  tier fields are left unset because they are small tier enums, not raw scores.
- `8034 -> 8035` is implemented and returns the same account-scoped profile-card
  projection as the existing `7538 -> 7539` path, so the mini-profile stats
  request no longer falls through as unhandled.

### Evidence for Phase 11

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- A release API against temporary SQLite returned `7451` for account `1`; the
  decoded result contained `comm_score=10000` and `behavior_score=10000`.
- The same authenticated session sent `8034` with a different fallback Steam id
  and `GameServer=false`; the server returned `8035` with `account_id=1`.
  `8095 -> 8096` likewise returned `account_id=1` and raw behavior score
  `10000`, confirming the session identity fix.
- `git diff --check`: passed.
- Real Windows build-6783 validation remains pending: reconnect the client,
  refresh the mini-profile and confirm that conduct shows `10000/10000` and the
  showcase edit is accepted after the cache refresh.

### Phase 12 — local economy implemented in this working tree

The server now has a local, persistent wallet and store flow for the requested
match reward and item purchases. This is intentionally independent from the
official Steam wallet, Valve Market and Valve item ownership:

- Every eligible non-leaver human winner recorded by `7004` receives exactly
  `100` local credits. The immutable ledger reference
  `match-win:{MatchId}:{AccountId}` makes retries idempotent and the wallet
  mutation is committed in the same transaction as the match.
- `Wallets` and `WalletTransactions` persist balance, reservations and the
  purchase/reward audit trail. `GET /api/store/wallet` and
  `GET /api/store/transactions` expose the authenticated account's state.
- `StoreCatalogItems` and `StoreCatalogComponents` support priced items and
  sets. An administrator can populate/update the local catalog through
  `POST /api/admin/store/catalog`; no arbitrary client-supplied definition is
  accepted at purchase time.
- `POST /api/store/purchase` provides an account-scoped REST purchase path.
  The GC path now handles store sales data, purchase init, finalize and cancel;
  init reserves credits, finalize debits and grants the durable econ item, and
  disconnect cleanup releases pending reservations.
- `EconItems` persists the client-facing `CSOEconItem` projection, including
  quantity, style, equipped states and attributes. `WelcomeBuilder` hydrates
  the econ Shared Object cache from SQLite after reconnect or restart, and
  purchase/equip updates are published as SO deltas.
- A single catalog item definition is represented as one owned stack per
  account; a set expands into its configured component definitions. Trading,
  refunds, official market synchronization and real-money payments remain out
  of scope.

### Evidence for Phase 12

- `dotnet restore D2STServer.sln`: passed.
- `dotnet build D2STServer.sln -c Release --no-restore`: passed with 0 warnings
  and 0 errors.
- A fresh SQLite database applied `InitialSchema`, `AddShowcases` and
  `AddLocalEconomy`; an existing database restarted with no pending migration
  changes.
- The isolated SQLite smoke path recorded one winning player at `100` credits,
  recorded no reward for the loser, returned no second reward on duplicate
  `MatchId`, completed a `100`-credit purchase with quantity `1`, and rejected
  the next purchase as `insufficient_funds`.
- The authenticated API smoke returned the catalog, wallet ledger and durable
  inventory after the purchase. The admin catalog endpoint returned HTTP 200
  and the inventory endpoint returned the purchased item.
- SQLite-specific `ulong` ordering was verified and fixed by sorting econ rows
  after materialization. `git diff --check` remains the final gate.
- Real Windows build-6783 validation of the store UI, balance rendering and
  client purchase/finalize sequence remains pending; server protocol and API
  paths are compile/startup/smoke verified only.

### Phase 12 follow-up — client catalog discovery and administration

The administrator can now discover the definitions that the target Dota client
actually carries instead of inventing `DefIndex` values manually:

- `DotaCatalogImporter` reads `pak01_dir.vpk`, extracts
  `scripts/items/items_game.txt`, parses client item definitions and reads
  `ClientVersion` from `steam.inf` when available.
- The importer keeps cosmetic candidates (hero wearables and global loadout
  cosmetics) and excludes defaults, tools, recipes, treasures and other
  non-equipable definitions. The gameplay `item_cost` field is deliberately
  ignored because it is gold, not local store credits.
- `GET /api/admin/store/catalog` lists active and inactive products for an
  authenticated administrator. `POST .../discover` previews the client
  definitions and `POST .../import` upserts them with a configurable default
  price. Existing prices and activation states are preserved; new products are
  inactive by default unless explicitly activated.
- `/admin` now has a Catálogo tab with client-path discovery, import, product
  editing, activation/deactivation and manual set creation. Sets use existing
  product IDs as components and do not require a client `DefIndex` of their
  own.

The path supplied to discovery is resolved on the machine running D2STServer.
If the server is remote, the Dota installation must be copied/available there
or a separate export/upload flow must be added; a browser cannot read another
machine's local VPK by path alone.

### Evidence for catalog discovery and administration

- `/tmp/d2st-dotnet/dotnet build D2STServer.sln -c Release --no-restore`:
  passed with 0 warnings and 0 errors after the importer, batch upsert and UI
  changes.
- `admin.html` JavaScript syntax check: passed.
- `git diff --check`: passed.
- A real Windows Dota installation is still required to validate the VPK
  reader's discovered count, build number and the resulting client store UI.

### Phase 13 — administrative wallet controls and paginated admin UI

The local economy is now manageable from `/admin` without loading the complete
catalog into the browser:

- `POST /api/admin/users/{accountId}/wallet/adjust` accepts a positive or
  negative `Delta` and an optional `Reason`. Positive adjustments credit the
  wallet; negative adjustments can only consume currently available credits and
  never reduce the balance below a pending purchase reservation. Every change
  is recorded as `AdminAdjustment` in `WalletTransactions` with a unique admin
  reference.
- The user administration projection now includes balance, reserved credits
  and available credits. The page uses
  `GET /api/admin/users/page?page=&pageSize=&search=&status=` so only the
  visible user slice and its avatars are rendered. The existing unpaged user
  endpoint remains available for compatibility.
- `GET /api/admin/store/catalog/page?page=&pageSize=&search=&status=&type=`
  queries only the requested product slice and returns total/active counts.
  This keeps a catalog of 11,390 products from creating 11,390 DOM rows on
  first load.
- `/admin` now presents compact summary cards, search/status/type filters,
  pagination, a reduced catalog table and per-user `Saldo +` / `Saldo −`
  actions. The authenticated REST balance remains available at
  `GET /api/store/wallet`, and the GC welcome now also supplies the native
  Dota currency/balance fields for the connected account.
- Account deletion now removes the account's wallet, ledger, purchase and econ
  inventory rows along with the existing account data.

### Evidence for administrative wallet controls and UI

- `admin.html` JavaScript syntax check: passed.
- `git diff --check`: passed.
- The current execution environment has no `dotnet` executable, so the release
  build could not be rerun in this pass; the previous baseline build remains
  recorded above and a build is required before deployment.
- Real Windows build-6783 validation remains pending for the catalog display,
  balance rendering and purchase/finalize sequence. The standard GC sales
  response exposes prices, while the local balance is currently enforced by the
  server and exposed through REST plus the native `CMsgClientWelcome` balance
  fields. The native client still renders the value using its standard currency
  formatter rather than a custom “créditos” label.

## Match close data flow

```text
local lobby
  -> listen server starts and reports 4506
  -> ConnectedPlayers (7034) mirrors game state/hero/leaver and forwards live
     first-blood/kills/lead/building updates to lobby clients
  -> game server sends GameMatchSignOut (7004)
  -> GameMatchSignOutHandler normalizes CMsgGameMatchSignOut
  -> MatchStore transaction writes match, players, overall and hero aggregates
  -> same transaction credits 100 local credits to each eligible winning human
     and writes an idempotent wallet ledger row
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
`ProfileCards` stores the authenticated account's selected profile-card slots;
the profile-card builder combines that layout with the real aggregate and
per-hero projections. No profile-card item ownership or showcase moderation
state is fabricated. `Showcases` stores one opaque protobuf payload per account
and canonical profile/mini-profile type, so public reads do not depend on the
editor's in-memory session.
`Wallets` stores the current local-credit balance and reserved checkout amount;
`WalletTransactions` is the immutable reward/purchase ledger with unique
references for idempotency. `StoreCatalogItems` and
`StoreCatalogComponents` define the administrator-managed local item/set
catalog. `EconItems` is the durable per-account `CSOEconItem` projection used
to rebuild the volatile econ Shared Object cache after reconnects.
The per-match rows remain the source of truth for future match-history and
hero-standings handlers. The current history readers query those per-match rows
directly, so they do not reconstruct history from lossy profile counters.

## Next order

1. Run `/admin` on the machine with the target Dota installation, discover the
   build 6783 definitions, assign prices/activate the intended products, then
   validate one real Windows client through catalog display, balance, purchase,
   reconnect and inventory rendering.
2. Validate web-account login, friend request and conduct/feature-gate refresh,
   then continue with Dota bots through create -> enable `FillWithBots` ->
   launch -> play -> `7004` -> reward -> purchase -> reconnect/profile/history.
3. If the machine can run two client sessions, repeat the economy and match
   validation with two accounts on the same PC. Otherwise keep the two-human
   validation as a pending external test.
4. Only then widen the scope to spectators, invites, matchmaking and other GC
   surfaces.

The complete capability inventory, dependencies, priorities, validation gates
and future/out-of-scope decisions are maintained in
[ROADMAP.md](ROADMAP.md). This handoff remains the execution log; the roadmap
is the planning reference for the work that follows the real-client gate.

## Important limitations

- Lobby objects and the server-to-lobby index are still in memory; completed
  match data is persistent but a running lobby is not.
- The profile card now exposes lifetime games and the real wins/games stat
  slots, plus a persisted editable layout. Badge points, trophies, leaderboard
  rank, previous-season rank, MVP totals and other fields remain zero because
  their source data is not persisted yet.
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
- The conduct scorecard and the `7451` player-resource values are a local
  compatibility policy, not a Valve behavior history. There is no report,
  commend, moderation or low-priority enforcement pipeline yet; local
  match/abandon counts are real, while the 10,000 good behavior and
  communication scores are deliberately server-owned.
- `UseActiveWebUser` requires a recent password-authenticated web session from
  the same source IP. Without it, the shim keeps using its configured fallback
  identity; the admin-created account must therefore be selected through that
  web session or by configuring the client fallback id to the account id.
- The server friend endpoint persists valid authenticated requests. The
  external client's immediate first-use session race can still make the first
  click appear ineffective, and the custom profile overlay's English strings
  are owned by that client rather than this server.
- The external client now defers Workshop cache loading until after
  `SteamUGC` interface construction. The published fix is source-verified but
  still requires rebuilding/replacing the Windows `steam_api` DLL and running
  Dota once to confirm the foreground/UI path on the target machine.
- Profile-card slots and profile/mini-profile showcase payloads are persisted
  and publicly returned. The `8034 -> 8035` statistics surface now reuses the
  account-scoped profile projection. Store catalog and econ ownership are now
  validated locally, but showcase item/trophy semantics still require local
  source data or a real client capture.
- Showcase edits are available to every client on its next public read. The
  targeted build exposes no dedicated unsolicited showcase-update message, so
  already-open profile windows are not pushed live by the server.
- A bot lobby can launch with one human when `FillWithBots` is enabled, but the
  built-in Dota bot population and the exact `7004` payload still need real
  build-6783 validation. Bot rows are intentionally excluded from
  `MatchPlayers` and all account projections; bot matches do not change Elo.
- The normal database path is migration-managed. The old SQL bootstrap remains
  only as a one-time compatibility bridge for databases created before
  `__EFMigrationsHistory` existed; it is not used for fresh databases.
- The local economy is not Steam Wallet or Valve Market. `StoreCatalogItems`
  starts empty on a fresh installation and must be populated by an administrator
  with definitions/prices appropriate for the target client build. The new
  importer reads local client definitions, but it does not infer local prices
  from `item_cost` and does not claim official Valve ownership or market values.
- Client catalog discovery reads the VPK on the D2STServer machine. A remote
  browser cannot make the server read a path on the administrator's PC; use a
  shared installation/export or add a dedicated upload path before deploying
  the server on another host.
- The match reward is currently a fixed local policy: `100` credits per clean
  winning human row, once per persisted `MatchId`/account reference. No
  refund, trade, gift, real-money payment or cross-server wallet sync exists.
- The client-facing balance is exposed by the local REST store API and enforced
  by both REST and GC purchase handlers. Whether the unmodified Windows client
  renders that balance and completes the full purchase UI still requires a
  build-6783 capture.
- EF Core design-time tooling is intentionally not part of the server projects;
  future migration generation requires the temporary workflow described above.
- The fallback server lookup for simultaneous local launches remains a known
  risk until the game-server/lobby association is made explicit for every
  launch.
- No test project should be added unless explicitly requested. Use the solution
  build, startup smoke check and real client captures.

## Phase 14 — Users administration workspace

- Replaced the admin Users view with a denser operations workspace: summary
  metrics, persistent search/status/page-size filters, a paginated directory,
  and a responsive inspector panel that uses the available screen space.
- Every visible user row now exposes direct actions for adding/removing wallet
  credits, adding/removing MMR, editing persona, changing avatar/password,
  resetting MMR and deleting the account. Normal operations no longer use
  browser prompts; destructive deletion and MMR reset remain confirmed.
- The inspector manages profile/access, wallet balance/available/reserved
  values, MMR calibration and the danger zone without leaving the user list.
- User search now accepts canonical Steam IDs as well as account IDs,
  usernames and persona names.
- Evidence: `git diff --check`, admin JavaScript syntax validation and a DOM
  audit for unique IDs and all JavaScript element references pass. The current
  environment still lacks `dotnet`, so the solution build and real
  build-6783 client validation remain pending.

## Phase 15 — local Dota Plus entitlement (Phase 1)

The first Dota Plus slice is now server-owned and persistent for the LAN
deployment. It is deliberately independent from Valve billing or Steam Wallet:
an administrator grants the entitlement to a local account.

Implemented:

- `DotaPlusAccounts` stores one entitlement per account with start, expiry,
  enabled state, local flags and update time. `DotaPlusTransactions` records
  every activation, extension and revocation with the acting admin, days,
  reason and resulting expiry.
- `PUT /api/admin/users/{accountId}/dota-plus` activates/extends a subscription
  for 1–3,650 days or revokes it immediately. `GET /api/dota-plus` exposes the
  authenticated account's current local status.
- The Users workspace shows Dota Plus state and remaining days in the table and
  provides activate/extend/revoke controls in the per-user inspector and direct
  actions. The operation is available without leaving the user directory.
- The GC publishes a populated `CSODOTAGameAccountPlus` (SO type 2012) at
  welcome time. The projection carries local status and Unix-time boundaries,
  and an admin mutation publishes a Shared Object delta to an already-connected
  client.
- Profile cards now derive `IsPlusSubscriber` from the persisted entitlement.
  Lobby static projections do the same per member instead of marking everyone
  as a subscriber, and an active lobby is rebuilt when its member's entitlement
  changes.
- Account deletion removes the entitlement and its audit rows.
- The reusable GameCoordinator still works without the API database through an
  empty persistence adapter.

Evidence and limits:

- `git diff --check` passes and the admin JavaScript was reviewed for the new
  controls and endpoint wiring.
- The current environment has no `dotnet` executable, so the Release build and
  SQLite migration/startup smoke could not be rerun in this phase. Windows/.NET
  build validation is required before deployment.
- This phase exposes the subscription entitlement only. Shards, hero/player
  challenges, match progress, relics and their rewards are the next phase; the
  `CSODOTAPlayerChallenge` cache is not populated yet.
- `PlusStatus=1` is the local active projection and `0` is inactive. The exact
  visual interpretation of date units and optional flags still needs a real
  build-6783 client capture after the DLL/server are deployed.

Next step: implement the Phase 2 Dota Plus state (shards, challenge SO objects,
match-progress updates and relic transactions) behind the same account-scoped
store, then publish that phase separately.

## Phase 16 — local Dota Plus progress (Phase 2)

The second slice makes the local entitlement useful during LAN play. All state
below is owned by the server database and is scoped to the local account.

Implemented:

- `DotaPlusAccounts.Shards` is the current shard balance. Every award, admin
  adjustment and relic charge is appended to `DotaPlusShardTransactions` with
  amount, balance-after, actor, reason, timestamp and a unique reference.
- `DotaPlusChallenges` persists three account challenges with progress,
  sequence, target/reward parameters, rank, attempts and last-match reference.
  The GC projects them as SO type 2010 and reconciles create/update/destroy
  deltas when the account logs in, is rerolled or finishes a match.
- Closing a local match through 7004 awards a fixed local shard policy and
  advances play/win/kills challenges once per match/account. The dedicated
  7587 challenge report is also accepted and shares the same idempotency guard.
- Client messages 7584/7586 now reroll a persisted challenge, 8258/8259 buys
  a common or rare local hero relic, and 8276/8277 refreshes the challenge
  projection. Relic ownership is retained in `DotaPlusRelics`; the response
  returns a deterministic local kill-eater type.
- `GET /api/dota-plus` now returns subscription, shards and challenge progress.
  The admin Users inspector shows/adjusts shards, and account deletion removes
  all Phase 2 rows. Event id 19 also reports the local shard total through the
  legacy event-points envelope used by older clients.
- The GC now wires the challenge/relic handlers and match-progress projection
  through the same empty-adapter boundary used by the API host.

Evidence and limits:

- `git diff --check`, source-reference assertions, admin JavaScript syntax and
  duplicate-DOM-id checks pass.
- The current environment still has no `dotnet` executable, so the Release
  build, EF migration/startup smoke and Windows client capture remain pending.
- Challenge template ids and kill-eater ids are deliberately local and
  deterministic. The server state and protocol transactions are functional,
  while the exact labels/item visuals of a particular Dota client build must
  be confirmed against that build's local schema before treating the visual
  relic catalog as complete.
- Phase 3 is implemented below: Dota Plus plans now connect to the local
  catalog and wallet. The remaining validation is the Windows/.NET and real
  client smoke on the LAN target.

## Phase 17 — local Dota Plus catalog purchase (Phase 3)

The third slice connects the local Dota Plus entitlement to the server-owned
catalog and virtual wallet. It remains entirely inside the LAN server: there
is no Valve billing, Steam Wallet charge or external entitlement lookup.

Implemented:

- `StoreProductType.DotaPlusSubscription` adds a catalog plan type. An admin
  can create, edit, activate, deactivate and filter plans in the compact
  catalog workspace. Each plan stores a local-credit price and `1`–`3,650`
  subscription days; plans cannot have an econ `DefIndex` or components.
- The catalog migration `20260808220000_AddDotaPlusCatalogPlans` persists the
  plan duration in both catalog rows and pending purchase transactions. The GC
  sales response advertises a plan as a local SKU using its `ProductId`, so
  the existing purchase-init/finalize path can select it.
- A Dota Plus checkout reserves the user's local credits, rejects mixed
  plan/item purchases, snapshots the total days, then at finalization debits
  the wallet and extends `DotaPlusAccounts.ExpiresAt` in the same SQLite
  transaction. The operation also appends a `purchase` row to
  `DotaPlusTransactions` and the normal wallet ledger.
- REST (`POST /api/store/purchase`) and GC finalization both refresh the Dota
  Plus Shared Objects after a successful purchase. The connected client sees
  the local subscription projection without waiting for a relogin; reconnects
  rebuild it from the database.
- The existing admin Users workspace remains the direct operational fallback:
  an administrator can activate/revoke/extend Dota Plus, add/remove shards
  and add/remove local wallet credits per account.

How to publish a plan locally: in Admin → Catálogo, use a unique ProductId,
select “Dota Plus”, leave DefIndex/components empty, set the price in local
credits and enter the number of days. Activate it when it is ready. The user
then needs that amount in the local wallet; the client or REST purchase path
uses the same SKU and wallet rules.

Evidence and limits:

- `git diff --check`, admin JavaScript parsing, duplicate DOM-id audit and
  source-reference checks pass for this phase.
- This environment has no `dotnet` or `sqlite3` executable, so the Release
  build, migration/startup smoke and Windows build-6783 capture could not be
  rerun here. Run those checks on the LAN server before deployment.
- The server-side local purchase, entitlement extension, audit and SO refresh
  paths are implemented. An unmodified client may not provide a native
  friendly label or dedicated Dota Plus checkout screen for a custom local
  SKU; that visual/schema behavior still needs a real build-6783 capture.
- Plan purchases are deliberately local-credit transactions. Refunds,
  Steam Wallet, real-money billing, Valve catalog ownership and cross-server
  synchronization are out of scope.

## Phase 18 — native wallet visibility and purchase compatibility follow-up

The first real-client symptom after Phase 17 was split into two protocol issues:
the local wallet existed only in REST, while the stock Dota client reads its
store balance from the welcome packet; and a checkout line could arrive without
an explicit quantity. This phase closes both server-side gaps without routing
anything through Valve:

- `WelcomeBuilder` now reads the authenticated account's available local
  credits and sets `CMsgClientWelcome.TxnCountryCode`, `Currency` and `Balance`.
  The client receives the balance on the next GC hello/reconnect. The wire
  amount uses the same minor-unit value as catalog prices, so `100` credits is
  shown by the stock USD formatter as `$1.00` and costs `100` credits server
  side.
- The store sales response uses the same bounded wire conversion as the
  welcome balance, preventing prices and balances from being represented in
  different units.
- `StorePurchaseInitHandler` treats an omitted protobuf `quantity` (`0`) as a
  single unit, continues to use the server catalog as the authoritative price,
  and logs stale client prices, unknown/inactive definitions and the exact
  local result code. Finalization logs the transaction result and resulting
  wallet as well.

Evidence and limits:

- `git diff --check` passes and the dependency/source-reference audit confirms
  that the API's `IEconomyStore` registration and the fallback empty store both
  satisfy the new `WelcomeBuilder` dependency.
- This environment has no `dotnet` or `sqlite3` executable, so a Release build,
  migration/startup smoke and real Windows build-6783 purchase capture could
  not be rerun here. The real client must reconnect after deploying this
  commit; inspect the server GC log for `Compra local init` and
  `Compra local finalize` entries if the client still displays a generic error.
- A native stock Dota screen cannot label the value “créditos”; it will display
  the configured standard currency. A literal local-credit widget would
  require a client/overlay change and is outside the server protocol.

## Phase 19 — local economy expressed in USD dollars 1:1

The local wallet and catalog now use whole USD dollars as their server-side
unit. This is still a fictitious local economy with no Valve billing, Steam
Wallet charge or real-money settlement:

- `1` in a wallet, ledger row, catalog price or purchase total means `$1.00`.
  The fixed clean-match reward is now `$1` through
  `EconomyRules.MatchWinRewardDollars`; new catalog imports default to `$1`.
- The REST/admin contracts and the admin UI expose `BalanceDollars`,
  `ReservedDollars`, `AvailableDollars`, `PriceDollars` and
  `DefaultPriceDollars`. The admin wallet adjustment accepts `DeltaDollars`.
- The native Dota protocol still receives USD minor units: the server sends
  `$1` as `100` wire units so the stock client renders `$1.00`. Catalog prices
  and the welcome balance use the same conversion.
- Migration `20260808230000_ConvertLocalCreditsToDollars` renames the economy
  columns and divides the previous minor-unit values by `100`, preserving the
  existing fictitious USD value during upgrade. Pending reservations and
  purchase totals are converted together with balances and ledger history.

Evidence and limits:

- Static source-reference audit, admin JavaScript syntax, migration SQL smoke
  and `git diff --check` pass in this environment.
- This environment has no `dotnet` executable, so the Release build, EF
  migration/startup smoke and Windows build-6783 capture must be rerun on the
  LAN server. Confirm that an old database applies the conversion migration,
  then verify `$1`, `$100` and a purchase through the native client.
- The economy remains local/fictitious; it does not synchronize with Valve's
  official wallet, market, catalog or ownership.

## Phase 20 — Steam Market price synchronization for the local catalog

The catalog can now use current Steam Community Market references without
making the D2ST purchase path depend on Steam at runtime:

- `DotaCatalogImporter` reads `resource/localization/items_english.txt` from
  the same `pak01_dir.vpk` and exposes a display name alongside the internal
  item key. A catalog re-import therefore gives the market resolver a useful
  search name instead of only values such as `wearable_*`.
- `StoreCatalogItems` now stores the exact `MarketHashName`, lowest and median
  USD prices in cents, volume, source, status and last-check time. Migration
  `20260808233000_AddMarketPricing` adds these fields without changing the
  existing whole-dollar local economy.
- `SteamMarketPriceSync` uses Dota 2's Steam Market endpoints (`appid=570`),
  resolves a name only on an exact normalized match, applies either the
  lowest or median value, rounds it to the existing whole-dollar local unit
  with a minimum of `$1`, and keeps the exact cents for audit/display.
- Administrators can call `POST /api/admin/store/catalog/market-prices` or
  use “Actualizar precios Steam” in the catalog panel. The operation is
  serialized, limited to 500 items per run, defaults to active products and
  skips matched products younger than the configured 60-minute cache window.
  An administrator can enter an exact `MarketHashName` manually for unusual
  names, quality variants or items whose market listing is ambiguous.
- Items without an exact market match, without current listings or without a
  usable display name remain at their current local price and are reported;
  they are never silently assigned a market value from `item_cost`, because
  that field is gameplay gold.

Evidence and limits:

- The public Steam endpoint was verified with Dota 2 `Feast of Abscession`,
  returning lowest price, median price and volume. The endpoint is
  undocumented and rate-limited, so the server uses a delay, cache and batch
  limit; it should never be called by the Dota client.
- A temporary .NET 10.0.302 SDK was installed for this validation. `dotnet
  build D2STServer.sln -c Release --no-restore` passed with 0 warnings and 0
  errors, and a temporary SQLite startup smoke applied
  `20260808233000_AddMarketPricing` successfully. The Windows build-6783
  catalog/client capture remains pending: re-import the target catalog,
  activate a small group of items, synchronize one batch, inspect the stored
  status/price and then verify the native store/purchase flow.
- The displayed local price remains a fictitious whole-dollar USD value. A
  future exact-cent local economy would require changing wallet/catalog/
  purchase totals and wire conversion together; this phase does not do that.

## Phase 21 — same-account web store fallback

The local economy now has a consumer-facing store that can be opened from the
launcher without asking for a second password:

- `/store` is a same-origin responsive page with catalog search, prices, local
  balance, purchase history and activated inventory. It is intentionally
  separate from the administrator UI at `/admin`.
- `POST /api/store/handoff` creates a 90-second, single-use code bound to the
  authenticated launcher session. `POST /api/store/handoff/exchange` consumes
  it and sets an `HttpOnly`, `SameSite=Strict` cookie limited to `/api/store`.
  The permanent launcher bearer token is never placed in a URL or returned to
  browser JavaScript.
- Store endpoints accept that cookie as well as the existing bearer token, so
  the account scope still comes from the server session rather than request
  body ids. Logout clears the browser cookie.
- The store purchase path uses the existing atomic begin/finalize operation,
  immediately persists `EconItems` and publishes the SO delta. A connected
  client receives the new item; a disconnected client receives it after the
  next welcome/reconnect. `/api/store/inventory/equip` also exposes the same
  validated equip projection for trusted store tooling.
- Native init/cancel and disconnect-cleanup handlers now log transaction ids,
  results, balances and reservation cleanup. This makes the missing native
  finalize step distinguishable from a catalog rejection in the next capture.

Root cause confirmed from the supplied capture: the native client created local
reservations for transactions 2–10, but no `StorePurchaseFinalize` reached the
server; later cancellation/cleanup released those reservations. Separate
attempts for definitions such as 19994–19999, 20002, 20051 and 32658 were
rejected because the corresponding active local catalog entry was absent. The
web checkout avoids the unavailable native/Steam payment step while using the
same account, catalog, wallet and durable inventory projection.

Evidence for this phase:

- `dotnet restore D2STServer.sln` and `dotnet build D2STServer.sln -c Release
  --no-restore` pass with 0 warnings and 0 errors using SDK 10.0.100.
- A temporary SQLite end-to-end smoke created a user, issued a handoff,
  exchanged it for the HttpOnly cookie, loaded `/store`, read the catalog and
  wallet, completed a purchase and verified the durable inventory item. A
  second exchange of the same code returned HTTP 401.
- The store page JavaScript syntax check and `git diff --check` pass.
- Native build-6783 rendering and the Windows Qt launcher build still require
  the target Windows machine. The local economy remains virtual: no Steam
  Wallet charge, real-money payment, refund or Valve ownership is implied.

## Phase 22 — session revocation, safe catalog replacement and polished clients

This phase closes the remaining store and launcher issues reported after Phase
21:

- Launcher logout now removes the exact authenticated token that the browser
  store received through the handoff and sends an expired `d2st_store_session`
  cookie. The web page also heartbeats `/api/store/session` every 15 seconds,
  so an open store tab notices launcher logout and disables its catalog.
- The consumer catalog uses `GET /api/store/catalog/page` with server-side
  pagination, search, product type, category and hero filters. Imported Dota
  hero names are persisted in `HeroesJson` and returned to the UI as hero tags.
- `POST /api/admin/store/catalog/clear` removes catalog products/components
  while preserving every user's durable `EconItems` inventory. The admin import
  checkbox performs a validated clear followed by a clean import using the new
  default price/activation values.
- Repeated imports are idempotent by item `DefIndex`; the migration removes old
  duplicate rows, keeps the lowest stable product id and creates a filtered
  unique index. Existing prices, activation and Steam market metadata survive a
  normal import. Direct admin saves also preserve omitted market/hero metadata.
- The store displays the exact matched Steam lowest price in cents and shows
  the separate local whole-dollar amount required by checkout. Steam sync now
  rounds that local amount upward, avoiding undercharging. The wallet/GC wire
  contract remains whole-dollar for compatibility; this is not a real-money or
  official Steam Market settlement.
- The launcher no longer shows `--`: its header subscribes to `pingFinished` and
  renders checking, online/version or offline states. The `Agregar cuenta`
  button was removed. The main window received a card-based dark glass theme,
  shadows, status pills and improved spacing. Dota detection now checks the
  saved path, executable/Dota/Steam root forms, Steam registry and library VDFs,
  environment/common roots and drive candidates; launch resolves and validates
  `dota2.exe` before starting.

Evidence for this phase:

- `dotnet build D2STServer.sln --configuration Release` passed with 0 warnings
  and 0 errors using SDK 10.0.100.
- EF reports no pending model changes. A clean temporary SQLite startup applied
  `20260809010000_ImproveStoreCatalog`, including the `HeroesJson` column,
  duplicate cleanup and filtered unique index.
- API smoke verified duplicate upsert resolution to one product, hero filtering,
  catalog clear, handoff exchange, session HTTP 200 before offline and HTTP 401
  after launcher-style `/api/presence/offline`.
- Store and admin JavaScript syntax checks plus `git diff --check` passed.
- CMake configuration of `new_launcher` was attempted; Qt6 is not installed in
  this Linux environment, so the Windows Qt build still requires the target
  machine/CI. No native client compatibility claim is made without that build.

## Phase 23 — immediate web inventory sync, real catalog pricing and localization

This phase addresses the three store issues observed after Phase 22 and records
the launcher redesign delivered alongside them:

- `EconInventory.Purchase` now reconciles the durable inventory and pushes a
  complete account-owned SO snapshot after a successful web checkout. The
  existing delta is still written first, while `PushSubscribe` repairs a live
  client that missed the delta while establishing its econ subscription. A
  connected Dota client therefore does not need to be restarted; a disconnected
  client still receives the same inventory on its next welcome.
- Dota catalog imports accept a language (Spanish by default), use that
  localization for the visible store name and retain the English localization
  as `MarketSearchName` for Steam lookup. The new
  `20260809020000_AddMarketSearchName` migration stores that second name.
- The import fallback is now `0`, never an implicit `$1`. New/unresolved
  products are not purchasable until the Steam lookup resolves them. The import
  endpoint queues item ids in `MarketPriceRefreshQueue`, which processes
  batches of 500 in the background through Steam Market `appid=570`. A
  successful match stores lowest/median cents, rounds the local whole-dollar
  checkout amount upward for compatibility, and can activate the item when the
  administrator selected activation. No-match/no-data products are cleared to
  zero/inactive unless they carry a previous verified or explicitly manual
  price.
- Native `RequestStoreSalesData` now advertises the verified Steam cents when
  available, instead of falling back to `$1`; unresolved products are omitted.
  The local wallet and purchase ledger remain whole-dollar by design, so the
  web UI continues to show the exact Steam reference separately from the
  rounded local balance required for checkout.
- `/store` now hides the purchase/catalog view after a 401 or heartbeat
  revocation and shows a separate session gate with retry and return actions.
  It no longer mixes a closed-session message into the product grid.
- `new_launcher` now has a full dashboard layout: navigation rail, account
  hero, metrics, grouped Dota/server settings and a distinct launch action.
  Existing handoff, login, autodetection and process controls remain wired to
  the same server contracts.

Evidence for this phase:

- `/tmp/dotnet-sdk-d2st/dotnet restore D2STServer.sln` and
  `dotnet build D2STServer.sln -c Release --no-restore` pass with 0 warnings and
  0 errors using SDK 10.0.100.
- A temporary SQLite API startup applied the full migration chain, including
  `20260809020000_AddMarketSearchName`, and reached the listening state.
- Store/admin JavaScript syntax checks and `git diff --check` pass.
- CMake configuration of `new_launcher` was attempted; Qt6 is not installed in
  this Linux environment, so the Windows Qt build and the real build-6783
  client validation remain pending. No native client compatibility claim is
  made without that validation.

## Phase 24 — native launcher store and bearer-only session

The consumer browser store has been retired. `new_launcher` now renders the
catalog, wallet, inventory, activity and purchase controls as a native Qt view.
The server keeps the authenticated `/api/store/*` REST surface for that view,
but no longer publishes `/store`, handoff tickets or store cookies.

- Session authentication is bearer-only. The native view sends the exact token
  returned by `/api/auth/login`, so logging in on the launcher and opening the
  store use one session instead of a browser cookie bridge.
- The legacy store HTML, handoff service, cookie helper and their DI/endpoint
  registrations were removed. Admin catalog pages remain browser-backed and
  continue to use their own bearer token in `sessionStorage`.
- `ServerClient` now parses catalog pages, filters, wallet, inventory,
  transactions and purchase responses, including HTTP 401 detection. A native
  purchase refreshes the visible catalog and inventory and still uses the
  server-side SO snapshot path described in Phase 23.

Evidence: `dotnet restore D2STServer.sln --force` and
`dotnet build D2STServer.sln -c Release --no-restore` pass with 0 warnings and
0 errors. A temporary SQLite startup applies all migrations and reaches the
listening state; `/store` returns 404 and unauthenticated `/api/store/catalog`
returns 401. Qt6/Windows visual and purchase validation remain pending because
Qt6 and the target Dota client are not available in this Linux environment.

## Phase 25 — bearer ranking endpoint for the native launcher

The server now exposes `GET /api/ranking?page=1&pageSize=50` for authenticated
launcher sessions. It joins calibrated `PlayerRanks` with `Accounts`, orders by
MMR descending with deterministic wins/games/AccountId tie-breakers, includes
online presence from `ISessionStore`, and returns the player's display name,
MMR, Dota rank tier/star/progress and basic games/wins/losses/win-rate data.
`SteamId` is serialized as text on this endpoint so Qt clients do not round a
64-bit Steam id through a JSON number. Uncalibrated accounts are intentionally
excluded; an empty result is a valid ranking state for the launcher UI.

The medal source was verified against the public Dota game-file index:
`dota/pak01_dir.vpk` contains the official compiled assets under
`panorama/images/rank_tier_icons/rank0_psd.vtex_c` through
`rank8_psd.vtex_c`. This API returns the numeric tier/star; the native launcher
maps those values to its local display badge because the server should not
serve Valve's compiled VPK assets as web files.

Evidence: `git diff --check` passes. `/tmp/dotnet-sdk-d2st/dotnet restore
D2STServer.sln` and `dotnet build D2STServer.sln -c Release --no-restore`
pass with 0 warnings and 0 errors. An authenticated SQLite smoke confirmed
HTTP 401 without a bearer, HTTP 200 with a launcher login and the valid empty
payload `Items:[]`, `TotalCount:0`. The endpoint still needs a live Windows/Qt
check with both populated and empty rank tables.

## Working conventions

- Update this `HANDOFF.md` when a phase changes, before committing.
- Every code or configuration adjustment must update this `HANDOFF.md` in the
  same change, recording the implementation, evidence, limitations and next
  step before the commit.
- After validation, commit the intended adjustment and push it to `main`; then
  verify the remote branch contains the commit before reporting completion.
- Work directly on `main` for this repository, using explicit file lists with
  git; never use `git reset --hard`, `git clean -fd` or broad destructive cleanup.
- Keep `TreatWarningsAsErrors` green and do not claim a client build is
  compatible without a real client verification.
- The current database bootstrap is transitional. Do not silently change old
  columns; add an explicit compatibility path or a migration.

## Current handoff

Phases 1–25 and the server-side bot-match support are implemented, with the
schema managed by EF Core and legacy databases preserved through the
transition bridge. The next session should build the launcher on Windows,
open the native store from the launcher, validate a real Steam-priced catalog
purchase and confirm the pushed inventory snapshot in a live build-6783 client,
then capture the native sales/purchase sequence on Windows.
Preserve capture/replay evidence, rebuild the published client shim and verify
that Dota returns to the foreground before claiming client compatibility. Also
validate `/api/ranking` with both calibrated users and an empty rank table, and
confirm the native launcher renders both states. See
[ROADMAP.md](ROADMAP.md) for the complete plan; do not treat client
compatibility as complete until it is recorded here.
