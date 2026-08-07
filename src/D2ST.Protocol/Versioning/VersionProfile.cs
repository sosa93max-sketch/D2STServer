namespace D2ST.Protocol.Versioning;

/// <summary>
/// Per-build handshake profile. The wire protobuf definitions are shared across
/// Dota builds (GC message ids and field numbers are stable), so only the
/// version-divergent constants/behaviour of the ClientHello -> ClientWelcome
/// handshake live here, selected from the client's steam.inf ClientVersion.
/// </summary>
/// <param name="Name">Label surfaced in logs/diagnostics.</param>
/// <param name="SocacheFileVersion">gc_socache_file_version stamped on the welcome.</param>
/// <param name="IncludeDotaPlus">
/// Publish the Dota Plus account SO object. Dota Plus shipped in 2018, so 7.22g
/// (2019) has it; pre-2018 builds must omit it.
/// </param>
public sealed record VersionProfile(string Name, int SocacheFileVersion, bool IncludeDotaPlus);
