using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Core.GameCoordinator;
using D2ST.Core.Steam;
using D2ST.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol;
using D2ST.Protocol.Dota;
using D2ST.Steam;

namespace D2ST.Api.Endpoints;

public static class GameCoordinatorEndpoints
{
    private static readonly GcExchangeResponse EmptyExchange = new(false, Array.Empty<GcMessageDto>());

    public static IEndpointRouteBuilder MapGameCoordinatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/gamecoordinator/exchange", (
            GcExchangeRequest request,
            HttpContext http,
            ISessionStore sessions,
            GameCoordinatorService gc,
            IGcProtoCodec codec) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (!IsDota(request.AppId))
            {
                return Results.Ok(EmptyExchange);
            }

            var context = ContextFor(session, request.SteamId, codec);
            var message = new GcMessage(
                request.MessageType,
                DecodeBody(request.BodyBase64),
                SourceJobId: JobId(request.SourceJobId));

            var responses = gc.Exchange(context, message);
            session.ClientVersion = context.ClientVersion;

            return Results.Ok(new GcExchangeResponse(
                gc.CanHandle(request.MessageType),
                responses.Select(ToDto).ToList()));
        });

        app.MapPost("/api/gamecoordinator/poll", (
            GcPollRequest request,
            HttpContext http,
            ISessionStore sessions,
            GameCoordinatorService gc) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (!IsDota(request.AppId))
            {
                return Results.Ok(EmptyExchange);
            }

            var responses = gc.Poll(AccountIdFor(session, request.SteamId));
            return Results.Ok(new GcExchangeResponse(true, responses.Select(ToDto).ToList()));
        });

        app.MapPost("/api/gamecoordinator/econ/grant", (
            GcGrantItemRequest request,
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var steamId = request.SteamId != 0 ? request.SteamId : session.Account.SteamId;
            var item = inventory.Grant(
                steamId,
                SteamAccount.AccountIdFromSteamId(steamId),
                request.DefIndex,
                request.Quantity);

            return Results.Ok(new GcGrantItemResponse(
                item.Id,
                item.DefIndex,
                item.Quantity,
                inventory.CacheVersion(steamId)));
        });

        app.MapGet("/api/gamecoordinator/econ/items", (
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory,
            ulong steamId = 0) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var owner = steamId != 0 ? steamId : session.Account.SteamId;
            var items = inventory.Items(owner)
                .Select(item => new GcInventoryItem(item.Id, item.DefIndex, item.Quantity, item.Style, item.Inventory))
                .ToList();

            return Results.Ok(new GcInventoryResponse(items, inventory.CacheVersion(owner)));
        });

        app.MapGet("/api/gamecoordinator/party", (
            HttpContext http,
            ISessionStore sessions,
            PartyService parties,
            ulong steamId = 0) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var party = parties.Find(steamId != 0 ? steamId : session.Account.SteamId);
            return party is null
                ? Results.NotFound()
                : Results.Ok(new GcPartyResponse(
                    party.PartyId,
                    party.LeaderId,
                    party.MemberIds ?? [],
                    party.Members.Select(member => member.IsCoach).ToList(),
                    party.ReadyCheck?.FinishTimestamp ?? 0));
        });

        app.MapGet("/api/gamecoordinator/lobby", (
            HttpContext http,
            ISessionStore sessions,
            LobbyService lobbies,
            ulong steamId = 0) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var lobby = lobbies.Find(steamId != 0 ? steamId : session.Account.SteamId);
            return lobby is null
                ? Results.NotFound()
                : Results.Ok(new GcLobbyResponse(
                    lobby.LobbyId,
                    lobby.LeaderId,
                    lobby.GameName,
                    lobby.GameMode,
                    lobby.ServerRegion,
                    lobby.state.ToString(),
                    !string.IsNullOrEmpty(lobby.PassKey),
                    lobby.Connect,
                    lobby.MatchId,
                    lobby.GameStartTime,
                    (uint)lobby.GameState,
                    lobby.Lan,
                    lobby.ServerId,
                    lobby.AllMembers
                        .Select(member => new GcLobbyMember(
                            member.Id,
                            lobbies.MemberName(lobby.LobbyId, member.Id),
                            (int)member.Team,
                            member.Slot))
                        .ToList()));
        });

        app.MapGet("/api/gamecoordinator/chat/channels", (
            HttpContext http,
            ISessionStore sessions,
            ChatService chat) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(chat.Snapshot()
                .Select(channel => new GcChatChannelResponse(
                    channel.Id,
                    channel.Name,
                    channel.Type.ToString(),
                    channel.MaxMembers,
                    channel.Configured,
                    channel.Members
                        .Select(member => new GcChatMember(member.SteamId, member.Name))
                        .ToList()))
                .ToList());
        });

        return app;
    }

    private static GcContext ContextFor(SteamSession session, ulong steamId, IGcProtoCodec codec) => new()
    {
        AccountId = AccountIdFor(session, steamId),
        SteamId = steamId != 0 ? steamId : session.Account.SteamId,
        ClientVersion = session.ClientVersion,
        PersonaName = session.PersonaName ?? string.Empty,
        Codec = codec
    };

    // A game server logs on under its own Steam id while reusing the client's
    // session, so trust the id on the request when it carries one.
    private static uint AccountIdFor(SteamSession session, ulong steamId) =>
        steamId != 0 ? SteamAccount.AccountIdFromSteamId(steamId) : session.Account.AccountId;

    private static bool IsDota(uint appId) => appId is 0 or DotaApp.AppId;

    private static byte[] DecodeBody(string? bodyBase64) =>
        string.IsNullOrEmpty(bodyBase64) ? Array.Empty<byte>() : Convert.FromBase64String(bodyBase64);

    // The shim uses ulong.MaxValue as "no job id".
    private static ulong? JobId(ulong jobId) => jobId == ulong.MaxValue ? null : jobId;

    private static GcMessageDto ToDto(GcMessage message) => new(
        DotaApp.AppId,
        message.MessageType,
        Convert.ToBase64String(message.Body),
        message.TargetJobId,
        Protobuf: true);
}
