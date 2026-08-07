using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Chat;

/// <summary>
/// The chat the GC serves. Everything a deployment may want to change lives
/// here — which channels exist before anyone joins one, which of them a client
/// is put in at logon, how large they are and whether players may open channels
/// of their own — because the 7.22g client has no way to configure any of it:
/// it only ever asks the GC for the list.
/// </summary>
public sealed class GcChatOptions
{
    public const string SectionName = "GameCoordinator:Chat";

    /// <summary>
    /// Channels that exist from startup, in the order the list is served. A
    /// deployment that configures none gets <see cref="DefaultChannels"/>;
    /// configuring any replaces them entirely, so a channel can be removed by
    /// leaving it out.
    /// </summary>
    public IList<GcChatChannelOptions> Channels { get; set; } = new List<GcChatChannelOptions>();

    /// <summary>What a server with no chat configuration serves.</summary>
    public static IReadOnlyList<GcChatChannelOptions> DefaultChannels { get; } =
    [
        new() { Name = "D2MAX", WelcomeMessage = "Welcome to D2MAX.", AutoJoin = true },
        new() { Name = "Trade", Type = DOTAChatChannelTypet.DOTAChannelTypeCustom },
        new() { Name = "LFG", Type = DOTAChatChannelTypet.DOTAChannelTypeCustom }
    ];

    /// <summary>Members a channel takes when it does not set its own limit.</summary>
    public int DefaultMaxMembers { get; set; } = 500;

    /// <summary>Channels one player may be in at once; the client itself stops at ten tabs.</summary>
    public int MaxChannelsPerUser { get; set; } = 10;

    /// <summary>Characters kept from a chat line; longer ones are cut, not refused.</summary>
    public int MaxMessageLength { get; set; } = 1024;

    /// <summary>
    /// Whether joining a name that does not exist creates it. With this off the
    /// only channels are the configured ones (plus private chats), which is what
    /// a closed deployment wants.
    /// </summary>
    public bool AllowCustomChannels { get; set; } = true;

    /// <summary>
    /// Whether a chat line is sent back to the player who wrote it. The 7.22g
    /// client already draws its own line as it sends it, so echoing shows it
    /// twice; leave this off unless a client needs the server's copy.
    /// </summary>
    public bool EchoOwnMessages { get; set; }
}

/// <summary>One configured channel.</summary>
public sealed class GcChatChannelOptions
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Channel type the client tabs it under. Regional is the lobby-screen chat;
    /// Custom is a user channel.
    /// </summary>
    public DOTAChatChannelTypet Type { get; set; } = DOTAChatChannelTypet.DOTAChannelTypeRegional;

    /// <summary>Member cap, or null for <see cref="GcChatOptions.DefaultMaxMembers"/>.</summary>
    public int? MaxMembers { get; set; }

    /// <summary>Line the client prints when it enters the channel.</summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>Whether every client is put in this channel as soon as it reaches the GC.</summary>
    public bool AutoJoin { get; set; }
}
