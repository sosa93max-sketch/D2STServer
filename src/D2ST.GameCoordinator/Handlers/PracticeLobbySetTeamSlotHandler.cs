using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Moves the caller to a team slot (7047 → 7055 with the generic result the
/// modern client waits for; the lobby update is how the change is drawn).
/// </summary>
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
        var moved = _lobbies.SetTeamSlot(context, slot);
        var response = new CMsgGenericResult { Eresult = moved ? 1u : 0u };

        return
        [
            new GcMessage(GcMsg.PracticeLobbyResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
