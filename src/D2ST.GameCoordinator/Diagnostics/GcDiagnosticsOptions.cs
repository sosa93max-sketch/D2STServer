namespace D2ST.GameCoordinator.Diagnostics;

/// <summary>
/// Controls the capture of GC messages the router has no handler for. The dump
/// is the raw material for stage 4: every line can be decoded offline to find
/// out what a build actually asks for.
/// </summary>
public sealed class GcDiagnosticsOptions
{
    public const string SectionName = "GameCoordinator:Diagnostics";

    /// <summary>Whether unhandled messages are appended to <see cref="UnhandledMessageLogPath"/>.</summary>
    public bool RecordUnhandledMessages { get; set; } = true;

    /// <summary>JSON Lines file the dump is appended to, relative to the content root.</summary>
    public string UnhandledMessageLogPath { get; set; } = "Logs/unhandled-gc.jsonl";

    /// <summary>Upper bound on the payload bytes written per message; longer bodies are truncated.</summary>
    public int MaxBodyBytes { get; set; } = 65536;
}
