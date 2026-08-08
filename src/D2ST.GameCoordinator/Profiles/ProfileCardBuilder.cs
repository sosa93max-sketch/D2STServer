using D2ST.Core.Matches;
using D2ST.Core.Profiles;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Ranks;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Profiles;

/// <summary>
/// Projects persisted local match aggregates and the saved card layout into
/// the protobuf card consumed by the Dota profile UI.
/// </summary>
public sealed class ProfileCardBuilder
{
    private readonly IRankStore _ranks;
    private readonly IMatchStore _matches;
    private readonly IProfileStore _profiles;
    private readonly DotaPlusProjection _dotaPlus;

    public ProfileCardBuilder(
        IRankStore ranks,
        IMatchStore matches,
        IProfileStore profiles,
        DotaPlusProjection dotaPlus)
    {
        _ranks = ranks;
        _matches = matches;
        _profiles = profiles;
        _dotaPlus = dotaPlus;
    }

    public CMsgDOTAProfileCard Build(uint accountId)
    {
        var stats = _matches.GetProfileStats(accountId);
        var rank = _ranks.GetOrCreate(accountId);
        var info = RankMath.VisibleRankFor(rank);
        var configuredSlots = _profiles.GetCard(accountId).Slots;
        var slots = configuredSlots.Count == 0
            ? DefaultSlots()
            : configuredSlots;

        var heroStats = slots.Any(slot => slot.SlotType == (uint)EProfileCardSlotType.kEProfileCardSlotTypeHero)
            ? _matches.GetHeroStats(accountId).ToDictionary(stat => stat.HeroId)
            : new Dictionary<int, HeroStatsRecord>();

        var card = new CMsgDOTAProfileCard
        {
            AccountId = accountId,
            BadgePoints = 0,
            EventId = 0,
            RankTier = (uint)info.RankValue,
            RankTierScore = (uint)info.ProgressPercent,
            LeaderboardRank = 0,
            LeaderboardRankCore = 0,
            IsPlusSubscriber = _dotaPlus.IsActive(accountId),
            LifetimeGames = NonNegative(stats.Games)
        };

        foreach (var slot in slots)
        {
            card.Slots.Add(ToProtocolSlot(slot, stats, heroStats));
        }

        return card;
    }

    private static IReadOnlyList<ProfileCardSlot> DefaultSlots() =>
    [
        new(
            0,
            (uint)EProfileCardSlotType.kEProfileCardSlotTypeStat,
            (ulong)CMsgDOTAProfileCard.EStatID.keStatWins),
        new(
            1,
            (uint)EProfileCardSlotType.kEProfileCardSlotTypeStat,
            (ulong)CMsgDOTAProfileCard.EStatID.keStatGamesPlayed)
    ];

    private static CMsgDOTAProfileCard.Slot ToProtocolSlot(
        ProfileCardSlot slot,
        PlayerProfileStats stats,
        IReadOnlyDictionary<int, HeroStatsRecord> heroStats)
    {
        var result = new CMsgDOTAProfileCard.Slot { SlotId = slot.SlotId };
        var slotType = (EProfileCardSlotType)slot.SlotType;

        switch (slotType)
        {
            case EProfileCardSlotType.kEProfileCardSlotTypeStat:
                var statId = ToStatId(slot.SlotValue);
                result.stat = new CMsgDOTAProfileCard.Slot.Stat
                {
                    StatId = statId,
                    StatScore = StatScore(statId, stats)
                };
                break;

            case EProfileCardSlotType.kEProfileCardSlotTypeTrophy:
                result.trophy = new CMsgDOTAProfileCard.Slot.Trophy
                {
                    TrophyId = ToUInt(slot.SlotValue),
                    TrophyScore = 0
                };
                break;

            case EProfileCardSlotType.kEProfileCardSlotTypeItem:
                result.item = new CMsgDOTAProfileCard.Slot.Item
                {
                    ItemId = slot.SlotValue
                };
                break;

            case EProfileCardSlotType.kEProfileCardSlotTypeHero:
                var heroId = slot.SlotValue <= int.MaxValue ? (int)slot.SlotValue : 0;
                heroStats.TryGetValue(heroId, out var hero);
                result.hero = new CMsgDOTAProfileCard.Slot.Hero
                {
                    HeroId = heroId,
                    HeroWins = NonNegative(hero?.Wins ?? 0),
                    HeroLosses = NonNegative(hero?.Losses ?? 0)
                };
                break;

            case EProfileCardSlotType.kEProfileCardSlotTypeEmoticon:
                result.emoticon = new CMsgDOTAProfileCard.Slot.Emoticon
                {
                    EmoticonId = ToUInt(slot.SlotValue)
                };
                break;

            case EProfileCardSlotType.kEProfileCardSlotTypeTeam:
                result.team = new CMsgDOTAProfileCard.Slot.Team
                {
                    TeamId = ToUInt(slot.SlotValue)
                };
                break;
        }

        return result;
    }

    private static CMsgDOTAProfileCard.EStatID ToStatId(ulong value) => value switch
    {
        (ulong)CMsgDOTAProfileCard.EStatID.keStatWins => CMsgDOTAProfileCard.EStatID.keStatWins,
        (ulong)CMsgDOTAProfileCard.EStatID.keStatCommends => CMsgDOTAProfileCard.EStatID.keStatCommends,
        (ulong)CMsgDOTAProfileCard.EStatID.keStatGamesPlayed => CMsgDOTAProfileCard.EStatID.keStatGamesPlayed,
        (ulong)CMsgDOTAProfileCard.EStatID.keStatFirstMatchDate => CMsgDOTAProfileCard.EStatID.keStatFirstMatchDate,
        (ulong)CMsgDOTAProfileCard.EStatID.keStatPreviousSeasonRank => CMsgDOTAProfileCard.EStatID.keStatPreviousSeasonRank,
        (ulong)CMsgDOTAProfileCard.EStatID.keStatGamesMVP => CMsgDOTAProfileCard.EStatID.keStatGamesMVP,
        _ => CMsgDOTAProfileCard.EStatID.keStatWins
    };

    private static uint StatScore(CMsgDOTAProfileCard.EStatID statId, PlayerProfileStats stats) => statId switch
    {
        CMsgDOTAProfileCard.EStatID.keStatWins => NonNegative(stats.Wins),
        CMsgDOTAProfileCard.EStatID.keStatGamesPlayed => NonNegative(stats.Games),
        _ => 0
    };

    private static uint ToUInt(ulong value) => value > uint.MaxValue ? 0 : (uint)value;

    private static uint NonNegative(int value) => (uint)Math.Max(0, value);
}
