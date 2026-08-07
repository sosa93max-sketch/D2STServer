namespace D2ST.Core.Networking;

/// <summary>
/// One peer-to-peer datagram relayed through the server. The server never
/// interprets the payload: it only routes it to <paramref name="RemoteSteamId"/>
/// with the transport metadata the receiving shim needs to hand it to the right
/// Steamworks networking interface.
/// </summary>
public sealed record P2PPacket(
    ulong RemoteSteamId,
    string PayloadBase64,
    int SendType,
    int Channel,
    string Transport,
    int VirtualPort,
    uint SourceConnectionId,
    uint TargetConnectionId);

/// <summary>
/// Transport discriminators understood by the shim's event pump. They select
/// which networking interface replays the packet, so the strings must match.
/// </summary>
public static class P2PTransports
{
    public const string Legacy = "legacy";
    public const string Messages = "messages";
    public const string Sockets = "sockets";
}
