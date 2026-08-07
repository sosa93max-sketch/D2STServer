using D2ST.Protocol;
using D2ST.Protocol.Versioning;

namespace D2ST.GameCoordinator;

/// <summary>
/// Per-request context handed to a handler: who is connected, which build they
/// run (and the resolved <see cref="VersionProfile"/>), and the codec to encode
/// response bodies.
/// </summary>
public sealed class GcContext
{
    public required uint AccountId { get; init; }
    public required ulong SteamId { get; init; }
    public required IGcProtoCodec Codec { get; init; }

    /// <summary>
    /// Name the caller logged on with. The GC has no persona directory of its
    /// own, so this is the only place a name enters it: it is remembered per
    /// player and reused when another player has to be shown who invited them.
    /// </summary>
    public string PersonaName { get; init; } = string.Empty;

    /// <summary>
    /// steam.inf ClientVersion of the connected build. It is only known once the
    /// client announces it in its GCClientHello, so the hello handler writes it
    /// here and the caller persists it on the session.
    /// </summary>
    public required int ClientVersion { get; set; }

    public VersionProfile Profile => VersionProfiles.Resolve(ClientVersion);
}
