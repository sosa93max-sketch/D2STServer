using D2ST.Core.Events;
using D2ST.Core.Social;
using D2ST.Core.Steam;
using D2ST.Steam.Events;

namespace D2ST.Steam.Social;

/// <summary>
/// Fans persona/presence changes out to everyone who can see the player. The
/// event carries the player's whole visible state (not just what changed), so a
/// client never has to follow up with a read and never keeps a stale "in game"
/// for someone who went offline.
/// </summary>
public sealed class SocialEventPublisher
{
    private readonly IUserDirectory _users;
    private readonly FriendGraph _graph;
    private readonly IEventStream _events;

    public SocialEventPublisher(IUserDirectory users, FriendGraph graph, IEventStream events)
    {
        _users = users;
        _graph = graph;
        _events = events;
    }

    public async Task PublishToAudienceAsync(
        uint accountId,
        string type,
        PersonaChange changeFlags,
        CancellationToken cancellationToken = default)
    {
        foreach (var recipient in await _graph.AudienceAsync(accountId, cancellationToken))
        {
            var profile = await _users.FindAsync(recipient, accountId, cancellationToken);
            if (profile is not null)
            {
                _events.Publish(recipient, ToEvent(profile, type, changeFlags));
            }
        }
    }

    /// <summary>Tells a single account about a relationship change with another.</summary>
    public async Task PublishRelationshipAsync(
        uint recipientAccountId,
        uint subjectAccountId,
        string type,
        FriendRelationship relationship,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _users.FindAsync(recipientAccountId, subjectAccountId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        _events.Publish(recipientAccountId, ToEvent(profile, type, PersonaChange.Relationship) with
        {
            FriendRelationship = relationship,
            RequestId = requestId
        });
    }

    private static SteamEvent ToEvent(UserProfile profile, string type, PersonaChange changeFlags) => new()
    {
        Type = type,
        SteamId = profile.SteamId,
        AccountId = profile.AccountId,
        PersonaName = profile.PersonaName,
        AppId = profile.AppId,
        LobbyId = profile.LobbyId,
        GameServerSteamId = profile.GameServerSteamId,
        GameServerIp = profile.GameServerIp,
        GameServerPort = profile.GameServerPort,
        PersonaState = profile.PersonaState,
        ChangeFlags = changeFlags,
        FriendRelationship = profile.Relationship,
        RichPresence = profile.RichPresence
    };
}
