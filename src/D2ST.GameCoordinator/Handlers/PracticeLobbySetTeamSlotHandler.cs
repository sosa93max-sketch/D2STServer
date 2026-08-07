using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Moves the caller to a team slot (7047); the lobby update is the answer.</summary>
public sealed class PracticeLobbySetTeamSlotHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbySetTeamSlotHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbySetTeamSlot;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var slot = context.Codec.Decode<CMsgPracticeLobbySetTeamSlot>(request.Body);
        _lobbies.SetTeamSlot(context, slot);
        return [];
    }
}
