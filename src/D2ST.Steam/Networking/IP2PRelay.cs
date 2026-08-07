using D2ST.Core.Networking;
using D2ST.Core.Steam;

namespace D2ST.Steam.Networking;

/// <summary>
/// Peers cannot reach each other directly through the shim, so every P2P
/// datagram is relayed: the sender posts it and the server pushes it to the
/// recipient's event stream verbatim.
/// </summary>
public interface IP2PRelay
{
    void Send(SteamSession session, P2PPacket packet);

    void Send(SteamSession session, IEnumerable<P2PPacket> packets);
}
