using D2ST.Core.Accounts;
using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Players;
using D2ST.Protocol;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Options;

namespace D2ST.GameCoordinator.Chat;

/// <summary>
/// The chat channels the GC serves. Unlike a party or a lobby a channel is not
/// a Shared Object: the client keeps no cache of it and reacts only to the
/// messages the GC addresses to it (joined, left, one chat line), so everything
/// here is pushed through <see cref="IGcMessageQueue"/> and nothing is
/// published as a delta.
/// <para>
/// Which channels exist is a server decision (<see cref="GcChatOptions"/>): the
/// configured ones are created at startup, are listed even while empty and
/// survive their last member, while a channel a player opened disappears with
/// it. A private chat is the same channel with a membership of its own: only an
/// invited account may enter it, and its admins are the accounts that may
/// invite or kick.
/// </para>
/// </summary>
public sealed class ChatService : IGcLogonListener
{
    private readonly GcChatOptions _options;
    private readonly IGcMessageQueue _queue;
    private readonly IGcProtoCodec _codec;
    private readonly IGcPlayerDirectory _players;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<ulong, Channel> _channels = [];
    private ulong _sequence;

    public ChatService(
        IOptions<GcChatOptions> options,
        IGcMessageQueue queue,
        IGcProtoCodec codec,
        IGcPlayerDirectory players,
        TimeProvider time)
    {
        _options = options.Value;
        _queue = queue;
        _codec = codec;
        _players = players;
        _time = time;

        var configuration = _options.Channels.Where(channel => !string.IsNullOrWhiteSpace(channel.Name)).ToList();
        foreach (var configured in configuration.Count > 0 ? configuration : GcChatOptions.DefaultChannels)
        {
            var channel = Create(configured.Name, configured.Type, configured.MaxMembers ?? _options.DefaultMaxMembers);
            channel.IsConfigured = true;
            channel.AutoJoin = configured.AutoJoin;
            channel.WelcomeMessage = configured.WelcomeMessage;
        }
    }

    /// <summary>
    /// Puts a client in the channels the server marked <c>AutoJoin</c> as soon
    /// as it reaches the GC. The join is pushed rather than returned, because
    /// nobody asked for it: the reply carries <c>gc_initiated_join</c>, which is
    /// how the client knows to open the tab on its own.
    /// </summary>
    public void OnLogon(GcContext context)
    {
        lock (_gate)
        {
            foreach (var channel in _channels.Values.Where(channel => channel.AutoJoin).ToList())
            {
                if (channel.Members.ContainsKey(context.SteamId) || IsFull(channel))
                {
                    continue;
                }

                var response = Enter(context, channel);
                response.GcInitiatedJoin = true;
                _queue.Enqueue(context.AccountId, Message(GcMsg.JoinChatChannelResponse, response));
            }
        }
    }

    /// <summary>Joins (and, when the server allows it, opens) a channel by name.</summary>
    public CMsgDOTAJoinChatChannelResponse Join(GcContext context, CMsgDOTAJoinChatChannel request)
    {
        lock (_gate)
        {
            var name = request.ChannelName?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                return Failed(CMsgDOTAJoinChatChannelResponse.Result.InvalidChannelType, name, request.ChannelType);
            }

            if (ChannelsOf(context.SteamId).Count >= _options.MaxChannelsPerUser)
            {
                return Failed(CMsgDOTAJoinChatChannelResponse.Result.UserInTooManyChannels, name, request.ChannelType);
            }

            var channel = Find(name, request.ChannelType);
            if (channel is null)
            {
                var isPrivate = request.ChannelType == DOTAChatChannelTypet.DOTAChannelTypePrivate;
                if (!isPrivate && !_options.AllowCustomChannels)
                {
                    return Failed(CMsgDOTAJoinChatChannelResponse.Result.ChannelTypeDisabled, name, request.ChannelType);
                }

                channel = Create(name, request.ChannelType, _options.DefaultMaxMembers);
                if (isPrivate)
                {
                    // Opening a private chat makes its creator the first admin:
                    // an empty one nobody may invite into is unusable.
                    channel.Creator = context.AccountId;
                    channel.CreatedAt = (uint)_time.GetUtcNow().ToUnixTimeSeconds();
                    channel.Admins.Add(context.AccountId);
                    channel.Allowed.Add(context.AccountId);
                }
            }

            if (channel.Type == DOTAChatChannelTypet.DOTAChannelTypePrivate && !channel.Allowed.Contains(context.AccountId))
            {
                return Failed(CMsgDOTAJoinChatChannelResponse.Result.PrivateChatNoPermission, name, request.ChannelType);
            }

            if (!channel.Members.ContainsKey(context.SteamId) && IsFull(channel))
            {
                return Failed(CMsgDOTAJoinChatChannelResponse.Result.ChannelFull, name, request.ChannelType);
            }

            return Enter(context, channel);
        }
    }

    /// <summary>Leaves a channel; the other members are told, and a channel nobody configured dies with its last one.</summary>
    public void Leave(GcContext context, ulong channelId)
    {
        lock (_gate)
        {
            if (!_channels.TryGetValue(channelId, out var channel) ||
                !channel.Members.Remove(context.SteamId, out var member))
            {
                return;
            }

            Push(channel, new CMsgDOTAOtherLeftChatChannel
            {
                ChannelId = channel.Id,
                SteamId = context.SteamId,
                ChannelUserId = member.ChannelUserId
            }, GcMsg.OtherLeftChatChannel, excluding: context.SteamId);

            Prune(channel);
        }
    }

    /// <summary>
    /// Broadcasts one line to the channel it was sent to, sender included: the
    /// client draws what the GC echoes back, not what it typed. The author is
    /// stamped by the server — a client may not speak as somebody else, nor in a
    /// channel it is not in.
    /// </summary>
    public void Send(GcContext context, CMsgDOTAChatMessage message)
    {
        lock (_gate)
        {
            if (!_channels.TryGetValue(message.ChannelId, out var channel) ||
                !channel.Members.TryGetValue(context.SteamId, out var member))
            {
                return;
            }

            message.AccountId = context.AccountId;
            message.PersonaName = member.Name;
            message.ChannelUserId = member.ChannelUserId;
            message.Timestamp = (uint)_time.GetUtcNow().ToUnixTimeSeconds();
            if (message.Text.Length > _options.MaxMessageLength)
            {
                message.Text = message.Text[.._options.MaxMessageLength];
            }

            Push(
                channel,
                message,
                GcMsg.ChatMessage,
                excluding: _options.EchoOwnMessages ? 0 : context.SteamId);
        }
    }

    /// <summary>The channel list the client's chat window offers.</summary>
    public CMsgDOTARequestChatChannelListResponse List()
    {
        lock (_gate)
        {
            var response = new CMsgDOTARequestChatChannelListResponse();
            foreach (var channel in _channels.Values
                // Only the channels anybody may walk into. A party, lobby, team
                // or private channel belongs to the group that owns it and the
                // client opens it on its own.
                .Where(channel => channel.Type is DOTAChatChannelTypet.DOTAChannelTypeRegional
                    or DOTAChatChannelTypet.DOTAChannelTypeCustom)
                .OrderByDescending(channel => channel.IsConfigured)
                .ThenBy(channel => channel.Id))
            {
                response.Channels.Add(new CMsgDOTARequestChatChannelListResponse.ChatChannel
                {
                    ChannelName = channel.Name,
                    ChannelType = channel.Type,
                    NumMembers = (uint)channel.Members.Count
                });
            }

            return response;
        }
    }

    /// <summary>Everyone in a channel, for the member list the client draws beside it.</summary>
    public CMsgDOTAChatGetUserListResponse UserList(ulong channelId)
    {
        lock (_gate)
        {
            var response = new CMsgDOTAChatGetUserListResponse { ChannelId = channelId };
            if (!_channels.TryGetValue(channelId, out var channel))
            {
                return response;
            }

            foreach (var member in channel.Members.Values)
            {
                response.Members.Add(new CMsgDOTAChatGetUserListResponse.Member
                {
                    SteamId = member.SteamId,
                    PersonaName = member.Name,
                    ChannelUserId = member.ChannelUserId
                });
            }

            return response;
        }
    }

    /// <summary>How many players are in a channel the caller has not joined.</summary>
    public CMsgDOTAChatGetMemberCountResponse MemberCount(CMsgDOTAChatGetMemberCount request)
    {
        lock (_gate)
        {
            var channel = Find(request.ChannelName ?? string.Empty, request.ChannelType);
            return new CMsgDOTAChatGetMemberCountResponse
            {
                ChannelName = request.ChannelName ?? string.Empty,
                ChannelType = request.ChannelType,
                MemberCount = (uint)(channel?.Members.Count ?? 0)
            };
        }
    }

    /// <summary>Lets an account into a private chat. Only an admin of it may.</summary>
    public CMsgGCToClientPrivateChatResponse Invite(GcContext context, CMsgClientToGCPrivateChatInvite request)
    {
        lock (_gate)
        {
            var (channel, failure) = PrivateChat(context, request.PrivateChatChannelName, adminRequired: true);
            if (channel is null)
            {
                return PrivateResult(request.PrivateChatChannelName, failure);
            }

            if (request.InvitedAccountId == 0)
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureUnknownUser);
            }

            return PrivateResult(
                request.PrivateChatChannelName,
                channel.Allowed.Add(request.InvitedAccountId)
                    ? CMsgGCToClientPrivateChatResponse.Result.Success
                    : CMsgGCToClientPrivateChatResponse.Result.FailureAlreadyMember);
        }
    }

    /// <summary>Throws an account out of a private chat and closes the door behind it.</summary>
    public CMsgGCToClientPrivateChatResponse Kick(GcContext context, CMsgClientToGCPrivateChatKick request)
    {
        lock (_gate)
        {
            var (channel, failure) = PrivateChat(context, request.PrivateChatChannelName, adminRequired: true);
            if (channel is null)
            {
                return PrivateResult(request.PrivateChatChannelName, failure);
            }

            if (channel.Admins.Contains(request.KickAccountId))
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureCannotKickAdmin);
            }

            if (!channel.Allowed.Remove(request.KickAccountId))
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureNotAMember);
            }

            var kicked = channel.Members.Values.FirstOrDefault(member => member.AccountId == request.KickAccountId);
            if (kicked is not null)
            {
                channel.Members.Remove(kicked.SteamId);
                _queue.Enqueue(kicked.AccountId, Message(GcMsg.ToClientPrivateChatResponse, PrivateResult(
                    channel.Name,
                    CMsgGCToClientPrivateChatResponse.Result.Success)));
                Push(channel, new CMsgDOTAOtherLeftChatChannel
                {
                    ChannelId = channel.Id,
                    SteamId = kicked.SteamId,
                    ChannelUserId = kicked.ChannelUserId
                }, GcMsg.OtherLeftChatChannel);
            }

            return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.Success);
        }
    }

    /// <summary>Makes a member of a private chat an admin of it.</summary>
    public CMsgGCToClientPrivateChatResponse Promote(GcContext context, CMsgClientToGCPrivateChatPromote request)
    {
        lock (_gate)
        {
            var (channel, failure) = PrivateChat(context, request.PrivateChatChannelName, adminRequired: true);
            if (channel is null)
            {
                return PrivateResult(request.PrivateChatChannelName, failure);
            }

            if (!channel.Allowed.Contains(request.PromoteAccountId))
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureNotAMember);
            }

            return PrivateResult(
                request.PrivateChatChannelName,
                channel.Admins.Add(request.PromoteAccountId)
                    ? CMsgGCToClientPrivateChatResponse.Result.Success
                    : CMsgGCToClientPrivateChatResponse.Result.FailureAlreadyAdmin);
        }
    }

    /// <summary>Takes the admin flag off an account, never off the last admin.</summary>
    public CMsgGCToClientPrivateChatResponse Demote(GcContext context, CMsgClientToGCPrivateChatDemote request)
    {
        lock (_gate)
        {
            var (channel, failure) = PrivateChat(context, request.PrivateChatChannelName, adminRequired: true);
            if (channel is null)
            {
                return PrivateResult(request.PrivateChatChannelName, failure);
            }

            if (!channel.Admins.Contains(request.DemoteAccountId))
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureNotAMember);
            }

            if (channel.Admins.Count == 1)
            {
                return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.FailureNoRemainingAdmins);
            }

            channel.Admins.Remove(request.DemoteAccountId);
            return PrivateResult(request.PrivateChatChannelName, CMsgGCToClientPrivateChatResponse.Result.Success);
        }
    }

    /// <summary>Who may enter a private chat and who is in it right now.</summary>
    public CMsgGCToClientPrivateChatInfoResponse PrivateChatInfo(GcContext context, string channelName)
    {
        lock (_gate)
        {
            var response = new CMsgGCToClientPrivateChatInfoResponse { PrivateChatChannelName = channelName };
            var (channel, _) = PrivateChat(context, channelName, adminRequired: false);
            if (channel is null)
            {
                return response;
            }

            response.Creator = channel.Creator;
            response.CreationDate = channel.CreatedAt;
            foreach (var accountId in channel.Allowed)
            {
                var member = channel.Members.Values.FirstOrDefault(entry => entry.AccountId == accountId);
                response.Members.Add(new CMsgGCToClientPrivateChatInfoResponse.Member
                {
                    AccountId = accountId,
                    Name = member?.Name ?? string.Empty,
                    // The status enum is not in the 7.22g protos, only the
                    // number the client reads as offline (0) or online (1).
                    Status = member is not null || _players.IsOnline(SteamAccount.SteamIdFromAccountId(accountId))
                        ? 1u
                        : 0u
                });
            }

            return response;
        }
    }

    /// <summary>A channel and who is in it, for the HTTP read-only view.</summary>
    public IReadOnlyList<(ulong Id, string Name, DOTAChatChannelTypet Type, int MaxMembers, bool Configured, IReadOnlyList<(ulong SteamId, string Name)> Members)> Snapshot()
    {
        lock (_gate)
        {
            return _channels.Values
                .OrderByDescending(channel => channel.IsConfigured)
                .ThenBy(channel => channel.Id)
                .Select(channel => (
                    channel.Id,
                    channel.Name,
                    channel.Type,
                    channel.MaxMembers,
                    channel.IsConfigured,
                    (IReadOnlyList<(ulong, string)>)channel.Members.Values
                        .Select(member => (member.SteamId, member.Name))
                        .ToList()))
                .ToList();
        }
    }

    private CMsgDOTAJoinChatChannelResponse Enter(GcContext context, Channel channel)
    {
        if (!channel.Members.TryGetValue(context.SteamId, out var member))
        {
            member = new Member(context.SteamId, context.AccountId, context.PersonaName, ++channel.NextUserId);
            channel.Members.Add(context.SteamId, member);

            Push(channel, new CMsgDOTAOtherJoinedChatChannel
            {
                ChannelId = channel.Id,
                SteamId = member.SteamId,
                PersonaName = member.Name,
                ChannelUserId = member.ChannelUserId
            }, GcMsg.OtherJoinedChatChannel, excluding: member.SteamId);
        }

        var response = new CMsgDOTAJoinChatChannelResponse
        {
            Response = (uint)CMsgDOTAJoinChatChannelResponse.Result.JoinSuccess,
            ChannelName = channel.Name,
            ChannelId = channel.Id,
            ChannelType = channel.Type,
            MaxMembers = (uint)channel.MaxMembers,
            ChannelUserId = member.ChannelUserId,
            WelcomeMessage = channel.WelcomeMessage
        };

        foreach (var entry in channel.Members.Values)
        {
            response.Members.Add(new CMsgDOTAChatMember
            {
                SteamId = entry.SteamId,
                PersonaName = entry.Name,
                ChannelUserId = entry.ChannelUserId
            });
        }

        return response;
    }

    private (Channel? Channel, CMsgGCToClientPrivateChatResponse.Result Failure) PrivateChat(
        GcContext context,
        string? name,
        bool adminRequired)
    {
        var channel = Find(name ?? string.Empty, DOTAChatChannelTypet.DOTAChannelTypePrivate);
        if (channel is null)
        {
            return (null, CMsgGCToClientPrivateChatResponse.Result.FailureUnknownChannelName);
        }

        if (!channel.Allowed.Contains(context.AccountId) ||
            (adminRequired && !channel.Admins.Contains(context.AccountId)))
        {
            return (null, CMsgGCToClientPrivateChatResponse.Result.FailureNoPermission);
        }

        return (channel, CMsgGCToClientPrivateChatResponse.Result.Success);
    }

    private static CMsgGCToClientPrivateChatResponse PrivateResult(
        string? channelName,
        CMsgGCToClientPrivateChatResponse.Result result) => new()
        {
            PrivateChatChannelName = channelName ?? string.Empty,
            result = result
        };

    private Channel Create(string name, DOTAChatChannelTypet type, int maxMembers)
    {
        var channel = new Channel(++_sequence, name, type, maxMembers);
        _channels.Add(channel.Id, channel);
        return channel;
    }

    private Channel? Find(string name, DOTAChatChannelTypet type) =>
        _channels.Values.FirstOrDefault(channel =>
            channel.Type == type && string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase));

    private List<Channel> ChannelsOf(ulong steamId) =>
        _channels.Values.Where(channel => channel.Members.ContainsKey(steamId)).ToList();

    private bool IsFull(Channel channel) => channel.Members.Count >= channel.MaxMembers;

    /// <summary>A channel nobody configured only exists while somebody is in it.</summary>
    private void Prune(Channel channel)
    {
        if (channel.Members.Count == 0 && !channel.IsConfigured &&
            channel.Type != DOTAChatChannelTypet.DOTAChannelTypePrivate)
        {
            _channels.Remove(channel.Id);
        }
    }

    private static CMsgDOTAJoinChatChannelResponse Failed(
        CMsgDOTAJoinChatChannelResponse.Result result,
        string name,
        DOTAChatChannelTypet type) => new()
        {
            Response = (uint)result,
            ChannelName = name,
            ChannelType = type
        };

    /// <summary>
    /// Sends one message to the channel. A join or a leave is news to everyone
    /// but the player it is about (<paramref name="excluding"/>, which learns it
    /// from its own reply); a chat line excludes nobody, since the sender's
    /// window draws the echo rather than what it typed.
    /// </summary>
    private void Push<T>(Channel channel, T body, uint messageType, ulong excluding = 0)
    {
        var message = Message(messageType, body);
        foreach (var member in channel.Members.Values.Where(member => member.SteamId != excluding))
        {
            _queue.Enqueue(member.AccountId, message);
        }
    }

    private GcMessage Message<T>(uint messageType, T body) => new(messageType, _codec.Encode(body));

    private sealed class Channel
    {
        public Channel(ulong id, string name, DOTAChatChannelTypet type, int maxMembers)
        {
            Id = id;
            Name = name;
            Type = type;
            MaxMembers = maxMembers;
        }

        public ulong Id { get; }
        public string Name { get; }
        public DOTAChatChannelTypet Type { get; }
        public int MaxMembers { get; }
        public bool IsConfigured { get; set; }
        public bool AutoJoin { get; set; }
        public string WelcomeMessage { get; set; } = string.Empty;
        public uint Creator { get; set; }
        public uint CreatedAt { get; set; }
        public uint NextUserId { get; set; }
        public HashSet<uint> Admins { get; } = [];
        public HashSet<uint> Allowed { get; } = [];
        public Dictionary<ulong, Member> Members { get; } = [];
    }

    private sealed record Member(ulong SteamId, uint AccountId, string Name, uint ChannelUserId);
}
