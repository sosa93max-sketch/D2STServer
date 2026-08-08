using D2ST.Core.Accounts;
using D2ST.Core.GameCoordinator;
using D2ST.Core.Matches;
using D2ST.GameCoordinator.Ranks;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Profiles;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The game server reports the match ended (7004). The complete scoreboard is
/// persisted once, rank/profile projections are updated from that same record,
/// and the lobby moves to POSTGAME. Repeated sign-out packets only receive the
/// protocol response; they do not duplicate the match or rating.
/// </summary>
public sealed class GameMatchSignOutHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;
    private readonly IRankStore _ranks;
    private readonly IMatchStore _matches;
    private readonly SoCacheService _soCache;
    private readonly IGcMessageQueue _queue;

    public GameMatchSignOutHandler(
        LobbyService lobbies,
        IRankStore ranks,
        IMatchStore matches,
        SoCacheService soCache,
        IGcMessageQueue queue)
    {
        _lobbies = lobbies;
        _ranks = ranks;
        _matches = matches;
        _soCache = soCache;
        _queue = queue;
    }

    public uint MessageType => GcMsg.GameMatchSignOut;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var signOut = context.Codec.Decode<CMsgGameMatchSignOut>(request.Body);
        var lobby = _lobbies.FindByServer(context.SteamId);
        var matchId = signOut.MatchId != 0 ? signOut.MatchId : lobby?.MatchId ?? 0;
        var winningTeam = ResolveWinningTeam(signOut);
        var match = BuildMatchRecord(signOut, lobby, matchId, winningTeam);
        var result = _matches.Record(match);

        if (result.Created)
        {
            var results = match.Players
                .Where(player => IsPlayingTeam(player.Team))
                .Select(player => (player.AccountId, player.Won))
                .ToList();

            _ranks.ApplyMatchResult(results);
            UpdateAccountCaches(match.Players);

            if (lobby is not null)
            {
                _lobbies.CompleteMatch(context.SteamId, matchId, winningTeam, signOut.FirstBloodTime);

                var pushed = context.Codec.Encode(new CMsgGCToClientMatchSignedOut { MatchId = matchId });
                foreach (var member in lobby.AllMembers)
                {
                    _queue.EnqueueToSteamId(member.Id, new GcMessage(GcMsg.GCToClientMatchSignedOut, pushed));
                }
            }
        }

        var response = context.Codec.Encode(new CMsgGameMatchSignoutResponse { MatchId = matchId });
        return
        [
            new GcMessage(GcMsg.GameMatchSignOutResponse, response, TargetJobId: request.SourceJobId)
        ];
    }

    private MatchRecord BuildMatchRecord(
        CMsgGameMatchSignOut signOut,
        CSODOTALobby? lobby,
        ulong matchId,
        DotaGcTeam winningTeam)
    {
        var players = signOut.Teams
            .SelectMany((team, index) => team.Players.Select(player =>
                ToMatchPlayer(player, TeamForIndex(index), winningTeam)))
            .Where(player => player.SteamId != 0 && IsPlayingTeam(player.Team))
            .GroupBy(player => player.AccountId)
            .Select(group => group.First())
            .ToList();

        if (players.Count == 0 && lobby is not null)
        {
            players = lobby.AllMembers
                .Where(member => IsPlayingTeam((int)member.Team) && member.Id != 0)
                .Select(member => new MatchPlayerRecord
                {
                    SteamId = member.Id,
                    AccountId = SteamAccount.AccountIdFromSteamId(member.Id),
                    Team = (int)member.Team,
                    HeroId = member.HeroId,
                    Won = member.Team == winningTeam,
                    LeaverStatus = (uint)member.LeaverStatus
                })
                .Where(player => player.AccountId != 0)
                .GroupBy(player => player.AccountId)
                .Select(group => group.First())
                .ToList();
        }

        return new MatchRecord
        {
            MatchId = matchId,
            LobbyId = lobby?.LobbyId ?? 0,
            GameMode = lobby?.GameMode ?? 0,
            DurationSeconds = signOut.Duration,
            EndedAt = signOut.Date == 0
                ? DateTimeOffset.UtcNow
                : DateTimeOffset.FromUnixTimeSeconds(signOut.Date),
            GoodGuysWin = winningTeam == DotaGcTeam.DotaGcTeamGoodGuys,
            WinningTeam = (int)winningTeam,
            FirstBloodTime = signOut.FirstBloodTime,
            RadiantScore = At(signOut.TeamScores, 0),
            DireScore = At(signOut.TeamScores, 1),
            TowerStatus = signOut.TowerStatus ?? Array.Empty<uint>(),
            BarracksStatus = signOut.BarracksStatus ?? Array.Empty<uint>(),
            TeamScores = signOut.TeamScores ?? Array.Empty<uint>(),
            Cluster = signOut.Cluster,
            ServerAddress = signOut.ServerAddr,
            EventScore = signOut.EventScore,
            AutomaticSurrender = signOut.AutomaticSurrender,
            ServerVersion = signOut.ServerVersion,
            PreGameDuration = signOut.PreGameDuration,
            AverageNetworthDelta = signOut.AverageNetworthDelta,
            MatchFlags = signOut.MatchFlags,
            Players = players
        };
    }

    private static MatchPlayerRecord ToMatchPlayer(
        CMsgGameMatchSignOut.CTeam.CPlayer player,
        DotaGcTeam team,
        DotaGcTeam winningTeam) =>
        new()
        {
            SteamId = player.SteamId,
            AccountId = SteamAccount.AccountIdFromSteamId(player.SteamId),
            Team = (int)team,
            HeroId = player.HeroId,
            Won = team == winningTeam,
            Gold = player.Gold,
            Kills = player.Kills,
            Deaths = player.Deaths,
            Assists = player.Assists,
            LeaverStatus = player.LeaverStatus,
            LastHits = player.LastHits,
            Denies = player.Denies,
            GoldPerMin = player.GoldPerMin,
            XpPerMinute = player.XpPerMinute,
            GoldSpent = player.GoldSpent,
            Level = player.Level,
            ScaledHeroDamage = player.ScaledHeroDamage,
            ScaledTowerDamage = player.ScaledTowerDamage,
            ScaledHeroHealing = player.ScaledHeroHealing,
            TimeLastSeen = player.TimeLastSeen,
            SupportAbilityValue = player.SupportAbilityValue,
            PartyId = player.PartyId,
            ClaimedFarmGold = player.ClaimedFarmGold,
            SupportGold = player.SupportGold,
            ClaimedDenies = player.ClaimedDenies,
            ClaimedMisses = player.ClaimedMisses,
            Misses = player.Misses,
            NetWorth = player.NetWorth,
            HeroDamage = player.HeroDamage,
            TowerDamage = player.TowerDamage,
            HeroHealing = player.HeroHealing,
            MatchPlayerFlags = player.MatchPlayerFlags,
            HeroPickOrder = player.HeroPickOrder,
            HeroWasRandomed = player.HeroWasRandomed,
            Lane = player.Lane,
            Items = player.Items ?? Array.Empty<int>(),
            ItemPurchaseTimes = player.ItemPurchaseTimes ?? Array.Empty<uint>()
        };

    private void UpdateAccountCaches(IEnumerable<MatchPlayerRecord> players)
    {
        foreach (var player in players.GroupBy(player => player.AccountId).Select(group => group.First()))
        {
            var stats = _matches.GetProfileStats(player.AccountId);
            var steamId = player.SteamId != 0
                ? player.SteamId
                : SteamAccount.SteamIdFromAccountId(player.AccountId);
            var key = SoCacheKey.Game(steamId);
            var objectKey = new SoObjectKey(DotaSoCache.TypeDotaGameAccountClient, player.AccountId);

            if (!_soCache.TryGetObject(key, objectKey, out CSODOTAGameAccountClient account))
            {
                account = new CSODOTAGameAccountClient { AccountId = player.AccountId };
            }

            account.Wins = NonNegative(stats.Wins);
            account.Losses = NonNegative(stats.Losses);
            account.CasualGamesPlayed = NonNegative(stats.Games);
            account.LeaverCount = NonNegative(stats.LeaverCount);
            LocalConductState.ApplyTo(account);
            _soCache.Set(key, objectKey, account);
        }
    }

    private static DotaGcTeam ResolveWinningTeam(CMsgGameMatchSignOut signOut) =>
        signOut.ShouldSerializeWinningTeam() && IsPlayingTeam((int)signOut.WinningTeam)
            ? signOut.WinningTeam
            : signOut.GoodGuysWin
                ? DotaGcTeam.DotaGcTeamGoodGuys
                : DotaGcTeam.DotaGcTeamBadGuys;

    private static DotaGcTeam TeamForIndex(int index) => index switch
    {
        0 => DotaGcTeam.DotaGcTeamGoodGuys,
        1 => DotaGcTeam.DotaGcTeamBadGuys,
        _ => DotaGcTeam.DotaGcTeamNoteam
    };

    private static bool IsPlayingTeam(int team) => team is
        (int)DotaGcTeam.DotaGcTeamGoodGuys or
        (int)DotaGcTeam.DotaGcTeamBadGuys;

    private static uint At(uint[]? values, int index) =>
        values is not null && values.Length > index ? values[index] : 0;

    private static uint NonNegative(int value) => (uint)Math.Max(0, value);
}
