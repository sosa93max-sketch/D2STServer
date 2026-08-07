using D2ST.Core.Accounts;
using D2ST.GameCoordinator.Players;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Parties;

/// <summary>
/// The parties players are grouped in. A party is a Shared Object
/// (<c>CSODOTAParty</c>, type 2003) on a cache owned by the party itself rather
/// than by any of its members, so every member is subscribed to the same cache
/// and each change reaches all of them as one delta.
/// <para>
/// The object is the state: membership, leader, per-member ping data and the
/// ready check all live in it, and this service only keeps the indexes needed to
/// find a cache from a Steam id. An invite is the same idea one object smaller —
/// a <c>CSODOTAPartyInvite</c> (type 2006) on a cache owned by the invite, so
/// publishing it to its target and revoking it are a subscribe and an
/// unsubscribe.
/// </para>
/// </summary>
public sealed class PartyService : IGcWelcomeContributor
{
    /// <summary>Five players: the size of a Dota team, which is what a party fills.</summary>
    public const int MaxMembers = 5;

    private static readonly TimeSpan InviteLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReadyCheckDuration = TimeSpan.FromSeconds(60);

    private const int SequenceBits = 20;
    private const ulong SequenceMask = (1UL << SequenceBits) - 1;

    private readonly SoCacheService _soCache;
    private readonly IGcPlayerDirectory _players;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<ulong, ulong> _memberships = [];
    private readonly Dictionary<ulong, PartyInvite> _invites = [];
    private readonly Dictionary<ulong, string> _personaNames = [];
    private ulong _sequence;

    public PartyService(SoCacheService soCache, IGcPlayerDirectory players, TimeProvider time)
    {
        _soCache = soCache;
        _players = players;
        _time = time;
    }

    /// <summary>The party a player is in, or null. Read-only snapshot.</summary>
    public CSODOTAParty? Find(ulong steamId)
    {
        lock (_gate)
        {
            return TryGetPartyOf(steamId, out var party) ? party : null;
        }
    }

    /// <summary>
    /// Invites a player. The caller's party is created on the spot if it has
    /// none — the client shows a party as soon as it invites somebody, without
    /// asking for one first.
    /// </summary>
    public CMsgInvitationCreated Invite(GcContext context, CMsgInviteToParty request)
    {
        lock (_gate)
        {
            Remember(context);
            PruneInvites();

            var target = request.SteamId;
            if (target == 0 || target == context.SteamId || !_players.IsOnline(target))
            {
                return new CMsgInvitationCreated { SteamId = target, UserOffline = true };
            }

            var party = EnsureParty(context, request.PingData);
            if (party.MemberIds.Length >= MaxMembers || party.MemberIds.Contains(target))
            {
                return new CMsgInvitationCreated { GroupId = party.PartyId, SteamId = target };
            }

            // One live invite per target: re-inviting replaces the old one
            // instead of leaving two rows in the invitee's UI.
            foreach (var superseded in InvitesOf(party.PartyId).Where(invite => invite.TargetSteamId == target).ToList())
            {
                DestroyInvite(superseded);
            }

            var created = new PartyInvite(
                NextId(),
                party.PartyId,
                target,
                context.SteamId,
                request.TeamId,
                request.AsCoach,
                _time.GetUtcNow());

            _invites.Add(created.Id, created);
            _soCache.Set(
                SoCacheKey.PartyInvite(created.Id),
                new SoObjectKey(DotaSoCache.TypeDotaPartyInvite, created.Id),
                BuildInvite(party, created));
            _soCache.PushSubscribe(AccountIdOf(target), SoOwner.ForInvite(created.Id));

            return new CMsgInvitationCreated { GroupId = party.PartyId, SteamId = target };
        }
    }

    /// <summary>
    /// Accepts or declines an invite. Either way the invite is destroyed; an
    /// acceptance also moves the player out of whatever party it was in, because
    /// the client can only display one.
    /// </summary>
    public void RespondToInvite(GcContext context, CMsgPartyInviteResponse response)
    {
        lock (_gate)
        {
            Remember(context);
            PruneInvites();

            var invite = _invites.Values.FirstOrDefault(pending =>
                pending.PartyId == response.PartyId && pending.TargetSteamId == context.SteamId);

            if (invite is null)
            {
                return;
            }

            DestroyInvite(invite);
            if (!response.Accept || !TryGetParty(invite.PartyId, out var party) || party.MemberIds.Length >= MaxMembers)
            {
                return;
            }

            if (TryGetPartyOf(context.SteamId, out var current) && current.PartyId != party.PartyId)
            {
                Detach(current, context.SteamId);
            }

            foreach (var pending in InvitesFor(context.SteamId))
            {
                DestroyInvite(pending);
            }

            AddMember(party, context.SteamId, response.PingData, invite.AsCoach);
            Write(party);
            _soCache.PushSubscribe(context.AccountId, SoOwner.ForParty(party.PartyId));

            if (party.MemberIds.Length >= MaxMembers)
            {
                foreach (var outstanding in InvitesOf(party.PartyId))
                {
                    DestroyInvite(outstanding);
                }
            }
        }
    }

    /// <summary>Withdraws the invites the caller's party sent, optionally only to some players.</summary>
    public void CancelInvites(GcContext context, IReadOnlyCollection<ulong> targetSteamIds)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party))
            {
                return;
            }

            foreach (var invite in InvitesOf(party.PartyId))
            {
                if (targetSteamIds.Count == 0 || targetSteamIds.Contains(invite.TargetSteamId))
                {
                    DestroyInvite(invite);
                }
            }
        }
    }

    public void Leave(GcContext context)
    {
        lock (_gate)
        {
            if (TryGetPartyOf(context.SteamId, out var party))
            {
                Detach(party, context.SteamId);
            }
        }
    }

    /// <summary>Removes another member. Only the leader may, and never itself.</summary>
    public void Kick(GcContext context, ulong targetSteamId)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party) ||
                party.LeaderId != context.SteamId ||
                targetSteamId == context.SteamId ||
                !party.MemberIds.Contains(targetSteamId))
            {
                return;
            }

            Detach(party, targetSteamId);
        }
    }

    /// <summary>Hands the party over to another member. Only the leader may.</summary>
    public void SetLeader(GcContext context, ulong newLeaderSteamId)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party) ||
                party.LeaderId != context.SteamId ||
                party.LeaderId == newLeaderSteamId ||
                !party.MemberIds.Contains(newLeaderSteamId))
            {
                return;
            }

            party.LeaderId = newLeaderSteamId;
            Write(party);
        }
    }

    public void SetCoach(GcContext context, bool wantsCoach)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party))
            {
                return;
            }

            var member = MemberOf(party, context.SteamId);
            if (member is null || member.IsCoach == wantsCoach)
            {
                return;
            }

            member.IsCoach = wantsCoach;
            Write(party);
        }
    }

    /// <summary>
    /// Stores the caller's measured region pings on its party member entry. The
    /// party UI shows every member's ping, so this is what the other clients
    /// read it from.
    /// </summary>
    public void SetPingData(GcContext context, CMsgClientPingData ping)
    {
        lock (_gate)
        {
            if (TryGetPartyOf(context.SteamId, out var party) && ApplyPing(party, context.SteamId, ping))
            {
                Write(party);
            }
        }
    }

    public EReadyCheckRequestResult StartReadyCheck(GcContext context)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party))
            {
                return EReadyCheckRequestResult.kEReadyCheckRequestResultNotInParty;
            }

            var now = Now();
            if (party.ReadyCheck is not null && party.ReadyCheck.FinishTimestamp > now)
            {
                return EReadyCheckRequestResult.kEReadyCheckRequestResultAlreadyInProgress;
            }

            party.ReadyCheck = new CMsgReadyCheckStatus
            {
                StartTimestamp = now,
                FinishTimestamp = now + (uint)ReadyCheckDuration.TotalSeconds,
                InitiatorAccountId = context.AccountId
            };

            Write(party);
            return EReadyCheckRequestResult.kEReadyCheckRequestResultSuccess;
        }
    }

    /// <summary>Records one member's answer to the running ready check.</summary>
    public void AcknowledgeReadyCheck(GcContext context, EReadyCheckStatus status)
    {
        lock (_gate)
        {
            if (!TryGetPartyOf(context.SteamId, out var party) ||
                party.ReadyCheck is null ||
                party.ReadyCheck.FinishTimestamp < Now())
            {
                return;
            }

            var answered = party.ReadyCheck.ReadyMembers.FirstOrDefault(member => member.AccountId == context.AccountId);
            if (answered is null)
            {
                party.ReadyCheck.ReadyMembers.Add(new CMsgReadyCheckStatus.ReadyMember
                {
                    AccountId = context.AccountId,
                    ReadyStatus = status
                });
            }
            else if (answered.ReadyStatus == status)
            {
                return;
            }
            else
            {
                answered.ReadyStatus = status;
            }

            Write(party);
        }
    }

    /// <summary>
    /// The party and pending invites a reconnecting client has to be told about,
    /// as subscription messages the welcome carries.
    /// </summary>
    public IReadOnlyList<CMsgSOCacheSubscribed> CachesFor(GcContext context)
    {
        lock (_gate)
        {
            Remember(context);
            PruneInvites();

            var caches = new List<CMsgSOCacheSubscribed>();
            if (TryGetPartyOf(context.SteamId, out var party))
            {
                caches.AddRange(_soCache.Subscribe(context.AccountId, SoOwner.ForParty(party.PartyId)));
            }

            foreach (var invite in InvitesFor(context.SteamId))
            {
                caches.AddRange(_soCache.Subscribe(context.AccountId, SoOwner.ForInvite(invite.Id)));
            }

            return caches;
        }
    }

    private CSODOTAParty EnsureParty(GcContext context, CMsgClientPingData? ping)
    {
        if (TryGetPartyOf(context.SteamId, out var existing))
        {
            if (ApplyPing(existing, context.SteamId, ping))
            {
                Write(existing);
            }

            return existing;
        }

        var party = new CSODOTAParty
        {
            PartyId = NextId(),
            LeaderId = context.SteamId,
            MemberIds = []
        };

        AddMember(party, context.SteamId, ping, asCoach: false);
        Write(party);
        _soCache.PushSubscribe(context.AccountId, SoOwner.ForParty(party.PartyId));
        return party;
    }

    /// <summary>
    /// Removes a member, disbanding the party when it would be left with a
    /// single player: a party of one is what the client shows for no party at
    /// all, and the real GC destroys it too.
    /// </summary>
    private void Detach(CSODOTAParty party, ulong steamId)
    {
        if (!party.MemberIds.Contains(steamId))
        {
            return;
        }

        if (party.MemberIds.Length <= 2)
        {
            Disband(party);
            return;
        }

        RemoveMember(party, steamId);
        if (party.LeaderId == steamId)
        {
            party.LeaderId = party.MemberIds[0];
        }

        _soCache.Unsubscribe(AccountIdOf(steamId), SoOwner.ForParty(party.PartyId));
        Write(party);
    }

    private void Disband(CSODOTAParty party)
    {
        foreach (var invite in InvitesOf(party.PartyId))
        {
            DestroyInvite(invite);
        }

        foreach (var member in party.MemberIds)
        {
            _memberships.Remove(member);
        }

        _soCache.RemoveOwner(SoOwner.ForParty(party.PartyId));
    }

    private void AddMember(CSODOTAParty party, ulong steamId, CMsgClientPingData? ping, bool asCoach)
    {
        party.MemberIds = [.. party.MemberIds, steamId];
        party.Members.Add(new CSODOTAPartyMember { IsCoach = asCoach });
        _memberships[steamId] = party.PartyId;
        ApplyPing(party, steamId, ping);
    }

    // member_ids and members are parallel lists: the client reads a member's
    // Steam id from one and its ping/coach state from the other by index.
    private void RemoveMember(CSODOTAParty party, ulong steamId)
    {
        var index = Array.IndexOf(party.MemberIds, steamId);
        if (index < 0)
        {
            return;
        }

        party.MemberIds = party.MemberIds.Where((_, position) => position != index).ToArray();
        if (index < party.Members.Count)
        {
            party.Members.RemoveAt(index);
        }

        var answered = party.ReadyCheck?.ReadyMembers.FirstOrDefault(member => member.AccountId == AccountIdOf(steamId));
        if (answered is not null)
        {
            party.ReadyCheck!.ReadyMembers.Remove(answered);
        }

        _memberships.Remove(steamId);
    }

    private static CSODOTAPartyMember? MemberOf(CSODOTAParty party, ulong steamId)
    {
        var index = Array.IndexOf(party.MemberIds, steamId);
        return index >= 0 && index < party.Members.Count ? party.Members[index] : null;
    }

    private static bool ApplyPing(CSODOTAParty party, ulong steamId, CMsgClientPingData? ping)
    {
        var member = MemberOf(party, steamId);
        if (ping is null || member is null)
        {
            return false;
        }

        member.RegionPingCodes = ping.RegionCodes ?? [];
        member.RegionPingTimes = ping.RegionPings ?? [];
        member.RegionPingFailedBitmask = ping.RegionPingFailedBitmask;
        return true;
    }

    private CSODOTAPartyInvite BuildInvite(CSODOTAParty party, PartyInvite invite)
    {
        var message = new CSODOTAPartyInvite
        {
            GroupId = invite.PartyId,
            SenderId = invite.SenderSteamId,
            SenderName = PersonaNameOf(invite.SenderSteamId),
            TeamId = invite.TeamId,
            AsCoach = invite.AsCoach,
            InviteGid = invite.Id
        };

        for (var index = 0; index < party.MemberIds.Length; index++)
        {
            message.Members.Add(new CSODOTAPartyInvite.PartyMember
            {
                SteamId = party.MemberIds[index],
                Name = PersonaNameOf(party.MemberIds[index]),
                IsCoach = index < party.Members.Count && party.Members[index].IsCoach
            });
        }

        return message;
    }

    private void DestroyInvite(PartyInvite invite)
    {
        _invites.Remove(invite.Id);
        _soCache.RemoveOwner(SoOwner.ForInvite(invite.Id));
    }

    /// <summary>
    /// Drops invites nobody answered. There is no timer: an invite only matters
    /// while somebody is talking to the GC, so expiry is applied on the way in.
    /// </summary>
    private void PruneInvites()
    {
        var cutoff = _time.GetUtcNow() - InviteLifetime;
        foreach (var invite in _invites.Values.Where(pending => pending.CreatedAt < cutoff).ToList())
        {
            DestroyInvite(invite);
        }
    }

    private List<PartyInvite> InvitesOf(ulong partyId) =>
        _invites.Values.Where(invite => invite.PartyId == partyId).ToList();

    private List<PartyInvite> InvitesFor(ulong targetSteamId) =>
        _invites.Values.Where(invite => invite.TargetSteamId == targetSteamId).ToList();

    private bool TryGetPartyOf(ulong steamId, out CSODOTAParty party)
    {
        if (_memberships.TryGetValue(steamId, out var partyId) && TryGetParty(partyId, out party))
        {
            return true;
        }

        _memberships.Remove(steamId);
        party = default!;
        return false;
    }

    private bool TryGetParty(ulong partyId, out CSODOTAParty party) =>
        _soCache.TryGetObject(
            SoCacheKey.Party(partyId),
            new SoObjectKey(DotaSoCache.TypeDotaParty, partyId),
            out party);

    private void Write(CSODOTAParty party) =>
        _soCache.Set(
            SoCacheKey.Party(party.PartyId),
            new SoObjectKey(DotaSoCache.TypeDotaParty, party.PartyId),
            party);

    private void Remember(GcContext context)
    {
        if (!string.IsNullOrEmpty(context.PersonaName))
        {
            _personaNames[context.SteamId] = context.PersonaName;
        }
    }

    private string PersonaNameOf(ulong steamId) =>
        _personaNames.TryGetValue(steamId, out var name) ? name : string.Empty;

    private uint Now() => (uint)_time.GetUtcNow().ToUnixTimeSeconds();

    private static uint AccountIdOf(ulong steamId) => SteamAccount.AccountIdFromSteamId(steamId);

    /// <summary>
    /// Party and invite ids the client can tell apart across restarts: the
    /// current second in the high bits, a counter in the low ones.
    /// </summary>
    private ulong NextId()
    {
        _sequence = (_sequence + 1) & SequenceMask;
        return ((ulong)_time.GetUtcNow().ToUnixTimeSeconds() << SequenceBits) | _sequence;
    }

    private sealed record PartyInvite(
        ulong Id,
        ulong PartyId,
        ulong TargetSteamId,
        ulong SenderSteamId,
        uint TeamId,
        bool AsCoach,
        DateTimeOffset CreatedAt);
}
