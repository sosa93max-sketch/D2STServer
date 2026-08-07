using System.Security.Cryptography;
using D2ST.Core.Events;
using D2ST.Core.GameServers;
using D2ST.Core.Leaderboards;
using D2ST.Core.Lobbies;
using D2ST.Core.Networking;
using D2ST.Core.Stats;
using D2ST.Core.Steam;
using D2ST.Core.Storage;
using D2ST.Core.Workshop;
using D2ST.Steam.Lobbies;

namespace D2ST.Api.Contracts;

/// <summary>Domain to wire conversions for the shim-facing endpoints.</summary>
public static class ApiMappings
{
    public static ApiUser ToApiUser(this UserProfile profile) => new(
        profile.AccountId,
        profile.SteamId,
        profile.PersonaName,
        profile.AppId,
        profile.LobbyId,
        profile.GameServerSteamId,
        profile.GameServerIp,
        profile.GameServerPort,
        profile.IsFriend,
        (int)profile.Relationship,
        profile.PersonaState,
        PlayerLevel: 0,
        profile.RichPresence);

    public static ApiEvent ToApiEvent(this SteamEvent steamEvent) => new(
        steamEvent.Type,
        steamEvent.SteamId,
        steamEvent.AccountId,
        steamEvent.PersonaName,
        steamEvent.AppId,
        steamEvent.LobbyId,
        steamEvent.GameServerSteamId,
        steamEvent.GameServerIp,
        steamEvent.GameServerPort,
        steamEvent.PersonaState,
        (int)steamEvent.ChangeFlags,
        (int)steamEvent.FriendRelationship,
        steamEvent.RequestId,
        steamEvent.GameName,
        steamEvent.Lobby?.ToApiLobby(),
        steamEvent.PayloadBase64,
        steamEvent.MessageType,
        steamEvent.TargetJobId,
        steamEvent.Protobuf,
        steamEvent.RemoteSteamId,
        steamEvent.Channel,
        steamEvent.Transport,
        steamEvent.VirtualPort,
        steamEvent.SourceConnectionId,
        steamEvent.TargetConnectionId,
        steamEvent.RichPresence,
        steamEvent.StatName,
        steamEvent.StatValue,
        steamEvent.AchievementName,
        steamEvent.AchievementEarned,
        steamEvent.AchievementProgress,
        steamEvent.AchievementMaxProgress);

    public static ApiLobby ToApiLobby(this Lobby lobby) => new(
        lobby.SteamId,
        lobby.AppId,
        lobby.OwnerSteamId,
        lobby.LobbyType,
        lobby.MaxMembers,
        lobby.Joinable,
        lobby.LobbyData,
        lobby.Members
            .Select(member => new ApiLobbyMember(
                member.SteamId,
                member.Data.Select(entry => new ApiLobbyMetaData(entry.Key, entry.Value)).ToList()))
            .ToList(),
        new ApiLobbyGameServer(lobby.GameServer.SteamId, lobby.GameServer.Ip, lobby.GameServer.Port));

    public static LobbyQuery ToLobbyQuery(this LobbyQueryRequest request)
    {
        var stringFilters = (request.StringFilters ?? Array.Empty<LobbyStringFilterRequest>())
            .Where(filter => !string.IsNullOrEmpty(filter.KeyToMatch))
            .Select(filter => new LobbyStringFilter(
                filter.KeyToMatch!,
                filter.ValueToMatch ?? string.Empty,
                (LobbyComparison)filter.ComparisonType))
            .ToList();

        var numericalFilters = (request.NumericalFilters ?? Array.Empty<LobbyNumericalFilterRequest>())
            .Where(filter => !string.IsNullOrEmpty(filter.KeyToMatch))
            .Select(filter => new LobbyNumericalFilter(
                filter.KeyToMatch!,
                filter.ValueToMatch,
                (LobbyComparison)filter.ComparisonType))
            .ToList();

        // A single filter may also arrive inline instead of in a list; whether
        // it is a string or a numerical one is told apart by which value is set.
        if (!string.IsNullOrEmpty(request.KeyToMatch))
        {
            if (request.StringValueToMatch is not null)
            {
                stringFilters.Add(new LobbyStringFilter(
                    request.KeyToMatch,
                    request.StringValueToMatch,
                    (LobbyComparison)request.ComparisonType));
            }
            else
            {
                numericalFilters.Add(new LobbyNumericalFilter(
                    request.KeyToMatch,
                    request.ValueToMatch,
                    (LobbyComparison)request.ComparisonType));
            }
        }

        return new LobbyQuery
        {
            AppId = request.AppId,
            ResultCount = request.ResultCount,
            SlotsAvailable = request.SlotsAvailable,
            StringFilters = stringFilters,
            NumericalFilters = numericalFilters,
            NearValueFilters = (request.NearValueFilters ?? Array.Empty<LobbyNearValueFilterRequest>())
                .Where(filter => !string.IsNullOrEmpty(filter.KeyToMatch))
                .Select(filter => new LobbyNearValueFilter(filter.KeyToMatch!, filter.ValueToBeCloseTo))
                .ToList()
        };
    }

    public static P2PPacket ToP2PPacket(this P2PPacketRequest request) => new(
        request.RemoteSteamId,
        request.BufferBase64 ?? string.Empty,
        request.SendType,
        request.Channel,
        string.IsNullOrWhiteSpace(request.Transport) ? P2PTransports.Legacy : request.Transport,
        request.VirtualPort,
        request.SourceConnectionId,
        request.TargetConnectionId);

    public static ApiGameServer ToApiGameServer(this GameServer server) => new(
        server.SteamId,
        server.AppId,
        server.Ip,
        server.Port,
        server.QueryPort,
        server.Flags,
        server.Secure,
        server.VersionString,
        server.Product,
        server.Description,
        server.ModDir,
        server.Dedicated,
        server.MaxPlayers,
        server.BotPlayers,
        server.ServerName,
        server.MapName,
        server.PasswordProtected,
        server.SpectatorPort,
        server.SpectatorServerName,
        server.GameTags,
        server.GameData,
        server.Region,
        server.LoggedOn,
        server.AdvertiseActive,
        server.KeyValues,
        server.Players
            .Select(player => new ApiGameServerPlayer(player.SteamId, player.Name, player.Score, player.TimePlayedSeconds))
            .ToList());

    public static GameServer ToGameServer(this ApiGameServer server) => new()
    {
        SteamId = server.SteamId,
        AppId = server.AppId,
        Ip = server.IP,
        Port = server.Port,
        QueryPort = server.QueryPort,
        Flags = server.Flags,
        Secure = server.Secure,
        VersionString = server.VersionString ?? string.Empty,
        Product = server.Product ?? string.Empty,
        Description = server.Description ?? string.Empty,
        ModDir = server.ModDir ?? string.Empty,
        Dedicated = server.Dedicated,
        MaxPlayers = server.MaxPlayers,
        BotPlayers = server.BotPlayers,
        ServerName = server.ServerName ?? string.Empty,
        MapName = server.MapName ?? string.Empty,
        PasswordProtected = server.PasswordProtected,
        SpectatorPort = server.SpectatorPort,
        SpectatorServerName = server.SpectatorServerName ?? string.Empty,
        GameTags = server.GameTags ?? string.Empty,
        GameData = server.GameData ?? string.Empty,
        Region = server.Region ?? string.Empty,
        LoggedOn = server.LoggedOn,
        AdvertiseActive = server.AdvertiseActive,
        KeyValues = server.KeyValues ?? new Dictionary<string, string>(StringComparer.Ordinal),
        Players = (server.Players ?? Array.Empty<ApiGameServerPlayer>())
            .Select(player => new GameServerPlayer(player.SteamId, player.Name ?? string.Empty, player.Score, player.TimePlayedSeconds))
            .ToList()
    };

    public static ApiStatsEnvelope ToApiStats(this UserStats stats) => new(
        stats.SteamId,
        stats.Stats.Select(stat => new ApiStat(stat.Name, stat.Data)).ToList(),
        stats.Achievements
            .Select(achievement => new ApiAchievement(
                achievement.Name,
                achievement.Earned,
                achievement.Date,
                achievement.Progress,
                achievement.MaxProgress))
            .ToList(),
        stats.CurrentPlayers);

    public static IReadOnlyList<StatValue> ToStatValues(this IReadOnlyList<ApiStat>? stats) =>
        (stats ?? Array.Empty<ApiStat>()).Select(stat => new StatValue(stat.Name, stat.Data)).ToList();

    public static IReadOnlyList<AchievementValue> ToAchievementValues(this IReadOnlyList<ApiAchievement>? achievements) =>
        (achievements ?? Array.Empty<ApiAchievement>())
            .Select(achievement => new AchievementValue(
                achievement.Name,
                achievement.Earned,
                achievement.Date,
                achievement.Progress,
                achievement.MaxProgress))
            .ToList();

    public static ApiRemoteStorageFile ToApiFile(this StorageFile file) => new(
        file.FileName,
        Convert.ToBase64String(file.Content),
        file.Content.Length,
        (uint)file.UpdatedAt.ToUnixTimeSeconds(),
        Convert.ToHexStringLower(SHA256.HashData(file.Content)),
        file.SyncPlatforms,
        file.Version);

    public static ApiRemoteStorageFileListItem ToApiFileListItem(this StorageFile file) => new(
        file.FileName,
        file.Content.Length,
        (uint)file.UpdatedAt.ToUnixTimeSeconds(),
        Convert.ToHexStringLower(SHA256.HashData(file.Content)),
        file.SyncPlatforms,
        file.Version);

    public static ApiLeaderboard ToApiLeaderboard(this Leaderboard leaderboard) => new(
        leaderboard.Id,
        leaderboard.AppId,
        leaderboard.Name,
        leaderboard.SortMethod,
        leaderboard.DisplayType,
        leaderboard.EntryCount);

    public static ApiLeaderboardEntries ToApiEntries(this LeaderboardEntries entries) => new(
        entries.Leaderboard.ToApiLeaderboard(),
        entries.Entries
            .Select(entry => new ApiLeaderboardEntry(
                entry.SteamId,
                entry.GlobalRank,
                entry.Score,
                entry.Details,
                entry.UgcHandle))
            .ToList());

    public static ApiWorkshopItem ToApiWorkshopItem(this WorkshopItem item) => new(
        item.PublishedFileId,
        item.CreatorAppId,
        item.ConsumerAppId,
        item.OwnerSteamId,
        item.FileType,
        item.Title,
        item.Description,
        item.Tags,
        item.FileName,
        item.Metadata,
        item.PreviewUrl,
        item.Visibility,
        item.Banned,
        item.AcceptedForUse,
        item.TimeCreated,
        item.TimeUpdated,
        item.FileSize,
        item.TotalFilesSize,
        item.VotesUp,
        item.VotesDown,
        item.Score);

    public static WorkshopItem ToWorkshopItem(this ApiWorkshopItem item, ulong publishedFileId, ulong ownerSteamId) => new()
    {
        PublishedFileId = publishedFileId,
        CreatorAppId = item.CreatorAppId,
        ConsumerAppId = item.ConsumerAppId,
        // The publisher is whoever is logged in: a client cannot hand a file to
        // another account by putting their id in the body.
        OwnerSteamId = ownerSteamId,
        FileType = item.FileType,
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        Tags = item.Tags ?? string.Empty,
        FileName = item.FileName ?? string.Empty,
        Metadata = item.Metadata ?? string.Empty,
        PreviewUrl = item.PreviewUrl ?? string.Empty,
        Visibility = item.Visibility,
        Banned = item.Banned,
        AcceptedForUse = item.AcceptedForUse,
        TimeCreated = item.TimeCreated,
        FileSize = item.FileSize,
        TotalFilesSize = item.TotalFilesSize,
        VotesUp = item.VotesUp,
        VotesDown = item.VotesDown,
        Score = item.Score
    };

    public static ApiWorkshopSubscription ToApiSubscription(this WorkshopSubscription subscription) => new(
        subscription.PublishedFileId,
        subscription.SubscribedAtUtc,
        subscription.DisabledLocally,
        subscription.Item?.ToApiWorkshopItem());
}
