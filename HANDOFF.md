# D2STServer — Handoff / Continuation Guide

This document is a complete snapshot of the project so **any developer or AI can
continue in a fresh chat** without prior context. It states what exists, why,
how to build/run/test, and exactly what remains.

- **Repo:** https://github.com/mukipromax-web/D2STServer (public)
- **Language/stack:** C# / .NET 10, ASP.NET Core Minimal API, protobuf-net, EF Core + SQLite.
- **No test project.** It was removed on request; verification is `dotnet build`
  plus a capture from the real client. Do not add one back unless asked.
- **Last update:** the practice-lobby launch flow (stage 4g continuation): a
  launch is answered, the lobby's game server is welcomed and attached, and the
  lobby moves to `RUN` with a connect string and a match id — the handoff a 1v1
  start needs. See §3.18. Before that: stage 4h — chat: the channels a client joins, talks in and
  leaves, with the ones that exist before anybody joins them (and which of them
  a client is put in at logon) configured on the server, plus private chats.
  See §3.21. Before that: the push channel fix — a client-bound GC message now travels
  as a `gc_message` event, which is the channel the shim's event pump actually
  drains: a Dota client never calls `/api/gamecoordinator/poll`, so every Shared
  Object delta the GC pushed (the lobby it just created included) used to be
  written to a queue nobody read. See §3.20. Before that: stage 4g (first half) — practice lobbies: `CSODOTALobby`
  (type 2004) on a cache owned by the lobby itself, and the nine GC messages the
  lobby screen sends (create, join, leave, set details, set team slot, kick,
  kick from team, launch, list). Before that: stage 4f — party: `CSODOTAParty`
  (type 2003) on a cache owned
  by the party itself, invites as one-object caches (type 2006), and the ten GC
  messages the party UI sends (invite, response, leave, kick, cancel invites,
  set leader, set coach, ping data, ready check request/acknowledge); stage 4e — econ/inventory: items as Shared Objects on the
  econ cache (`EconInventory`), the equip/style/position/store handlers 7.22g
  sends, and `/api/gamecoordinator/econ/*` to put items in an account; stage 4d (the SO / push foundation those items are written through:
  `SoCacheStore`, `SoCacheService`, `IGcMessageQueue`, and `WelcomeBuilder`
  rebuilt on top of them), stage 4c (the seven
  handlers the first real 7.22g capture asked for), the test project removed,
  and stage 0 of the shim work in §4 (logging, ini and shutdown fixed in the
  shim; data folder renamed to `D2MAX`).

---

## 1. Goal

Build a fresh, well-designed **Dota 2-only** private backend: an emulated Steam
service + **Game Coordinator (GC)** for AppID 570. It is meant to be used with:

- **D2MaxLauncher** — WPF launcher (`gerardopromax1-del/D2MaxLauncher`).
- A **Steamworks client shim** (`manuelalmaguersosa2-creator/steam_api`) that replaces the
  `steam_api64.dll` `dota2.exe` loads and translates Steam API calls into HTTP
  calls against this server. Installation and configuration in §4.

It is a ground-up redesign inspired by the older `soulhuntermax/SKY_server`
(kept only as a reference), intentionally **Dota-only**, cleaner, and with GC
logic in **C#** (no TypeScript scripting VM).

**Immediate priority:** full support for **Dota 2 7.22g** (the build the user has
for testing), while keeping the design ready to add more builds later.

- 7.22g `steam.inf`: `ClientVersion=3756`, `ServerVersion=3756`,
  `SourceRevision=5339971`, `VersionDate=Sep 06 2019`, `appID=570`.

---

## 2. Architecture

```
dota2.exe ── steam_api shim ──HTTP──▶ D2ST.Api  (ASP.NET Core Minimal API)
                                        │
   ┌────────────────────────────────────┼───────────────────────────────┐
   │ D2ST.Steam  (auth/session/social)   │ D2ST.GameCoordinator (GC 570)  │
   │ D2ST.Persistence (EF Core + SQLite) │   GcRouter + IGcMessageHandler │
   └────────────────────────────────────┴───────────────────────────────┘
                     D2ST.Protocol (protobuf-net contracts, version profiles, codec)
                     D2ST.Core     (domain models)
```

Project responsibilities and dependency direction (leaf → host):

| Project | Responsibility | References |
| --- | --- | --- |
| `D2ST.Core` | Domain models: `SteamAccount`, `SteamSession`, `UserProfile`, `UserPresence`, `SteamEvent`, `GcMessage`. | — |
| `D2ST.Protocol` | protobuf-net GC contracts (generated), message ids, `GcConnectionStatus`, `IGcProtoCodec`, `VersionProfile(s)`. | protobuf-net |
| `D2ST.Persistence` | Accounts, friendships, friend requests, cloud files, stats/achievements, leaderboards (+entries), workshop items/subscriptions, `D2stDbContext`, DI ext. | Core, EF Core Sqlite |
| `D2ST.Steam` | Auth (PBKDF2), session store, presence tracker, event stream, user directory, friend service, lobby service, P2P relay, game invites, auth tickets, game server registry, cloud storage, stats, leaderboards, workshop. | Core, Persistence |
| `D2ST.GameCoordinator` | `GcContext`, `IGcMessageHandler`, `GcRouter`, `GameCoordinatorService`, `WelcomeBuilder`, `SharedObjects/` (SO cache store + service), `Messaging/` (push queue), `Parties/`, `Lobbies/`, handlers (`ClientHello`, `Ping`, `SoCacheSubscriptionRefresh`, `GetProfileCard`, …), `Diagnostics/` (unhandled-message dump), DI ext. | Core, Protocol |
| `D2ST.Api` | Minimal API host, endpoint modules, DTOs, DI wiring, DB bootstrap. | all of the above |

Design rules being followed:
- **Shared protobuf** across builds (GC message ids and field numbers are stable
  between 2019 and today); only version-divergent handshake **behaviour/constants**
  live in `VersionProfile`.
- A handler replies to its caller by returning messages; anything addressed to
  **another** player goes through `IGcMessageQueue` and is delivered on that
  player's next poll.
- Handlers are small, one per message type, registered via DI and dispatched by
  `GcRouter`. Unknown messages are logged (not fatal) — that log is how you
  discover what a build actually needs.
- **The session is the identity.** Endpoints resolve the caller from its bearer
  token; ids in a request body are never trusted for authorization.

---

## 3. What is implemented (done)

1. **Login** — `POST /api/auth/login` `{Username,Password}`. Registers unknown
   users on first login; passwords stored as salted PBKDF2 (never plaintext).
2. **Shim logon** — `POST /api/auth/steam/session` returns
   `{AccessToken, RefreshToken, User}`. Records persona name, client instance and
   process role (`client` / `dedicated`).
3. **GC handshake** — `POST /api/gamecoordinator/exchange` with a `GCClientHello`
   (msgType `4006`) returns the real 3-packet sequence:
   - `4009` ConnectionStatus = `NoSessionInLogonQueue`
   - `4004` ClientWelcome (`version`, `game_data`, SO caches), `TargetJobId` echoes `SourceJobId`
   - `4009` ConnectionStatus = `HaveSession`
4. **SO caches in the welcome** — `WelcomeBuilder` emits the game cache
   (`CSODOTAGameAccountClient` type `2002`, Dota Plus `2012` gated by the version
   profile) and the econ cache (types `1`/`7`), `SOCACHE_FILE_VERSION=20`.
5. **Version profiles** — `VersionProfiles.Resolve(clientVersion)` returns
   `V722g` for `clientVersion in [1, 3756]`, else `Modern`.
6. **Push channel** — a GC message addressed to a live client is published as a
   `gc_message` event and reaches it through the event pump it long-polls;
   `POST /api/gamecoordinator/poll` still drains the queue for a dedicated
   server or a player with no client session. See §3.20.
7. **Full protobuf contract set** — generated into
   `src/D2ST.Protocol/Generated/` from Valve's `.proto`, regenerated by
   `tools/regenerate-protos.sh` (pinned to the 7.22g-era commit).
8. **Sessions** — bearer tokens issued at logon and validated on every
   authenticated endpoint; sessions expire when unused and refresh on use.
9. **Steam identity surface (stage 1)** — users, friends, presence, avatars and
   the pushed event stream the shim's event pump consumes. See §5.
10. **Persistence** — EF Core + SQLite; schema created at startup via
    `EnsureCreated()` (see caveat in §6).
11. **Lobbies + P2P (stage 2)** — matchmaking lobbies (create/query/join/leave,
    lobby & member data, settings, game server, chat, invites), game invites and
    the P2P relay, plus every `lobby_*`, `game_invite` and `p2p_packet` event.
    Lobbies live in memory only: they are volatile by definition and disappear
    with their last member (the presence sweep also evicts crashed clients).
12. **Tickets, game servers, storage, stats (stage 3)** — session/encrypted app
    tickets, the game server directory, Steam Cloud, stats & achievements,
    leaderboards and the workshop catalogue. See §5.
13. **First GC handlers (stage 4a)** — `PingHandler` (3001→3002, the keepalive
    whose silence makes the client drop its GC session),
    `SoCacheSubscriptionRefreshHandler` (ESOMsg 28 → replays the same caches the
    welcome published, refusing a request for another owner) and
    `GetProfileCardHandler` (7534→7535, a bare `CMsgDOTAProfileCard`).
14. **Handlers driven by a real 7.22g capture (stage 4c)** — every message the
    live client sent without a handler now has one: matchmaking stats
    (7197→7198), store sales data (2536→2537), weekend tourney schedule
    (7464→7465), my team info (8137→8136), legacy guild data (7226→7227),
    emoticon data (7503→7504) and event points (7387→7388). All of them answer
    "nothing here" (empty population, no divisions, no teams, no guild invite,
    no emoticons, zero points) so the client's jobs complete instead of hanging.
15. **SO cache + push foundation (stage 4d)** — Shared Objects are no longer
    rebuilt inline for every welcome. `SoCacheStore` holds the live caches keyed
    by owner (`CMsgSOIDOwner`) and service (0 game, 1 econ), `SoCacheService`
    writes them and publishes the deltas the client actually reacts to
    (`SOCreate` 21 / `SOUpdate` 22 / `SODestroy` 23 / `SOCacheSubscribed` 24 /
    `SOCacheUnsubscribed` 25), and `IGcMessageQueue` delivers a message to a
    player who did not ask for it (the same queue `/poll` drains). Cache
    versions increment per mutation, like the reference GC. `WelcomeBuilder`
    now seeds the account/econ caches once (`SeedIfAbsent`, so a reconnect
    publishes no phantom update) and subscribes the caller to them, and the
    subscription refresh replays them from the store — including a shared cache
    (party, lobby) the caller is subscribed to, and still nothing for an owner
    it has no business reading.
16. **Econ / inventory (stage 4e)** — items are Shared Objects on the econ cache
    (service 1, type 1), not a table of their own: `EconInventory` writes them
    through `SoCacheService`, so storing an item and publishing the delta the
    armory redraws on are the same operation. Handled: equip (2569→2570, with
    the reply's `so_cache_version_id` taken from the cache the equip produced),
    set style (2577→2578), unlock style (2571→2572), set positions (1077, no
    reply — the client re-reads the cache), use item (1025→1026), unlock crate
    (2574→2575), unpack bundle (2576→2567), store purchase init/cancel
    (2510→2511, 2506→2507), redeem item (7518→7519) and purchase with event
    points (8248→8249). Equipping a hero slot unequips whatever else held it,
    like the real GC. There is no item catalogue, drop table or currency, so
    everything that would create or sell an item is refused rather than faked:
    items enter only through `POST /api/gamecoordinator/econ/grant`, whose item
    id is derived from account + def index (granting twice updates one item).
17. **Party (stage 4f)** — `PartyService` keeps parties as Shared Objects
    (`CSODOTAParty`, type 2003) on a cache owned by the *party* (owner type 2)
    rather than by any member, so one write reaches every member as a single
    delta and the object itself is the state (membership, leader, per-member
    ping data, coach flag, ready check). An invite is a `CSODOTAPartyInvite`
    (type 2006) alone on a cache owned by the invite (owner type 4): publishing
    it to its target is a subscribe and revoking it an unsubscribe, so a
    declined, superseded or expired invite leaves nothing behind. Handled:
    invite (4501→4502), invite response (4503), leave (4505), kick (4504),
    cancel invites (7589), set leader (7588), set coach (7343), ping data
    (8068) and ready check (8262→8263, 8264). Inviting creates the party on the
    spot, because the client shows one as soon as it invites somebody; five
    members is the cap; leaving or kicking disbands a two-player party (what the
    client draws for "no party") and otherwise hands the party to the next
    member; a reconnecting client gets its party and pending invites back
    through `IGcWelcomeContributor`, since the welcome cannot find caches the
    player does not own. Whether an invite target is reachable comes from the
    session table via `IGcPlayerDirectory`; persona names are only those the GC
    has seen, as it has no directory of its own. `GET
    /api/gamecoordinator/party` reads a party without a Dota client.
18. **Practice lobbies (stage 4g, first half)** — `LobbyService` keeps lobbies
    as Shared Objects (`CSODOTALobby`, type 2004) on a cache owned by the
    *lobby* (owner type 3), the shape parties already use: membership, team
    slots, settings and the launch state are fields of the object, so one write
    is one delta for every member. Handled: create (7038→7055), join
    (7044→7113), leave (7040), set details (7046), set team slot (7047), kick
    (7081), kick from team (8047), launch (7041) and list (7042→7043); 7055 and
    7113 carry the same `CMsgPracticeLobbyJoinResponse` body and only the id
    tells the client which request answered. Joining fills Radiant then Dire
    (five slots each) and drops the rest in the player pool; a slot somebody
    holds is refused rather than shared; the pass key, an unknown lobby and a
    lobby whose game already started are refused with the result the client
    renders. Only the host may change the settings, kick or launch, and its
    departure closes the lobby (a practice lobby cannot be handed over), while
    another member leaving only shrinks it.
    **Launching is a real flow now:** 7041 is answered with a `CMsgGenericResult`
    (the job the host's client waits on before it starts its local game
    server); the lobby moves to `SERVERSETUP` with an empty connect string; the
    game server's hello (4007) gets its welcome (4005) — without it the listen
    server never finishes its GC connection, which is what used to take the
    whole client down on "Start"; its address (4508/4511) installs the connect
    string the members dial (`127.0.0.1:27015` for a local listen server);
    4506 moves the lobby to `RUN` with a match id and a start time; 7034
    mirrors connected players, heroes and leaver states onto the object; and
    7088 aborts a launch whose player failed to load, back to `UI`. The
    listen-server (region 0) path works without any dedicated server; a
    non-zero region still has no dedicated-server launcher.
    A reconnecting client gets the lobby back through `IGcWelcomeContributor`,
    and `GET /api/gamecoordinator/lobby` reads one without a Dota client. Still
    to come in the second half: lobby invites (`CSODOTALobbyInvite`), lobby
    chat, spectators/broadcast channels and the party↔lobby link.
19. **Capture pipeline (stage 4b)** — a rolling file log next to the API and a
    JSON Lines dump of every GC message without a handler (id, resolved enum
    name, job ids, payload in base64 and hex), plus `tools/capture-gc-logs.ps1`
    to run the server and zip the result. See §4.
20. **Push channel (the shim's real one)** — a GC message the server sends to a
    player who did not ask for it is published as a `gc_message` event
    (`MessageType`, `PayloadBase64`, `TargetJobId`, `Protobuf`), because that is
    the only channel a Dota client drains: the shim's event pump long-polls
    `/api/events` and replays the message into the game, while
    `SteamGameCoordinator.TryPollServerMessages` returns early unless the process
    is a logged-on dedicated server. Everything stage 4d..4g pushed — the
    `SOCacheSubscribed` for a lobby the client just created, the `SOUpdate` its
    members redraw on, the `SOCacheUnsubscribed` that ends a lobby — went to the
    poll queue nobody read, so the client saw a create it could not draw and a
    lobby it could not leave. `EventStreamGcMessageQueue` picks the channel per
    recipient: the event stream for a live client session, the queue (drained by
    `/api/gamecoordinator/poll` and, from now on, by that account's next
    exchange) for a dedicated server or a player whose client is gone.

21. **Chat (stage 4h)** — `ChatService` keeps the chat channels. A channel is
    *not* a Shared Object: the client holds no cache of one and reacts only to
    what the GC addresses to it, so everything here is pushed through
    `IGcMessageQueue` (the event stream, for a live client) and nothing is
    published as a delta. Which channels exist is a server decision
    (`GameCoordinator:Chat`, see §4): the configured ones are created at
    startup, are listed while empty and outlive their last member, and the ones
    marked `AutoJoin` are entered on the client's behalf as soon as its
    `GCClientHello` is answered — pushed with `gc_initiated_join`, through the
    new `IGcLogonListener` hook, because the welcome can only carry caches. A
    channel a player opened disappears with its last member, and
    `AllowCustomChannels: false` stops players from opening any. Handled: join
    (7009→7010, whose reply carries the member list the channel is drawn from),
    leave (7272), chat message (7273, not answered but broadcast to the other
    members — the client draws its own line as it sends it, so echoing it back
    showed it twice; `EchoOwnMessages: true` restores the echo for a client
    that needs the server's copy — with the author, channel user id and
    timestamp stamped by the server and the text cut at `MaxMessageLength`),
    channel list (7060→7061, only the
    channels anybody may walk into: a party, lobby, team or private channel
    belongs to its group and the client opens it itself), user list (7403→7404) and member count
    (8048→8049); the members already in a channel learn about a join and a
    leave through `CMsgDOTAOtherJoinedChatChannel` (7013) and
    `CMsgDOTAOtherLeftChatChannel` (7014). A private chat is the same channel
    with a membership of its own: opening one makes its creator an admin, only
    an invited account may enter it (`PRIVATE_CHAT_NO_PERMISSION` otherwise),
    and invite (8084), kick (8088), promote (8089) and demote (8090) all answer
    8091 while info answers 8092→8093 — an admin cannot be kicked and the last
    one cannot be demoted. Nothing here is persisted: channels and their
    membership are in memory, like lobbies. `GET
    /api/gamecoordinator/chat/channels` reads the channels without a Dota
    client.

### Verified this session
- `dotnet build D2STServer.sln -c Release` → clean (0 warnings; warnings-as-errors on).
- Stage 4d over HTTP against a running server: `4006` still answers
  `4009/4004/4009` with a 118-byte welcome; a subscription refresh for the
  caller's own soid returns the two `SOCacheSubscribed` caches (54 and 48 bytes)
  and one for another soid returns nothing; a second `4006` produces a
  byte-identical welcome and leaves the poll queue empty (the seed does not
  masquerade as an update).
- Stage 4f over HTTP with three logged-on players (`tools/verify-party.py`, 27
  checks): 4501 answers 4502 naming the new group, subscribes the inviter to the
  party cache (owner type 2) and the invitee to the invite cache (owner type 4)
  carrying the sender's name; accepting unsubscribes the invite and subscribes
  the party while the other member gets `SOUpdate` 22; a reconnect's welcome
  carries the party cache; a ready check answers `kSuccess` then
  `kAlreadyInProgress`, and the acknowledgement reaches the initiator; a third
  member joins, the leader leaves and the party survives with the next member as
  leader while the leaver is unsubscribed; kicking the last other member
  disbands the party and unsubscribes both; a non-leader kick and a self-invite
  change nothing and publish nothing.
- Stage 4g over HTTP with three logged-on players (`tools/verify-lobby.py`, 34
  checks): 7038 answers 7055 with `SUCCESS`, seats the host on Radiant slot 1
  and subscribes it to the lobby cache (owner type 3); 7042 lists the lobby with
  its name, host and pass key flag; a wrong pass key answers 3, an unknown lobby
  2 and a launched one 1, while a good join answers 0, subscribes the joiner and
  pushes `SOUpdate` 22 to the host; a taken team slot is refused and publishes
  nothing; only the host renames, kicks and launches; a kick from team leaves
  the player in the pool and a kick unsubscribes it; a reconnect's welcome
  carries the lobby cache; the host leaving closes the lobby and unsubscribes
  everybody, a member leaving does not.
- Stage 4f still passes unchanged after 4g (`tools/verify-party.py`, 27/27).
- The push channel fix over HTTP (`tools/verify-lobby.py`, now 35 checks, and
  `tools/verify-party.py`, 27): both harnesses read the pushes back from
  `/api/events` instead of the poll queue and pass unchanged, and creating a
  lobby leaves the poll queue of the (online) host empty while its
  `SOCacheSubscribed` shows up as a `gc_message` event. Both harnesses need a
  freshly started server: they log on fixed accounts and a party or lobby left
  over from an earlier run changes what the invites publish.
- Stage 4h over HTTP with two logged-on players (`tools/verify-chat.py`, 37
  checks, on a freshly started server): the hello pushes one 7010 for the
  auto-join channel, marked `gc_initiated_join` and carrying its welcome
  message; 7060 lists exactly the three configured channels with the members of
  each and not a party channel a client opened; a join answers `JOIN_SUCCESS` with the member list and pushes 7013 to
  everybody already there and to nobody else; a chat line is not answered but
  reaches the other member with the author and timestamp the server stamped and
  is not sent back to its own sender, while a line to a channel that does not exist publishes
  nothing; 7403 and 8048 report both members; leaving pushes 7014, kills a
  channel a player opened and leaves a configured one alive and listed; a
  private chat refuses an uninvited player, refuses an invite from a non-admin,
  lets the invited one in, reports both members and its creator, refuses to
  demote its last admin or kick an admin, and locks a kicked member out again;
  the HTTP view shows the configured channels with their members.
- Stages 4f and 4g still pass unchanged after 4h (`tools/verify-party.py` 27,
  `tools/verify-lobby.py` 35). The party harness needed one change of its own:
  a batch pushed to a player is no longer only Shared Objects, since logging on
  is now announced to the default chat channel the players share.
- Stage 4g launch over HTTP (`tools/verify-lobby.py`, now 45 checks): 7041
  answers 2579 with success only for the host (a member gets failure and no
  state change); the lobby moves to `SERVERSETUP` with no server; 4508/4511
  install the connect string (`127.0.0.1:27015`) while still in `SERVERSETUP`;
  4506 moves it to `RUN` with the lobby id as the match id and a start time,
  and every step reaches the members as an `SOUpdate`; 7034 advances the game
  state; 7088 aborts the launch back to `UI`. Party (27/27) and chat (37/37)
  still pass unchanged on a freshly started server.
- Packed-encoding caveat: `CMsgDOTAPlayerFailedToConnect`'s repeated `fixed64`
  members are generated as packed arrays by protobuf-net, and the harness
  encodes them packed. The 2019 proto declares them unpacked, so if a real
  7.22g capture shows unpacked `failed_loaders`/`abandoned_loaders`, flip those
  members to `IsPacked = false` in the generated contract.
- Stage 4e over HTTP: a grant pushes `SOCreate` 21 and shows up in
  `/econ/items`; equipping publishes `SOUpdate` 22 and the 2570 reply carries
  the new cache version; equipping a second item onto the same hero slot
  publishes two 22s (the displaced item shrinks from 66 to 59 bytes); style and
  positions update the stored object; every reply above was produced with the
  expected result byte, owned vs unowned included (`kSetStyleSucceeded` on an
  owned item, `kSetStyleFailed` otherwise), and the poll queue ends empty.
- A real Dota 2 7.22g client reached the GC through the shim: handshake and ping
  worked, and `unhandled-gc.jsonl` listed exactly the seven messages the stage
  4c handlers now answer.
- The capture pipeline was exercised against a **running** server over HTTP:
  handled traffic (4006 → 4009/4004/4009, 3001 → 3002) writes no dump line;
  7454 dumps exactly one line whose payload, `MessageName`, `ClientVersion` and
  job id match what was sent; 40 concurrent unhandled exchanges produced 40
  intact lines; a 70 000-byte body was truncated to 65 536 with
  `BodyTruncated: true`; an unwritable dump path logged once and kept serving;
  restarts append instead of truncating; unauthenticated calls 401 and non-570
  app ids write nothing. One bug was found and fixed there: the dump used to
  start with a UTF-8 BOM, which made `jq`/`json.loads` reject its first line.
- `Logging:File:LogLevel:D2ST = Debug` is a no-op today — there is no
  `LogDebug` call in `src/`; per-sink filtering itself works.
- Not verified: anything involving the real Dota 2 7.22g client or the compiled
  `steam_api` shim (both need Windows).

---

## 4. Build / run / test

Requires the **.NET 10 SDK**.

```bash
dotnet restore D2STServer.sln
dotnet build   D2STServer.sln -c Release
# run the API (avoid launchSettings overriding the port):
dotnet run --project src/D2ST.Api --no-launch-profile --urls "http://127.0.0.1:5199"
```

Configuration (`appsettings.json` / env):

| Key | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings:D2st` | `Data Source=Data/d2st.db` | SQLite database. |
| `Steam:SessionTimeout` | `00:30:00` | Idle lifetime of a session token. |
| `Steam:PresenceTimeout` | `00:01:30` | How long after its last call a client still counts as online. |
| `Steam:PresenceSweepInterval` | `00:00:15` | How often offline transitions are detected and published. |
| `Logging:File:Enabled` | `true` | Write a daily rolling log next to the API (`Logs/d2st-yyyyMMdd.log`). |
| `Logging:File:Directory` / `:FilePrefix` / `:RetainedFileCount` | `Logs` / `d2st` / `14` | Where the file log lives and how many days are kept. |
| `Logging:File:LogLevel:*` | `D2ST` = `Debug` | Standard log filtering for the file sink only (the console keeps its own levels). |
| `GameCoordinator:Diagnostics:RecordUnhandledMessages` | `true` | Dump every GC message without a handler. |
| `GameCoordinator:Diagnostics:UnhandledMessageLogPath` | `Logs/unhandled-gc.jsonl` | JSON Lines dump used to drive stage 4. |
| `GameCoordinator:Diagnostics:MaxBodyBytes` | `65536` | Payload bytes kept per dumped message. |
| `GameCoordinator:Chat:Channels` | `D2MAX` (auto-join), `Trade`, `LFG` | The channels that exist from startup. Each entry takes `Name`, `Type` (a `DOTAChatChannelType_t` name, e.g. `DOTAChannelTypeRegional` / `DOTAChannelTypeCustom`), `MaxMembers`, `WelcomeMessage` and `AutoJoin`. Configuring any replaces the built-in list entirely, so a channel is removed by leaving it out. |
| `GameCoordinator:Chat:DefaultMaxMembers` | `500` | Cap for a channel that sets none. |
| `GameCoordinator:Chat:MaxChannelsPerUser` | `10` | Channels one player may be in at once. |
| `GameCoordinator:Chat:MaxMessageLength` | `1024` | Characters kept from a chat line. |
| `GameCoordinator:Chat:EchoOwnMessages` | `false` | Whether a chat line is sent back to the player who wrote it. Off, because the 7.22g client already drew it. |
| `GameCoordinator:Chat:AllowCustomChannels` | `true` | Whether joining an unknown name opens it. With this off the only channels are the configured ones and private chats. |

### Capturing what a real client asks for

On the Windows box that runs Dota 2 7.22g:

```powershell
powershell -ExecutionPolicy Bypass -File tools/capture-gc-logs.ps1 -Urls "http://0.0.0.0:5199"
```

Play until the client misbehaves, press Ctrl+C, and send the zip the script
writes to `captures/`. It contains `d2st-yyyyMMdd.log` (including every
`Unhandled GC message <id> (<enum name>) ...` line) and `unhandled-gc.jsonl`,
one JSON object per unhandled message with `MessageType`, `MessageName`,
`AccountId`, `SteamId`, `ClientVersion`, job ids and the payload in both
`BodyBase64` and `BodyHex` — enough to decode the protobuf offline and write the
missing handler instead of guessing. Running the API by hand works too; the log
and the dump are written whether or not the script is used.

### Pointing the real client at this server

The `steam_api` shim is **not injected**: it is a drop-in replacement for the
Steam DLL that `dota2.exe` already loads. Read from the shim's source (not yet
compiled or run by this project):

1. Build `manuelalmaguersosa2-creator/steam_api` on Windows (Visual Studio, .NET Framework
   4.7.2). Run `DllExport.bat` once first — it configures the native export
   wrapper — then build **Release / x64**, producing `bin\Release\steam_api.dll`.
2. In `...\dota 2 beta\game\bin\win64\`, rename `steam_api64.dll` to
   `steam_api64.dll.bak` and drop the built DLL in as `steam_api64.dll`.
3. Start the game once. The shim writes `<dota2.exe dir>\D2MAX\steam_api.ini`
   (the folder was called `SKYNET` before the shim stabilization PR; an existing
   one is renamed automatically);
   set at least:

   ```ini
   [Game Settings]
   AppId = 570

   [Network Settings]
   UseServerApi = true
   ServerUrl = http://127.0.0.1:5199/   ; or the LAN address of the server
   SecureNetworking = false             ; SDR certs are a modern-build path

   [Log Settings]
   File = true
   Console = true
   ```

4. The shim then logs to `D2MAX\steam_api.<pid>.log`, which is where the
   `Not found Interface for ...` lines used by roadmap item 10 appear. Collect
   it together with the server-side capture zip.

### Shim problems and their fix (stage 0 of the shim redesign)

Three defects found while diagnosing the first real 7.22g runs. None of them is
a server bug; they are written down because they make captures unreadable. All
three are fixed in `manuelalmaguersosa2-creator/steam_api` PR #1 ("Fix log truncation,
non-atomic ini writes and hung shutdown"), **not yet validated with the real
client** — it still needs a DllExport build on Windows.

**`steam_api.log` was truncated, and stopping at a line proved nothing.** The
run that produced the first capture ended its log at `Creating SteamUGC`, yet
the same process kept talking to the GC for another five minutes: the shim was
past interface creation and only its logging had stopped. Three causes in
`Helpers/Log.cs`, all fixed: `Initialize()` called `Clean()` (every process
wiped the shared log), `FlushBuffered()` used `File.AppendAllLines` without
`FileShare` (a second process holding the file made every later write throw
into a swallowed exception), and `AppEnd` dropped any line equal to the
previous one. Now each process writes `steam_api.<pid>.log` through a shared
append handle, repeats become `(previous line repeated xN)`, and lines carry a
timestamp.

**`steam_api.ini` lost keys on its own.** `Settings.Load()` completes missing
defaults and saves, and `INIParser.Save()` used `File.WriteAllText`, which
truncates in place: a second process reading at that moment parsed a partial
file and wrote back only what it had seen (that is how `[Log Settings]` ended up
empty, silently turning logging off). The write is atomic now (`.tmp` +
`File.Replace`) and the whole load-complete-save cycle runs under the machine
mutex `Global\D2MAX_steam_api_ini`. The modal `MessageBox` on the config error
path — a dialog during the game's DLL load — is now just a log line.

**`dota2.exe` survived closing the game and blocked the next launch** (the next
start stays in the background because Source 2 refuses a second instance). Every
worker is `IsBackground = true`, so nothing should hold the process open; the
problem was that no loop was told to stop, so a worker parked in
`HttpWebRequest.GetResponse()` was torn down mid-flight while the CLR shut down,
and `SteamAPI_Shutdown` blocked the game for up to 3.5 s in `Join`. The fix adds
a process-wide `Lifetime` cancellation source: `ShutdownServices()` cancels it,
aborts every in-flight `HttpWebRequest`, then joins each worker for at most
250 ms; the long-poll loops (`EventPump`, presence, P2P) check it; `GoOffline`
waits 400 ms instead of 1500; and `AppDomain.ProcessExit` runs the same path for
games that never call `SteamAPI_Shutdown`. `Initialize()` also stopped leaving
`Initializing = true` forever after a failure.

How to confirm the shutdown fix on the Windows box: quit the game and check
whether `dota2.exe` is still in Task Manager. If it is gone, the fix works; if
it lingers, `procdump -ma dota2.exe` on the stuck process identifies the thread.

---

## 5. Key files (where to look / edit)

- Version selection: `src/D2ST.Protocol/Versioning/VersionProfiles.cs`.
- Inventory / items: `src/D2ST.GameCoordinator/Econ/EconInventory.cs`; the econ
  handlers are one file each under `src/D2ST.GameCoordinator/Handlers/`.
- Parties: `src/D2ST.GameCoordinator/Parties/PartyService.cs`, its handlers one
  file each under `Handlers/`, online lookup in `Players/IGcPlayerDirectory.cs`
  (implemented by `src/D2ST.Api/SessionGcPlayerDirectory.cs`), welcome hook in
  `IGcWelcomeContributor.cs`; HTTP check harness `tools/verify-party.py`.
- Practice lobbies: `src/D2ST.GameCoordinator/Lobbies/LobbyService.cs`, its
  handlers one file each under `Handlers/` (`PracticeLobby*Handler.cs`); HTTP
  check harness `tools/verify-lobby.py`. Not to be confused with the Steam
  matchmaking lobbies in `src/D2ST.Steam/Lobbies/`, which are a different thing
  the shim reaches over HTTP.
- Chat: `src/D2ST.GameCoordinator/Chat/ChatService.cs` and `GcChatOptions.cs`
  (the configured channels), its handlers one file each under `Handlers/`
  (`*ChatChannel*`, `Chat*`, `PrivateChat*`), logon hook in
  `IGcLogonListener.cs` (called by `ClientHelloHandler`); HTTP check harness
  `tools/verify-chat.py`.
- SO caches: `src/D2ST.GameCoordinator/SharedObjects/` (`SoCacheStore` holds
  them, `SoCacheService` mutates and publishes, `SoIdentifiers` has the owner /
  cache / object keys); push queue in `src/D2ST.GameCoordinator/Messaging/`, and
  the choice between the event stream and that queue in
  `src/D2ST.Api/EventStreamGcMessageQueue.cs`.
- Handshake logic: `src/D2ST.GameCoordinator/Handlers/ClientHelloHandler.cs`.
- Welcome + SO caches: `src/D2ST.GameCoordinator/WelcomeBuilder.cs`.
- GC message ids: `src/D2ST.Protocol/Dota/GcMsg.cs`; generated contracts in
  `src/D2ST.Protocol/Generated/`.
- Dispatch + unhandled logging: `src/D2ST.GameCoordinator/GcRouter.cs`.
- Unhandled-message dump: `src/D2ST.GameCoordinator/Diagnostics/`; message-id →
  enum name lookup in `src/D2ST.Protocol/Dota/GcMsgNames.cs`.
- File log: `src/D2ST.Api/Logging/`; capture helper `tools/capture-gc-logs.ps1`.
- HTTP host + DB bootstrap: `src/D2ST.Api/Program.cs`; endpoints in
  `src/D2ST.Api/Endpoints/`.
- Auth/sessions: `src/D2ST.Steam/SteamAuthService.cs`, `SessionStore.cs`.
- Social/presence/events: `src/D2ST.Steam/Social/`, `Presence/`, `Events/`.
- Lobbies: `src/D2ST.Steam/Lobbies/` (`LobbyService`, `LobbyQuery`), endpoints in
  `src/D2ST.Api/Endpoints/LobbyEndpoints.cs`.
- P2P relay and game invites: `src/D2ST.Steam/Networking/`, `Invites/`,
  endpoints in `src/D2ST.Api/Endpoints/NetworkEndpoints.cs`.
- API DTOs: `src/D2ST.Api/Contracts/ApiContracts.cs` (PascalCase, mirrors the
  shim's `Managers/APIClient.cs` DTOs).

### HTTP contract (current)

All endpoints except `/api/version`, `/api/auth/login` and
`/api/auth/steam/session` require `Authorization: Bearer <AccessToken>`.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/version` | Launcher compatibility check. |
| POST | `/api/auth/login` | Username/password login. |
| POST | `/api/auth/steam/session` | Shim logon → tokens + `ApiUser`. |
| GET | `/api/users/me` | Own profile. |
| GET | `/api/users` | Every known player, with the viewer's relationship. |
| GET | `/api/users/{steamId}` | One player as the viewer sees them. |
| PATCH | `/api/users/me/persona` | Rename; notifies friends. |
| GET | `/api/users/{steamId}/avatar` | PNG + `X-SKYNET-Avatar-SteamId` / `-Default` headers. |
| PUT | `/api/users/me/avatar` | Upload avatar (`ContentBase64`). |
| GET | `/api/friends` | Friend list. |
| POST | `/api/friends/request` | Invite by `SteamId` or `Identifier`. |
| POST | `/api/friends/{steamId}/accept` | Accept an invitation. |
| POST | `/api/friends/{steamId}/remove` | Unfriend / decline / withdraw. |
| PUT | `/api/presence` | Set one rich-presence key. |
| PUT | `/api/presence/game-server` | Advertise the server being played on. |
| POST | `/api/presence/offline` | Explicit logoff. |
| GET | `/api/events?since=&waitMs=` | Long-poll pushed events. |
| POST | `/api/lobbies/query` | Search public lobbies (string/numerical/near filters). |
| POST | `/api/lobbies` | Create a lobby. |
| GET | `/api/lobbies/{lobbyId}` | Read one lobby. |
| POST | `/api/lobbies/{lobbyId}/join` | Join (idempotent for an existing member). |
| POST | `/api/lobbies/{lobbyId}/leave` | Leave; the owner role is handed over. |
| POST | `/api/lobbies/{lobbyId}/invites` | Invite a player to the lobby. |
| PUT | `/api/lobbies/{lobbyId}/data` | Owner-only lobby data write. |
| POST | `/api/lobbies/{lobbyId}/data/delete` | Owner-only lobby data delete. |
| PUT | `/api/lobbies/{lobbyId}/member-data` | Caller's own member data. |
| PUT | `/api/lobbies/{lobbyId}/gameserver` | Advertise the lobby's game server. |
| PUT | `/api/lobbies/{lobbyId}/settings` | Joinable / type / owner / max members. |
| POST | `/api/lobbies/{lobbyId}/chat` | Lobby chat message (base64). |
| POST | `/api/game-invites` | "Join my game" invite with a connect string. |
| POST | `/api/network/p2p/send` | Relay one P2P datagram. |
| POST | `/api/network/p2p/send-batch` | Relay a batch of P2P datagrams. |
| POST | `/api/auth/tickets/session` | Mint a session ticket for the caller. |
| POST | `/api/auth/tickets/encrypted` | Encrypted app ticket (user data echoed back). |
| POST | `/api/auth/tickets/validate` | Validate a ticket a peer presented. |
| POST | `/api/auth/tickets/end-session` | Drop the caller's tickets. |
| POST | `/api/auth/tickets/cancel` | Cancel one ticket handle. |
| POST | `/api/gameservers/register`, `/logon` | Register a server, get its identity + public IP. |
| PUT | `/api/gameservers/state` | Update the advertised state. |
| POST | `/api/gameservers/heartbeat` | Keep the registration alive. |
| DELETE | `/api/gameservers/{steamId}` | Log the server off. |
| GET | `/api/gameservers?appId=` | Server browser listing (live servers only). |
| GET | `/api/gameservers/public-ip` | Public IP as seen by the server. |
| POST | `/api/gameservers/users/connect` | Authenticate a connecting player's ticket. |
| PUT | `/api/gameservers/users/data` | Set a connected player's name/score. |
| POST | `/api/gameservers/users/disconnect` | Drop a player from the server. |
| GET/PUT | `/api/gameservers/stats/users/{steamId}` | Read/store a player's stats server-side. |
| GET/PUT | `/api/stats/me` | Own stats and achievements. |
| GET | `/api/stats/users/{steamId}` | Another player's stats. |
| GET/PUT | `/api/storage/files` | List / upload cloud files. |
| GET | `/api/storage/files/{fileName}` | Download one file (404 = no save yet). |
| POST | `/api/storage/files/delete`, `/share` | Delete or share a file. |
| GET | `/api/storage/quota` | Cloud quota (1 GiB per account). |
| POST | `/api/leaderboards` | Find or create a leaderboard by name. |
| GET | `/api/leaderboards/{id}` | Read a leaderboard. |
| POST | `/api/leaderboards/{id}/entries` | Ranked entries by range or by user list. |
| PUT | `/api/leaderboards/{id}/score` | Upload a score (keep-best or force). |
| GET | `/api/workshop/subscriptions` | The caller's subscriptions, with items. |
| GET/PUT | `/api/workshop/items/{publishedFileId}` | Read / publish item metadata. |
| POST | `/api/workshop/items/{id}/subscribe` | Subscribe. |
| DELETE | `/api/workshop/items/{id}/subscription` | Unsubscribe. |
| POST | `/api/gamecoordinator/exchange` | Client → GC message, returns replies. |
| POST | `/api/gamecoordinator/poll` | Drain server-pushed GC messages. |
| POST | `/api/gamecoordinator/econ/grant` | Put an item def in an account's inventory (there is no drop system). |
| GET | `/api/gamecoordinator/econ/items?steamId=` | Read an inventory as the econ cache holds it. |
| GET | `/api/gamecoordinator/party?steamId=` | Read a party as the GC holds it (404 when there is none). |
| GET | `/api/gamecoordinator/lobby?steamId=` | Read a practice lobby as the GC holds it (404 when there is none). |
| GET | `/api/gamecoordinator/chat/channels` | Read the chat channels and who is in them. |

Event types published so far: `persona_state_changed`,
`friend_presence_changed`, `friend_added`, `friend_removed`,
`friend_request_received`, `friend_request_sent`, `lobby_created`,
`lobby_updated`, `lobby_member_updated`, `lobby_joined`, `lobby_left`,
`lobby_removed`, `lobby_chat`, `lobby_game_created`, `lobby_invite`,
`game_invite`, `p2p_packet`, `gc_message`, `stats_updated`,
`achievement_unlocked`.

Lobby ids are chat-type Steam ids with the lobby instance flag set
(`LobbyIds`), because the client checks that shape before treating an id as a
lobby. Every `lobby_*` event carries the whole lobby snapshot, so a client never
has to read the lobby back after a change.

Stage 3 rules worth knowing:
- **Tickets** are `[steamId | appId | handle | random]` and are accepted only
  while their handle is in the issued set, so nothing outside this deployment
  can forge one. The encrypted app ticket is *not* encrypted (there is no Valve
  key to encrypt with); it is the user data behind the same identity header.
- **Game servers are in-memory** and owned by the session that registered them;
  a server missing from the browser has simply stopped heartbeating
  (`SteamOptions.PresenceTimeout`). Unidentified servers are handed a
  game-server-account-type Steam id.
- **Cloud files, stats, leaderboards and workshop rows are persisted** (new
  tables). Existing `d2st.db` files predate them: delete the database or add
  the tables by hand until stage 5 replaces `EnsureCreated()` with migrations.
- A client may only write its **own** stats (`/api/stats/me`); the
  `/api/gameservers/stats/users/{steamId}` route is what writes another
  player's, and it pushes `stats_updated` / `achievement_unlocked` to them.
- Workshop items may only be overwritten by their publisher (403 otherwise);
  ownership comes from the session, not the request body.

Presence rule (matches what the client expects): an **offline** player reports
`PersonaState=0` and zeroed `AppId`/`LobbyId`/game server/rich presence, because
the client renders any non-zero `AppId` as "currently playing".

---

## 6. What's missing / next steps (roadmap)

Ordered by dependency. Each stage is a separate branch + PR.

1. ~~Full GC protobuf contract set.~~ **Done** (`Generated/`, `tools/regenerate-protos.sh`).
2. ~~Reconcile HTTP contract with `steam_api`.~~ **Done** for logon, exchange and poll.
3. ~~SO caches / welcome payload.~~ **Done** (`WelcomeBuilder`).
4. ~~Sessions.~~ **Done** (bearer validation, expiry, presence).
5. ~~Steam identity surface.~~ **Done** — stage 1: users, friends, presence,
   avatars, events.
6. ~~Lobbies + P2P (stage 2).~~ **Done** — lobbies, invites and the P2P relay.
   Not covered yet: SDR certificates (`/api/networking/sdr/cert`), which the
   shim only asks for on modern builds.
7. ~~Tickets, game servers, storage, stats (stage 3).~~ **Done** —
   `/api/auth/tickets/*`, `/api/gameservers/*`, `/api/storage/*`, `/api/stats/*`,
   `/api/leaderboards/*`, `/api/workshop/*`. Not covered: Dota cosmetics /
   equipment, which the shim does not call over HTTP at all — they belong to the
   GC econ handlers in stage 4.
8. **More GC handlers (stage 4).** Done so far (stage 4a): ping, SO cache
   subscription refresh, profile card; stage 4b added the capture pipeline (file
   log + `unhandled-gc.jsonl`) and the shim wiring notes in §4; stage 4c added
   the seven handlers the first real 7.22g capture asked for (see §3.14);
   stage 4d added the SO cache + push foundation (see §3.15) that party, lobby,
   chat and econ all build on; stage 4e added econ/inventory (see §3.16);
   stage 4f added party (see §3.17); the first half of stage 4g added practice
   lobbies (see §3.18), and the launch flow that hands a launched lobby to its
   game server (still §3.18).
   stage 4h added chat (see §3.21).
   Planned order for the rest, one PR each: **4g second half** (lobby invites,
   lobby chat, spectators/broadcast channels and the party↔lobby link; the
   listen-server launch path is done, a dedicated-server launcher for
   non-zero regions is still missing),
   **4i** matchmaking (`StartFindingMatch` and the
   queue state), which only makes sense once party and lobby exist. Reference
   for each: the module of the same name under `SKY_server/GC/570/modules/`.
   Still missing beyond that, and worth driving
   **empirically** from the dump described in §4 while running the real client:
   whatever 7.22g asks for that this list does not predict.
9. **EF Core migrations (stage 5).** Replace `Database.EnsureCreated()` in
   `Program.cs` with real migrations before the schema needs to evolve with data.
10. **steam_api interface coverage for 7.22g.** With the real client (installed
    as described in §4), capture `Not found Interface for ...` logs and ensure the shim exposes the interface
    versions 7.22g requests (SteamClient017+, SteamUser019+, etc.).
11. **Shim redesign (`steam_api_new`).** Stage 0 (the stabilization PR above) is
    done. The rest is a re-skeleton, not a rewrite: the ~40 000 lines of
    `Steamworks/Implementation` and the callback tables are ported as they are,
    while the shell becomes layered (`Contracts` shared with this server,
    `Core` testable without Windows, `Transport`, `Interop`, optional plugins for
    overlay/HTML/music), with lazy interface creation driven by a declarative
    version table, one source of configuration defaults, cooperative shutdown
    everywhere, a self-test host that exercises the shim against D2STServer
    without Dota, and CI.
12. **D2MaxLauncher wiring.** Point the launcher at this server; reuse the
    existing `/api/version` compatibility check.
13. **Account page served by the server (avatar and persona).** The server
    already stores and serves an avatar (`PUT /api/users/me/avatar` with the PNG
    base64-encoded, `GET /api/users/{steamId}/avatar`, and a
    `persona_state_changed` published on change), but nothing lets a player
    reach it: Dota 2 has no UI for the avatar — it reads it from Steam, whose
    community profile does not exist here — and neither does the launcher. So
    serve a small page from `D2ST.Api` (log in with the account credentials,
    show the current avatar and persona name, upload a PNG, rename), posting to
    the endpoints that already exist. Worth deciding while building it: what
    the page accepts (size cap, PNG only or convert, the 32/64/184 px sizes
    Steam keeps), whether it is reachable without a session cookie, and whether
    the launcher simply opens it instead of growing its own screen.

### How to validate 7.22g end-to-end (must be on Windows with the real client)
```
GCClientHello → ConnectionStatus(NoSessionInLogonQueue) → ClientWelcome → ConnectionStatus(HaveSession)
```
then verify profile, inventory/cosmetics, party, lobby, matchmaking. **This
cannot be validated on Linux / from code alone** — it needs the actual Dota
7.22g client + the compiled `steam_api` shim on Windows.

---

## 7. Constraints & conventions

- Branch per change; do **not** commit to `main` directly. Open PRs.
- No destructive git (`reset --hard`, `clean -fd`), no `git add .`, no `--no-verify`.
- Keep `TreatWarningsAsErrors` green.
- Don't claim 7.22g compatibility without a real-client test.
- Related repos for reference: `soulhuntermax/SKY_server` (old full server),
  `manuelalmaguersosa2-creator/steam_api` (the client shim actually in use; `soulhuntermax/steam_api`
  is its upstream), `gerardopromax1-del/D2MaxLauncher`.

---

## 8. Status summary

**Working:** clean modular solution, login + shim logon with validated sessions,
GC 3-packet handshake with SO caches and ClientVersion→profile selection, poll
queue, full generated protobuf set, the Steam identity surface (users, friends,
presence, avatars, event stream), SQLite persistence.
**Also working:** lobbies (query/create/join/leave, data, settings, chat,
invites, game server), game invites and the P2P relay with their events.
**Also working:** the diagnostics capture pipeline (file log +
`unhandled-gc.jsonl` + `tools/capture-gc-logs.ps1`), verified against a running
server, plus the stage 4c handlers derived from the first real-client capture,
and the stage 4d Shared Object store / push queue the rest of stage 4 sits on.
**Also working (in the shim, unvalidated with the real client):** per-process
logging, atomic `steam_api.ini` writes and cooperative shutdown.
**Also working:** econ/inventory — items as econ-cache Shared Objects with
equip/style/position deltas, and the econ replies 7.22g expects (stage 4e).
**Also working:** parties — party and invite Shared Objects with their ten GC
messages, ready check included, restored into the welcome after a reconnect
(stage 4f).
**Also working:** practice lobbies — the lobby Shared Object with create, join,
leave, settings, team slots, kick, launch and the browser listing, plus the
launch handoff: the game server is welcomed, its address installs the connect
string and the lobby runs with a match id (listen-server / LAN flow).
**Not done:** the rest of stage 4 (the second half of the lobby work — lobby
invites, lobby chat, spectators/broadcast channels, the party↔lobby link and
the dedicated-server launch path — plus matchmaking handlers and an item
catalogue behind the econ grant), EF migrations, the `steam_api_new` redesign, and
real-client validation. This is a
solid foundation, **not** a complete GC.
