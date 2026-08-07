using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Removes a player from the lobby (7081). Host only.</summary>
public sealed class PracticeLobbyKickHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyKickHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyKick;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var kick = context.Codec.Decode<CMsgPracticeLobbyKick>(request.Body);
        _lobbies.Kick(context, kick.AccountId);
        return [];
    }
}
